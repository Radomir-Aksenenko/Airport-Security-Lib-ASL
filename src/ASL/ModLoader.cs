using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ASL.Api;
using ASL.Core;
using BepInEx;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Scans the game's <c>mods/</c> folder, reads each mod's <c>manifest.json</c>, and loads it
    /// by kind: <b>dll</b> (compiled AslMod), <b>script</b> (Roslyn-compiled .cs), or <b>content</b>
    /// (declarative, no-code). Every mod is isolated: a single mod throwing during discovery or
    /// load is logged and skipped, never aborting the others.
    ///
    /// ASL 2.0 loads in three phases: (1) parse EVERY manifest, (2) resolve dependencies / enabled state /
    /// minimum ASL version into a deterministic load order (<see cref="DependencyResolver"/>), (3) load the
    /// survivors in that order. Every mod — loaded, failed, disabled, skipped, content — has a registry
    /// record with its <see cref="ModState"/> and an error text. Manifests without any v2 field behave exactly
    /// as before (folder order, same log lines).
    ///
    /// Script mods can be reloaded at runtime (<see cref="Reload"/>), either on request (Mod Manager, console)
    /// or automatically when the manifest says <c>"hotReload": true</c> (source mtimes polled once a second).
    /// </summary>
    internal sealed class ModLoader : IFrameParticipant
    {
        private readonly ManualLogSource _log;
        private readonly string _modsRoot;
        private readonly IAslEvents _bus;
        private readonly IModHooks _hooks;
        private readonly HookManager _hookMgr;
        private readonly ContentRegistry _content;
        private readonly MenuManager _menu;
        private readonly IAslNet _net;
        private readonly UiManager _ui;
        private readonly KeybindManager _keys;
        private readonly List<LoadedMod> _loaded = new();
        private readonly List<Candidate> _candidates = new();
        private readonly Dictionary<string, Candidate> _byId = new(StringComparer.Ordinal);
        private ModInfo _current;   // registry record of the mod currently being loaded (null between mods)
        private bool _reloading;
        private bool _watching;     // registered with the bus for hot-reload polling
        private float _pollAccum;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        /// <summary>Everything the loader knows about one mod folder with a usable manifest.</summary>
        private sealed class Candidate
        {
            public string Folder;
            public string Dir;
            public ModManifest Manifest;
            public ModKind Kind;
            public string Display;
            public ModInfo Info;
            public bool Blocked;            // the resolver refused it (disabled / missing dependency / ...): never loadable this session
            public int ReloadCount;         // compile counter for unique script assembly names
            public long SourceSignature;    // hot reload: signature of the sources at the last (re)load attempt
            public long PendingSignature;   // hot reload: a change seen on the previous poll, waiting to settle
        }

        public ModLoader(ManualLogSource log, string modsRoot, IAslEvents bus, IModHooks hooks, ContentRegistry content, MenuManager menu, IAslNet net, UiManager ui, KeybindManager keys)
        {
            _log = log;
            _modsRoot = modsRoot;
            _bus = bus;
            _hooks = hooks;
            _hookMgr = hooks as HookManager;
            _content = content;
            _menu = menu;
            _net = net;
            _ui = ui;
            _keys = keys;
        }

        public IReadOnlyList<LoadedMod> Loaded => _loaded;

        /// <summary>The loaded mod with this id (null if it is not currently loaded, or is a content mod without code).</summary>
        public LoadedMod FindLoaded(string modId)
        {
            foreach (var m in _loaded) if (string.Equals(m.Manifest.Id, modId, StringComparison.Ordinal)) return m;
            return null;
        }

        public void DiscoverAndLoad()
        {
            if (!Directory.Exists(_modsRoot))
            {
                Directory.CreateDirectory(_modsRoot);
                _log.LogInfo($"Created mods folder: {_modsRoot}");
            }

            try { ModStates.Init(_log); }
            catch (Exception ex) { _log.LogWarning($"Mod enable/disable states unavailable: {ex.Message}"); }

            var dirs = Directory.GetDirectories(_modsRoot);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);   // deterministic regardless of the file system
            _log.LogInfo($"Scanning mods/ : {dirs.Length} candidate folder(s) in {_modsRoot}");

            // Phase 1: read every manifest.
            foreach (var dir in dirs)
            {
                try
                {
                    var c = ParseFolder(dir);
                    if (c != null) _candidates.Add(c);
                }
                catch (Exception ex) { _log.LogError($"Mod folder '{Path.GetFileName(dir)}' failed to load: {ex}"); }
            }

            // Phase 2: dependencies, enabled state, versions -> load order.
            var order = Plan();

            // Phase 3: load in that order.
            foreach (var c in order)
            {
                try { LoadCandidate(c); }
                catch (Exception ex) { _log.LogError($"Mod folder '{c.Folder}' failed to load: {ex}"); }
            }

            _log.LogInfo($"Mod loading complete: {_loaded.Count} mod(s) active.");

            StartWatchingIfNeeded();
            RaiseAllModsLoaded();
        }

        // ---- phase 1: manifests -------------------------------------------------------------

        private Candidate ParseFolder(string dir)
        {
            var folderName = Path.GetFileName(dir);
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _log.LogWarning($"Skipping '{folderName}': no manifest.json.");
                return null;
            }

            ModManifest manifest;
            try { manifest = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(manifestPath), JsonOpts); }
            catch (Exception ex)
            {
                _log.LogError($"Skipping '{folderName}': invalid manifest.json ({ex.Message}).");
                RegisterBroken(folderName, dir, $"invalid manifest.json ({ex.Message})");
                return null;
            }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                _log.LogError($"Skipping '{folderName}': manifest is missing the required 'id' field.");
                RegisterBroken(folderName, dir, "manifest is missing the required 'id' field");
                return null;
            }

            var kind = ResolveKind(manifest, dir);
            var display = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name;
            var info = ModInfo.From(manifest, KindName(kind), dir, display);
            AslPlugin.Registry?.Add(info);

            return new Candidate { Folder = folderName, Dir = dir, Manifest = manifest, Kind = kind, Display = display, Info = info };
        }

        // ---- phase 2: plan --------------------------------------------------------------------

        // Runs the resolver, records the verdict on every registry entry and returns the candidates to load, in order.
        private List<Candidate> Plan()
        {
            var order = new List<Candidate>();
            ResolveResult rr = null;
            try
            {
                var nodes = new List<ResolverMod>(_candidates.Count);
                foreach (var c in _candidates)
                {
                    var m = c.Manifest;
                    nodes.Add(new ResolverMod
                    {
                        Id = m.Id,
                        Version = m.Version,
                        Folder = c.Folder,
                        Dependencies = m.Dependencies,
                        OptionalDependencies = m.OptionalDependencies,
                        LoadAfter = m.LoadAfter,
                        LoadBefore = m.LoadBefore,
                        MinAslVersion = m.MinAslVersion,
                        Disabled = !ModStates.IsEnabled(m.Id),
                        Tag = c,
                    });
                }
                rr = DependencyResolver.Resolve(nodes, AslVersion.Current);
            }
            catch (Exception ex)
            {
                // A resolver bug must never stop mods from loading: fall back to plain folder order.
                _log.LogError($"Dependency resolution failed ({ex.Message}); loading every mod in folder order.");
            }

            if (rr == null)
            {
                foreach (var c in _candidates) { Index(c); order.Add(c); }
                return order;
            }

            foreach (var w in rr.Warnings) _log.LogWarning($"Mod ordering: {w}");

            foreach (var r in rr.All)
            {
                var c = (Candidate)r.Mod.Tag;
                if (r.State == ResolvedState.Load) { Index(c); continue; }

                c.Blocked = true;
                if (!_byId.ContainsKey(c.Manifest.Id) && r.State != ResolvedState.Failed) _byId[c.Manifest.Id] = c;
                ApplyVerdict(c, r);
            }

            foreach (var r in rr.LoadOrder) order.Add((Candidate)r.Mod.Tag);
            return order;
        }

        private void Index(Candidate c)
        {
            if (!_byId.ContainsKey(c.Manifest.Id)) _byId[c.Manifest.Id] = c;
        }

        private void ApplyVerdict(Candidate c, ResolvedMod r)
        {
            var info = c.Info;
            var id = c.Manifest.Id;
            switch (r.State)
            {
                case ResolvedState.Disabled:
                    info.State = ModState.Disabled;
                    _log.LogInfo($"Mod '{c.Display}' (id={id}) is disabled - not loading.");
                    break;
                case ResolvedState.MissingDependency:
                    info.State = ModState.MissingDependency;
                    _log.LogWarning($"Skipping mod '{c.Display}' (id={id}): {r.Reason}.");
                    break;
                case ResolvedState.Incompatible:
                    info.State = ModState.Incompatible;
                    _log.LogWarning($"Skipping mod '{c.Display}' (id={id}): {r.Reason}.");
                    break;
                default:
                    info.State = ModState.Failed;
                    _log.LogError($"Mod '{c.Display}' (id={id}) cannot load: {r.Reason}.");
                    break;
            }
            info.Error = r.Reason;
        }

        // ---- lifecycle events -------------------------------------------------------------------

        // Every mod's OnLoad has run: tell each code mod (isolated per mod), then raise the events.
        private void RaiseAllModsLoaded()
        {
            foreach (var m in _loaded.ToArray())
            {
                if (m.Instance == null) continue;
                try { m.Instance.OnAllModsLoaded(); }
                catch (Exception ex) { _log.LogError($"'{m.Manifest.Id}' OnAllModsLoaded threw: {ex}"); }
            }
            try { AslPlugin.Registry?.RaiseAllModsLoaded(); }
            catch (Exception ex) { _log.LogError($"AllModsLoaded (Mods) raise failed: {ex.Message}"); }
            try { (_bus as EventBus)?.RaiseAllModsLoaded(); }
            catch (Exception ex) { _log.LogError($"AllModsLoaded raise failed: {ex.Message}"); }
        }

        public void UnloadAll()
        {
            if (_watching) { _watching = false; (_bus as EventBus)?.Unregister((IFrameParticipant)this); }
            // Reverse load order: dependents are torn down before the mods they depend on.
            for (int i = _loaded.Count - 1; i >= 0; i--)
            {
                var m = _loaded[i];
                try { m.Instance?.OnUnload(); }
                catch (Exception ex) { _log.LogError($"'{m.Manifest.Id}' OnUnload threw: {ex.Message}"); }
                try { m.Services?.Release(); }
                catch (Exception ex) { _log.LogError($"'{m.Manifest.Id}' release threw: {ex.Message}"); }
                try { m.ContentRelease?.Invoke(); }
                catch (Exception ex) { _log.LogError($"'{m.Manifest.Id}' UI release threw: {ex.Message}"); }
            }
            _loaded.Clear();
        }

        // ---- phase 3: load one mod --------------------------------------------------------------

        private void LoadCandidate(Candidate c)
        {
            var manifest = c.Manifest;
            var dir = c.Dir;

            // A hard dependency that FAILED to load (compile error, OnLoad threw...) must not let its dependents load.
            var depProblem = HardDependencyProblem(c);
            if (depProblem != null)
            {
                c.Blocked = true;
                c.Info.State = ModState.MissingDependency;
                c.Info.Error = depProblem;
                _log.LogWarning($"Skipping mod '{c.Display}' (id={manifest.Id}): {depProblem}.");
                return;
            }

            _log.LogInfo($"Loading mod '{c.Display}' v{manifest.Version} [{c.Kind.ToString().ToLowerInvariant()}] (id={manifest.Id})");

            if (c.Kind == ModKind.Script && manifest.HotReload) c.SourceSignature = Signature(c);

            _current = c.Info;
            try
            {
                switch (c.Kind)
                {
                    case ModKind.Dll:     LoadDllMod(manifest, dir); break;
                    case ModKind.Script:  LoadScriptMod(c); break;
                    case ModKind.Content: LoadContentMod(manifest, dir); break;
                    default:              Fail(manifest, "could not determine mod kind - nothing to load.", LogLevel.Warning); break;
                }

                // Declarative windows listed in the manifest's top-level "ui" (any kind; content mods are handled in LoadContentMod).
                if (c.Kind != ModKind.Content && c.Info.State == ModState.Loaded)
                    LoadManifestUi(manifest, dir, c.Display, FindLoaded(manifest.Id));
            }
            catch (Exception ex)
            {
                if (_current != null) { _current.State = ModState.Failed; _current.Error = ex.Message; }
                throw;   // DiscoverAndLoad logs it, exactly as before
            }
            finally { _current = null; }
        }

        // null if every hard dependency is loaded; otherwise why not. Dependencies load first (resolver order), so their state is final here.
        private string HardDependencyProblem(Candidate c)
        {
            var deps = c.Manifest.Dependencies;
            if (deps == null) return null;
            foreach (var raw in deps)
            {
                if (!DependencySpec.TryParse(raw, out var spec, out _)) continue;
                if (!_byId.TryGetValue(spec.Id, out var dep) || dep.Info == null) continue;   // not installed: the resolver already handled that
                if (dep.Info.State != ModState.Loaded)
                    return $"requires '{spec.Id}' which did not load ({dep.Info.State}{(string.IsNullOrEmpty(dep.Info.Error) ? "" : ": " + dep.Info.Error)})";
            }
            return null;
        }

        private static string KindName(ModKind k) => k == ModKind.Dll ? "dll" : k == ModKind.Script ? "script" : k == ModKind.Content ? "content" : "unknown";

        // A folder whose manifest is unusable still shows up in the registry (as Failed), keyed by folder name.
        private void RegisterBroken(string folderName, string dir, string error)
        {
            AslPlugin.Registry?.Add(new ModInfo { Id = folderName, Name = folderName, Version = "?", Kind = "unknown", Directory = dir, State = ModState.Failed, Error = error });
        }

        // Log a load problem exactly like the old inline code did, and record it on the mod's registry entry.
        private void Fail(ModManifest m, string message, LogLevel level = LogLevel.Error)
        {
            var line = $"  '{m.Id}': {message}";
            if (level == LogLevel.Warning) _log.LogWarning(line); else _log.LogError(line);
            if (_current != null) { _current.State = ModState.Failed; _current.Error = message; }
        }

        private ModKind ResolveKind(ModManifest m, string dir)
        {
            if (!string.IsNullOrWhiteSpace(m.Type))
            {
                switch (m.Type.Trim().ToLowerInvariant())
                {
                    case "dll": return ModKind.Dll;
                    case "script": return ModKind.Script;
                    case "content": return ModKind.Content;
                }
            }

            if (Directory.GetFiles(dir, "*.dll").Length > 0) return ModKind.Dll;
            if (Directory.GetFiles(dir, "*.cs").Length > 0) return ModKind.Script;
            return ModKind.Content;
        }

        // ---- DLL mods --------------------------------------------------------------------

        private void LoadDllMod(ModManifest manifest, string dir)
        {
            string dllPath = null;
            if (!string.IsNullOrWhiteSpace(manifest.Entry))
            {
                var candidate = Path.Combine(dir, manifest.Entry);
                if (File.Exists(candidate)) dllPath = candidate;
            }
            if (dllPath == null)
            {
                var dlls = Directory.GetFiles(dir, "*.dll");
                if (dlls.Length == 0) { Fail(manifest, "type=dll but no .dll file found."); return; }
                dllPath = dlls[0];
            }

            Assembly asm;
            try { asm = Assembly.LoadFrom(dllPath); }
            catch (Exception ex) { Fail(manifest, $"failed to load assembly {Path.GetFileName(dllPath)}: {ex.Message}"); return; }

            var lm = InstantiateAndLoad(asm, manifest, dir, false);
            if (lm != null) _loaded.Add(lm);
        }

        // ---- Script mods (Roslyn) --------------------------------------------------------

        private static string[] ScriptFiles(string dir, ModManifest manifest)
        {
            if (!string.IsNullOrWhiteSpace(manifest.Entry) && File.Exists(Path.Combine(dir, manifest.Entry)))
                return new[] { Path.Combine(dir, manifest.Entry) };
            return Directory.GetFiles(dir, "*.cs");
        }

        // Compile + instantiate a script mod. Returns the loaded mod, or null after recording the failure.
        private LoadedMod LoadScript(Candidate c, bool reload)
        {
            var manifest = c.Manifest;
            var dir = c.Dir;
            var scripts = ScriptFiles(dir, manifest);

            if (scripts.Length == 0) { Fail(manifest, "type=script but no .cs file found."); return null; }

            var interopDir = Path.Combine(Paths.GameRootPath, "BepInEx", "interop");
            // The first compile keeps the historical assembly name; recompiles need a fresh one (a second in-memory
            // assembly with the same name cannot be told apart from the first by the runtime).
            var asmName = $"ScriptMod_{Sanitize(manifest.Id)}" + (c.ReloadCount > 0 ? $"_r{c.ReloadCount}" : "");
            c.ReloadCount++;

            Assembly asm;
            try { asm = ScriptLoader.Compile(asmName, scripts, interopDir, _log); }
            catch (Exception ex) { Fail(manifest, $"script compilation crashed: {ex.Message}"); return null; }

            if (asm == null) { Fail(manifest, "script did not compile (see errors above)."); return null; }
            return InstantiateAndLoad(asm, manifest, dir, reload);
        }

        private void LoadScriptMod(Candidate c)
        {
            var lm = LoadScript(c, false);
            if (lm != null) _loaded.Add(lm);
        }

        // ---- Content mods (no code) ------------------------------------------------------

        private void LoadContentMod(ModManifest manifest, string dir)
        {
            bool hasUi = manifest.Ui != null && manifest.Ui.Length > 0;
            if (manifest.Content == null && !hasUi)
            {
                Fail(manifest, "type=content but no 'content' section in manifest.", LogLevel.Warning);
                return;
            }

            if (manifest.Content != null && manifest.Content.ListTextureNames) _content.RequestTextureNameDump();

            int queued = 0;
            if (manifest.Content?.Textures != null)
            {
                foreach (var tr in manifest.Content.Textures)
                {
                    if (tr == null || string.IsNullOrWhiteSpace(tr.Target) || string.IsNullOrWhiteSpace(tr.File)) continue;
                    var path = Path.Combine(dir, tr.File);
                    if (!File.Exists(path)) { _log.LogError($"  '{manifest.Id}': texture file not found: {tr.File}"); continue; }
                    try
                    {
                        _content.RegisterTextureSwap(manifest.Id, tr.Target, File.ReadAllBytes(path));
                        queued++;
                    }
                    catch (Exception ex) { _log.LogError($"  '{manifest.Id}': failed to read {tr.File}: {ex.Message}"); }
                }
            }

            _content.RegisterContentV2(manifest, dir);   // audio / strings / listAudioNames (no-ops when absent)

            var content = new LoadedMod(manifest, null, null) { Info = _current };
            _loaded.Add(content);
            MarkLoaded();
            LoadManifestUi(manifest, dir, string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name, content);
            _log.LogInfo($"  '{manifest.Id}': content mod loaded ({queued} texture swap(s) queued; applied on scene load).");
        }

        // ---- Shared instantiation (dll + script) -----------------------------------------

        // Builds the mod's services + context, runs OnLoad and returns the LoadedMod (null on failure; the failure is
        // recorded on _current). Does NOT add to _loaded — the caller decides where it goes.
        private LoadedMod InstantiateAndLoad(Assembly asm, ModManifest manifest, string dir, bool reload)
        {
            Type modType = null;
            foreach (var t in SafeGetTypes(asm))
            {
                if (t != null && !t.IsAbstract && typeof(AslMod).IsAssignableFrom(t)) { modType = t; break; }
            }
            if (modType == null) { Fail(manifest, "no public AslMod subclass found."); return null; }

            AslMod instance;
            try { instance = (AslMod)Activator.CreateInstance(modType); }
            catch (Exception ex) { Fail(manifest, $"failed to instantiate {modType.Name}: {ex.Message}"); return null; }

            var display = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name;
            var modLog = BepInEx.Logging.Logger.CreateLogSource(display);
            var section = _menu.EnsureSection(display);              // one section shared by Menu + Input
            if (reload) section.Controls.Clear();                     // the reloaded mod re-adds its rows in OnLoad
            var menu = new ModMenu(section);
            var input = new ModInput(manifest.Id, display, section, _keys, modLog);
            var info = _current ?? ModInfo.From(manifest, "dll", dir, display);   // _current is only null if the registry is unavailable
            var services = ModServices.Create(info, display, modLog, _bus, _ui);
            section.Config = services.Config as ModConfig;           // F8 "Settings" page (auto-UI for ctx.Config entries)
            services.Resources.Add(input.Release);   // this mod's keybind Pressed handlers go away with it

            IModHooks hooks = _hooks;
            if (_hookMgr != null)
            {
                var mh = new ModHooks(_hookMgr, manifest.Id);
                services.Resources.Add(mh.Release);   // this mod's hook callbacks go away with it
                hooks = mh;
            }

            IAslNet net = _net;
            if (_net != null)
            {
                var tn = new TrackingNet(_net);
                services.Resources.Add(tn.Release);   // this mod's net subscriptions / event handlers go away with it
                net = tn;
            }

            var ctx = new ModContext(manifest.Id, display, dir, modLog, services, hooks, menu, input, net);

            try
            {
                instance.OnLoad(ctx);
                MarkLoaded();
                _log.LogInfo($"  '{manifest.Id}': loaded OK.");
                return new LoadedMod(manifest, instance, ctx) { Info = info };
            }
            catch (Exception ex)
            {
                _log.LogError($"  '{manifest.Id}': OnLoad threw: {ex}");
                if (_current != null) { _current.State = ModState.Failed; _current.Error = "OnLoad threw: " + ex.Message; }
                // A mod that failed to load must not keep running half-initialised: drop whatever OnLoad registered.
                try { services.Release(); } catch (Exception rex) { _log.LogWarning($"  '{manifest.Id}': releasing after failed OnLoad threw: {rex.Message}"); }
                section.Controls.Clear();   // and the F8 rows it added before failing
                section.Config = null;
                return null;
            }
        }

        // Loads the layouts a manifest lists under "ui" through the mod's own ModUi. Code mods use the ModUi from their
        // services (so named actions, config binds and release all work); content mods have no services, so they get a
        // minimal ModUi (+ asset access for images) that is released with the mod. Never throws; one bad file never
        // stops the others.
        private void LoadManifestUi(ModManifest manifest, string dir, string display, LoadedMod lm)
        {
            var files = manifest.Ui;
            if (files == null || files.Length == 0 || lm == null) return;
            // Mods load at plugin Load, before any scene / TMP settings / fonts: build the layouts once the UI runtime is
            // Ready (first scene change or ~60 frames). A mod unloaded / reloaded meanwhile is skipped.
            UiRuntime.RunWhenReady(() =>
            {
                if (!_loaded.Contains(lm)) return;
                LoadManifestUiNow(manifest, dir, display, lm, files);
            });
        }

        private void LoadManifestUiNow(ModManifest manifest, string dir, string display, LoadedMod lm, string[] files)
        {
            try
            {
                var ui = lm.Services?.Ui as ModUi;
                if (ui == null)
                {
                    var modLog = BepInEx.Logging.Logger.CreateLogSource(display);
                    ui = new ModUi(manifest.Id, display, dir, _ui, modLog);
                    var assets = new ModAssets(manifest.Id, dir, modLog);
                    ui.AttachStandalone(assets);
                    lm.ContentRelease = () => { ui.ReleaseAll(); assets.Dispose(); };
                }

                foreach (var file in files)
                {
                    if (string.IsNullOrWhiteSpace(file)) continue;
                    try
                    {
                        var w = ui.LoadLayout(file);
                        if (w != null) _log.LogInfo($"  '{manifest.Id}': UI layout '{file}' loaded (window '{w.WindowId}').");
                        else _log.LogWarning($"  '{manifest.Id}': UI layout '{file}' could not be loaded (see errors above).");
                    }
                    catch (Exception ex) { _log.LogError($"  '{manifest.Id}': UI layout '{file}' failed: {ex.Message}"); }
                }
            }
            catch (Exception ex) { _log.LogError($"  '{manifest.Id}': loading UI layouts failed: {ex.Message}"); }
        }

        private void MarkLoaded()
        {
            if (_current == null) return;
            _current.State = ModState.Loaded;
            _current.Error = null;
        }

        // ---- hot reload (script mods) ------------------------------------------------------

        /// <summary>True if <paramref name="modId"/> is a script mod that <see cref="Reload"/> can act on (loaded, or failed after it started loading).</summary>
        public bool CanReload(string modId)
        {
            return modId != null && _byId.TryGetValue(modId, out var c) && c.Kind == ModKind.Script && !c.Blocked;
        }

        /// <summary>
        /// Reload a script mod: <c>OnUnload</c>, release every resource it created (timers, event handlers, hooks,
        /// commands, windows, ...), recompile its sources, create a new instance with fresh services, run
        /// <c>OnLoad</c> then <c>OnAllModsLoaded</c>. A mod that failed to compile (or whose OnLoad threw) can be
        /// reloaded again after fixing it. Never throws. Main thread only.
        /// </summary>
        /// <returns><c>ok</c> and a human-readable message (the failure reason when not ok).</returns>
        public (bool ok, string message) Reload(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId)) return (false, "no mod id given");
            if (_reloading) return (false, "another reload is already in progress");
            if (!_byId.TryGetValue(modId, out var c)) return (false, $"no mod with id '{modId}'");
            if (c.Kind != ModKind.Script) return (false, $"'{modId}' is a {KindName(c.Kind)} mod; only script mods can be reloaded");
            if (c.Blocked) return (false, $"'{modId}' did not load this session ({c.Info.State}: {c.Info.Error}); fix that and restart");

            _reloading = true;
            var prevCurrent = _current;
            try
            {
                var manifest = c.Manifest;
                _log.LogInfo($"Reloading mod '{c.Display}' (id={manifest.Id})");

                // 1. Tear the old instance down (OnUnload first so it can still use its services, then release).
                int slot = -1;
                for (int i = 0; i < _loaded.Count; i++)
                    if (ReferenceEquals(_loaded[i].Info, c.Info) || string.Equals(_loaded[i].Manifest.Id, manifest.Id, StringComparison.Ordinal)) { slot = i; break; }
                if (slot >= 0)
                {
                    var old = _loaded[slot];
                    try { old.Instance?.OnUnload(); }
                    catch (Exception ex) { _log.LogError($"'{manifest.Id}' OnUnload threw: {ex.Message}"); }
                    try { old.Services?.Release(); }
                    catch (Exception ex) { _log.LogError($"'{manifest.Id}' release threw: {ex.Message}"); }
                    _loaded.RemoveAt(slot);
                }

                c.Info.State = ModState.Failed;
                c.Info.Error = "Reload did not finish.";
                if (manifest.HotReload) { c.SourceSignature = Signature(c); c.PendingSignature = 0; }

                // 2. Recompile and load the new one.
                _current = c.Info;
                var lm = LoadScript(c, true);
                if (lm == null)
                {
                    var why = c.Info.Error ?? "unknown error";
                    _log.LogWarning($"Reload of '{manifest.Id}' failed: {why}");
                    try { var st = _menu.EnsureSection(c.Display); st.Controls.Clear(); st.Config = null; } catch { }   // stale rows of the old instance
                    return (false, why);
                }

                _loaded.Insert(slot >= 0 && slot <= _loaded.Count ? slot : _loaded.Count, lm);
                LoadManifestUi(manifest, c.Dir, c.Display, lm);   // the old windows went with the released services

                // 3. Everything is loaded: tell the new instance (and its own event subscribers) like at startup.
                try { lm.Instance.OnAllModsLoaded(); }
                catch (Exception ex) { _log.LogError($"'{manifest.Id}' OnAllModsLoaded threw: {ex}"); }
                lm.Services?.RaiseAllModsLoaded();

                _log.LogInfo($"Reloaded '{c.Display}' v{manifest.Version}.");
                return (true, $"reloaded '{c.Display}'");
            }
            catch (Exception ex)
            {
                _log.LogError($"Reload of '{modId}' crashed: {ex}");
                if (c.Info != null) { c.Info.State = ModState.Failed; c.Info.Error = "reload crashed: " + ex.Message; }
                return (false, "reload crashed: " + ex.Message);
            }
            finally
            {
                _current = prevCurrent;
                _reloading = false;
            }
        }

        // Hot-reload watching: script mods with "hotReload": true are polled for source changes once a second.
        private void StartWatchingIfNeeded()
        {
            if (_watching) return;
            bool any = false;
            foreach (var c in _candidates)
                if (c.Kind == ModKind.Script && c.Manifest.HotReload && !c.Blocked) { any = true; break; }
            if (!any) return;

            var bus = _bus as EventBus;
            if (bus == null) return;
            bus.Register((IFrameParticipant)this);
            _watching = true;
            _log.LogInfo("Hot reload: watching script mods that set \"hotReload\": true.");
        }

        void IFrameParticipant.FrameUpdate(float deltaTime, float unscaledDeltaTime)
        {
            _pollAccum += unscaledDeltaTime;
            if (_pollAccum < 1f) return;
            _pollAccum = 0f;
            try { PollSources(); }
            catch (Exception ex) { _log.LogError($"Hot reload poll failed: {ex.Message}"); }
        }

        void IFrameParticipant.FrameLateUpdate() { }

        private void PollSources()
        {
            if (_reloading) return;
            for (int i = 0; i < _candidates.Count; i++)
            {
                var c = _candidates[i];
                if (c.Kind != ModKind.Script || !c.Manifest.HotReload || c.Blocked) continue;

                long sig = Signature(c);
                if (sig == c.SourceSignature) { c.PendingSignature = 0; continue; }

                // Reload only once the change has settled for a full poll (editors often write in several steps).
                if (sig != c.PendingSignature) { c.PendingSignature = sig; continue; }

                c.PendingSignature = 0;
                c.SourceSignature = sig;   // set before reloading so a failing compile is not retried until the next edit
                _log.LogInfo($"[hotReload] '{c.Manifest.Id}' sources changed.");
                var r = Reload(c.Manifest.Id);
                if (!r.ok) _log.LogWarning($"[hotReload] '{c.Manifest.Id}': {r.message}");
            }
        }

        // Cheap fingerprint of a script mod's source files (count + mtimes + sizes). -1 if the folder is unreadable.
        private static long Signature(Candidate c)
        {
            try
            {
                var files = ScriptFiles(c.Dir, c.Manifest);
                long h = files.Length;
                unchecked
                {
                    foreach (var f in files)
                    {
                        var fi = new FileInfo(f);
                        h = h * 31 + fi.LastWriteTimeUtc.Ticks;
                        h = h * 31 + fi.Length;
                    }
                    // Also the mod's declarative UI layouts and translations: a change to ui/*.json (or the manifest's
                    // "ui" files) or lang/*.json reloads the mod too (simplest correct: the new instance re-reads them).
                    foreach (var f in DataFiles(c.Dir, c.Manifest))
                    {
                        var fi = new FileInfo(f);
                        h = h * 31 + (fi.Exists ? fi.LastWriteTimeUtc.Ticks : 1);
                        h = h * 31 + (fi.Exists ? fi.Length : 0);
                    }
                }
                return h;
            }
            catch { return -1; }
        }

        // ui/*.json, lang/*.json and the manifest's own "ui" entries, sorted (stable fingerprint); never throws.
        private static List<string> DataFiles(string dir, ModManifest manifest)
        {
            var list = new List<string>();
            try
            {
                foreach (var sub in new[] { "ui", "lang" })
                {
                    var d = Path.Combine(dir, sub);
                    if (Directory.Exists(d)) list.AddRange(Directory.GetFiles(d, "*.json"));
                }
                if (manifest?.Ui != null)
                    foreach (var u in manifest.Ui)
                    {
                        if (string.IsNullOrWhiteSpace(u)) continue;
                        var full = Path.Combine(dir, u);
                        if (!list.Contains(full)) list.Add(full);
                    }
                list.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch { }
            return list;
        }

        private static string Sanitize(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
            return new string(chars);
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                var list = new List<Type>();
                if (ex.Types != null)
                    foreach (var t in ex.Types) if (t != null) list.Add(t);
                return list.ToArray();
            }
        }
    }

    /// <summary>A mod that has been loaded. <see cref="Instance"/>/<see cref="Context"/> are null for content mods.</summary>
    internal sealed class LoadedMod
    {
        public ModManifest Manifest { get; }
        public AslMod Instance { get; }
        public IModContext Context { get; }

        /// <summary>The registry record for this mod (state, error, ...).</summary>
        public ModInfo Info { get; internal set; }

        /// <summary>The per-mod services (null for content mods). Released when the mod unloads.</summary>
        public ModServices Services => (Context as ModContext)?.Services;

        /// <summary>Content mods that declared "ui" layouts: releases their windows / asset cache on unload (null otherwise).</summary>
        internal Action ContentRelease { get; set; }

        public LoadedMod(ModManifest manifest, AslMod instance, IModContext context)
        {
            Manifest = manifest;
            Instance = instance;
            Context = context;
        }
    }
}
