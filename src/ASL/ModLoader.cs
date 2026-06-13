using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ASL.Api;
using BepInEx;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Scans the game's <c>mods/</c> folder, reads each mod's <c>manifest.json</c>, and loads it
    /// by kind: <b>dll</b> (compiled AslMod), <b>script</b> (Roslyn-compiled .cs), or <b>content</b>
    /// (declarative, no-code). Every mod is isolated: a single mod throwing during discovery or
    /// load is logged and skipped, never aborting the others.
    /// </summary>
    internal sealed class ModLoader
    {
        private readonly ManualLogSource _log;
        private readonly string _modsRoot;
        private readonly IAslEvents _bus;
        private readonly IModHooks _hooks;
        private readonly ContentRegistry _content;
        private readonly List<LoadedMod> _loaded = new();

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        public ModLoader(ManualLogSource log, string modsRoot, IAslEvents bus, IModHooks hooks, ContentRegistry content)
        {
            _log = log;
            _modsRoot = modsRoot;
            _bus = bus;
            _hooks = hooks;
            _content = content;
        }

        public IReadOnlyList<LoadedMod> Loaded => _loaded;

        public void DiscoverAndLoad()
        {
            if (!Directory.Exists(_modsRoot))
            {
                Directory.CreateDirectory(_modsRoot);
                _log.LogInfo($"Created mods folder: {_modsRoot}");
            }

            var dirs = Directory.GetDirectories(_modsRoot);
            _log.LogInfo($"Scanning mods/ : {dirs.Length} candidate folder(s) in {_modsRoot}");

            foreach (var dir in dirs)
            {
                try { LoadOne(dir); }
                catch (Exception ex) { _log.LogError($"Mod folder '{Path.GetFileName(dir)}' failed to load: {ex}"); }
            }

            _log.LogInfo($"Mod loading complete: {_loaded.Count} mod(s) active.");
        }

        public void UnloadAll()
        {
            foreach (var m in _loaded)
            {
                try { m.Instance?.OnUnload(); }
                catch (Exception ex) { _log.LogError($"'{m.Manifest.Id}' OnUnload threw: {ex.Message}"); }
            }
            _loaded.Clear();
        }

        private void LoadOne(string dir)
        {
            var folderName = Path.GetFileName(dir);
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _log.LogWarning($"Skipping '{folderName}': no manifest.json.");
                return;
            }

            ModManifest manifest;
            try { manifest = JsonSerializer.Deserialize<ModManifest>(File.ReadAllText(manifestPath), JsonOpts); }
            catch (Exception ex) { _log.LogError($"Skipping '{folderName}': invalid manifest.json ({ex.Message})."); return; }

            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                _log.LogError($"Skipping '{folderName}': manifest is missing the required 'id' field.");
                return;
            }

            var kind = ResolveKind(manifest, dir);
            var display = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name;
            _log.LogInfo($"Loading mod '{display}' v{manifest.Version} [{kind}] (id={manifest.Id})");

            switch (kind)
            {
                case ModKind.Dll:     LoadDllMod(manifest, dir); break;
                case ModKind.Script:  LoadScriptMod(manifest, dir); break;
                case ModKind.Content: LoadContentMod(manifest, dir); break;
                default:              _log.LogWarning($"  '{manifest.Id}': could not determine mod kind - nothing to load."); break;
            }
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
                if (dlls.Length == 0) { _log.LogError($"  '{manifest.Id}': type=dll but no .dll file found."); return; }
                dllPath = dlls[0];
            }

            Assembly asm;
            try { asm = Assembly.LoadFrom(dllPath); }
            catch (Exception ex) { _log.LogError($"  '{manifest.Id}': failed to load assembly {Path.GetFileName(dllPath)}: {ex.Message}"); return; }

            InstantiateAndLoad(asm, manifest, dir);
        }

        // ---- Script mods (Roslyn) --------------------------------------------------------

        private void LoadScriptMod(ModManifest manifest, string dir)
        {
            string[] scripts;
            if (!string.IsNullOrWhiteSpace(manifest.Entry) && File.Exists(Path.Combine(dir, manifest.Entry)))
                scripts = new[] { Path.Combine(dir, manifest.Entry) };
            else
                scripts = Directory.GetFiles(dir, "*.cs");

            if (scripts.Length == 0) { _log.LogError($"  '{manifest.Id}': type=script but no .cs file found."); return; }

            var interopDir = Path.Combine(Paths.GameRootPath, "BepInEx", "interop");
            Assembly asm;
            try { asm = ScriptLoader.Compile($"ScriptMod_{Sanitize(manifest.Id)}", scripts, interopDir, _log); }
            catch (Exception ex) { _log.LogError($"  '{manifest.Id}': script compilation crashed: {ex.Message}"); return; }

            if (asm == null) { _log.LogError($"  '{manifest.Id}': script did not compile (see errors above)."); return; }
            InstantiateAndLoad(asm, manifest, dir);
        }

        // ---- Content mods (no code) ------------------------------------------------------

        private void LoadContentMod(ModManifest manifest, string dir)
        {
            if (manifest.Content == null)
            {
                _log.LogWarning($"  '{manifest.Id}': type=content but no 'content' section in manifest.");
                return;
            }

            if (manifest.Content.ListTextureNames) _content.RequestTextureNameDump();

            int queued = 0;
            if (manifest.Content.Textures != null)
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

            _loaded.Add(new LoadedMod(manifest, null, null));
            _log.LogInfo($"  '{manifest.Id}': content mod loaded ({queued} texture swap(s) queued; applied on scene load).");
        }

        // ---- Shared instantiation (dll + script) -----------------------------------------

        private void InstantiateAndLoad(Assembly asm, ModManifest manifest, string dir)
        {
            Type modType = null;
            foreach (var t in SafeGetTypes(asm))
            {
                if (t != null && !t.IsAbstract && typeof(AslMod).IsAssignableFrom(t)) { modType = t; break; }
            }
            if (modType == null) { _log.LogError($"  '{manifest.Id}': no public AslMod subclass found."); return; }

            AslMod instance;
            try { instance = (AslMod)Activator.CreateInstance(modType); }
            catch (Exception ex) { _log.LogError($"  '{manifest.Id}': failed to instantiate {modType.Name}: {ex.Message}"); return; }

            var display = string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id : manifest.Name;
            var modLog = BepInEx.Logging.Logger.CreateLogSource(display);
            var ctx = new ModContext(manifest.Id, display, dir, modLog, _bus, _hooks);

            try
            {
                instance.OnLoad(ctx);
                _loaded.Add(new LoadedMod(manifest, instance, ctx));
                _log.LogInfo($"  '{manifest.Id}': loaded OK.");
            }
            catch (Exception ex) { _log.LogError($"  '{manifest.Id}': OnLoad threw: {ex}"); }
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

        public LoadedMod(ModManifest manifest, AslMod instance, IModContext context)
        {
            Manifest = manifest;
            Instance = instance;
            Context = context;
        }
    }
}
