using System;
using ASL.Api;
using BepInEx.Logging;

namespace ASL
{
    /// <summary>
    /// Per-mod <see cref="IAslConfig"/> (BepInEx <c>ConfigFile</c> at <c>BepInEx/config/&lt;modId&gt;.cfg</c>).
    /// Owned by the Data agent. Placeholder: constructing it must stay side-effect free (it is created for
    /// every mod at boot); the members throw until implemented.
    /// </summary>
    internal sealed class ModConfig : IAslConfig
    {
        private readonly string _modId;
        private readonly ManualLogSource _log;

        public ModConfig(string modId, ManualLogSource log)
        {
            _modId = modId;
            _log = log;
        }

        public bool ShowInMenu { get; set; } = true;
        public string FilePath => throw NotYet("Config.FilePath");

        public event Action Changed;

        public IConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description = null) => throw NotYet("Config.Bind");
        public IConfigEntry<float> BindRange(string section, string key, float defaultValue, float min, float max, string description = null) => throw NotYet("Config.BindRange");
        public IConfigEntry<int> BindRange(string section, string key, int defaultValue, int min, int max, string description = null) => throw NotYet("Config.BindRange");
        public void Save() => throw NotYet("Config.Save");
        public void Reload() => throw NotYet("Config.Reload");

        private void RaiseChanged() { try { Changed?.Invoke(); } catch (Exception ex) { _log.LogError($"[{_modId}] Config.Changed handler threw: {ex.Message}"); } }

        private static NotImplementedException NotYet(string what) => new NotImplementedException($"ASL 2.0: {what} not implemented yet");
    }
}
