using System;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslLocalization"/> (<c>lang/*.json</c> in the mod folder).
    /// Owned by the Data agent. Placeholder: <see cref="Language"/> works, lookups echo the key.
    /// </summary>
    internal sealed class ModLocalization : IAslLocalization
    {
        private readonly string _modId;
        private readonly string _modDirectory;
        private readonly ManualLogSource _log;

        public ModLocalization(string modId, string modDirectory, ManualLogSource log)
        {
            _modId = modId;
            _modDirectory = modDirectory;
            _log = log;
        }

        public string Language => Loc.CurrentLanguage();

        public event Action LanguageChanged;

        public string Get(string key, params object[] args) => throw new NotImplementedException("ASL 2.0: Loc.Get not implemented yet");
        public bool Has(string key) => throw new NotImplementedException("ASL 2.0: Loc.Has not implemented yet");

        internal void RaiseLanguageChanged() { try { LanguageChanged?.Invoke(); } catch (Exception ex) { _log.LogError($"[{_modId}] Loc.LanguageChanged handler threw: {ex.Message}"); } }
    }
}
