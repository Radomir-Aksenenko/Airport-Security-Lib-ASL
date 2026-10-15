using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ASL.Api;
using ASL.Core;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslLocalization"/>: <c>lang/*.json</c> in the mod folder (flat or nested JSON).
    /// Files are discovered and parsed lazily on first use; the file is chosen by
    /// <see cref="LanguageResolver"/> from the game's language token (<see cref="Loc.CurrentLanguage"/>).
    /// Lookup chain: current language → <c>en</c> → the key itself. When the shared bus reports a language
    /// change the cached tables are dropped (re-read on next use) and <see cref="LanguageChanged"/> fires.
    /// The constructor touches nothing, and the bus is only subscribed once the mod actually uses localization.
    /// </summary>
    internal sealed class ModLocalization : IAslLocalization, IDisposable
    {
        private readonly string _modId;
        private readonly string _modDirectory;
        private readonly ManualLogSource _log;
        private readonly IAslEvents _bus;
        private bool _subscribed;
        private bool _disposed;
        private Action _changed;

        private Dictionary<string, string> _files;       // lang file base name → absolute path (lazy)
        private List<string> _names;                     // sorted base names
        private Dictionary<string, string> _current;     // table for the resolved language (lazy; null = not loaded)
        private Dictionary<string, string> _english;     // table for en (null = not loaded / same as current)
        private bool _englishIsCurrent;
        private readonly HashSet<string> _warned = new HashSet<string>();

        public ModLocalization(string modId, string modDirectory, ManualLogSource log, IAslEvents bus = null)
        {
            _modId = modId;
            _modDirectory = modDirectory;
            _log = log;
            _bus = bus;
        }

        public string Language => Loc.CurrentLanguage();

        public event Action LanguageChanged
        {
            add { if (_disposed) return; _changed += value; Subscribe(); }
            remove { _changed -= value; }
        }

        public string Get(string key, params object[] args)
        {
            if (key == null) return "";
            EnsureTables();
            string text;
            if (!TryGet(key, out text)) return key;
            if (args == null || args.Length == 0) return text;
            try { return string.Format(text, args); }
            catch (FormatException) { return text; }   // translator typo: show the raw string rather than throw at the mod
        }

        public bool Has(string key)
        {
            if (key == null) return false;
            EnsureTables();
            return TryGet(key, out _);
        }

        /// <summary>Stop listening to the shared bus (mod unloaded / hot-reloaded).</summary>
        internal void Detach()
        {
            if (_subscribed && _bus != null) _bus.LanguageChanged -= OnBusLanguageChanged;
            _subscribed = false;
        }

        /// <summary>Release: unsubscribe from the shared bus and drop the mod's handlers. Idempotent.</summary>
        public void Dispose()
        {
            _disposed = true;
            Detach();
            _changed = null;
        }

        // Kept for older call sites; raises the mod-facing event.
        internal void RaiseLanguageChanged()
        {
            var h = _changed;
            if (h == null) return;
            foreach (var d in h.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception ex) { _log.LogError($"[{_modId}] Loc.LanguageChanged handler threw: {ex.Message}"); }
            }
        }

        // ---- internals ----------------------------------------------------------------------

        private void Subscribe()
        {
            if (_subscribed || _bus == null) return;
            _bus.LanguageChanged += OnBusLanguageChanged;
            _subscribed = true;
        }

        private void OnBusLanguageChanged(string lang)
        {
            _current = null;     // reload against the new language on next use
            _english = null;
            _files = null;       // pick up lang files added meanwhile (cheap; only on a language switch)
            RaiseLanguageChanged();
        }

        private bool TryGet(string key, out string text)
        {
            if (_current != null && _current.TryGetValue(key, out text)) return true;
            if (!_englishIsCurrent && _english != null && _english.TryGetValue(key, out text)) return true;
            text = null;
            return false;
        }

        private void EnsureTables()
        {
            if (_current != null) return;
            Subscribe();   // from now on we must hear about language switches

            if (_files == null) Discover();

            _current = new Dictionary<string, string>();
            _english = null;
            _englishIsCurrent = true;
            if (_names.Count == 0) return;

            var chosen = LanguageResolver.Pick(_names, Language);
            _current = LoadTable(chosen);

            // The resolver falls back to "first available" when there is no English, so check the pick really is English.
            var en = LanguageResolver.Pick(_names, "en");
            if (en != null && IsEnglishName(en) && !string.Equals(en, chosen, StringComparison.OrdinalIgnoreCase))
            {
                _english = LoadTable(en);
                _englishIsCurrent = false;
            }
        }

        private static bool IsEnglishName(string n)
        {
            n = n.Replace('_', '-').ToLowerInvariant();
            return n == "en" || n.StartsWith("en-", StringComparison.Ordinal);
        }

        private void Discover()
        {
            _files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _names = new List<string>();
            try
            {
                var dir = Path.Combine(_modDirectory, "lang");
                if (!Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                {
                    var n = Path.GetFileNameWithoutExtension(f);
                    if (n.Length == 0 || _files.ContainsKey(n)) continue;
                    _files[n] = f;
                    _names.Add(n);
                }
                _names.Sort(StringComparer.OrdinalIgnoreCase);   // deterministic "first available"
            }
            catch (Exception ex) { Warn("discover", $"could not list lang/ folder: {ex.Message}"); }
        }

        private Dictionary<string, string> LoadTable(string name)
        {
            if (name == null || !_files.TryGetValue(name, out var path)) return new Dictionary<string, string>();
            try { return JsonLangTable.Parse(File.ReadAllText(path, Encoding.UTF8)); }
            catch (Exception ex)
            {
                Warn("parse:" + name, $"lang/{name}.json is not valid ({ex.Message}); its strings are ignored.");
                return new Dictionary<string, string>();
            }
        }

        private void Warn(string id, string message)
        {
            if (_warned.Add(id)) _log.LogWarning($"[{_modId}] {message}");
        }
    }
}
