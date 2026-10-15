using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ASL.Api;
using ASL.Core;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Untyped view of one bound setting, for ASL's own UI (the F8 "Settings" page builds its controls
    /// from <see cref="ModConfig.Entries"/> without knowing T). Mods never see this type.
    /// </summary>
    internal abstract class ConfigEntryInfo
    {
        public string Section { get; protected set; }
        public string Key { get; protected set; }
        public string Description { get; protected set; }

        /// <summary>The T of the entry (bool, int, float, double, string, an enum or KeyCode).</summary>
        public Type ValueType { get; protected set; }

        /// <summary>True for <c>BindRange</c> entries; <see cref="Min"/>/<see cref="Max"/> are then the boxed bounds.</summary>
        public bool HasRange { get; protected set; }
        public object Min { get; protected set; }
        public object Max { get; protected set; }

        public bool IsEnum => ValueType.IsEnum;
        public bool IsKeyCode => ValueType == typeof(KeyCode);

        /// <summary>Enum member names in declaration order (null for non-enums).</summary>
        public string[] EnumNames { get; protected set; }

        public object DefaultBoxed { get; protected set; }

        /// <summary>Current value, boxed. The setter coerces (float→int rounds, string→enum parses) and clamps to the range.</summary>
        public abstract object BoxedValue { get; set; }

        public abstract void ResetToDefault();

        /// <summary>Raised after the value changed (menu, code or file reload). Handlers are isolated by the raiser.</summary>
        public event Action ValueChanged;

        protected void RaiseValueChanged(Action<Exception> onError)
        {
            var h = ValueChanged;
            if (h == null) return;
            foreach (var d in h.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception ex) { onError(ex); }
            }
        }
    }

    /// <summary>
    /// Per-mod <see cref="IAslConfig"/> over a BepInEx <c>ConfigFile</c> at
    /// <c>BepInEx/config/&lt;modId&gt;.cfg</c> (<c>SaveOnConfigSet</c> on). The file object is created on the
    /// first Bind/Save/Reload, so a mod that never uses config costs nothing and touches no disk.
    /// </summary>
    internal sealed class ModConfig : IAslConfig
    {
        private readonly string _modId;
        private readonly ManualLogSource _log;
        private ConfigFile _file;
        private string _path;
        private readonly Dictionary<string, ConfigEntryInfo> _byId = new Dictionary<string, ConfigEntryInfo>(StringComparer.Ordinal);
        private readonly List<ConfigEntryInfo> _entries = new List<ConfigEntryInfo>();

        public ModConfig(string modId, ManualLogSource log)
        {
            _modId = modId;
            _log = log;
        }

        public bool ShowInMenu { get; set; } = true;

        public string FilePath => _path ??= Path.Combine(Paths.ConfigPath, StorageNames.ToSafeFileName(_modId) + ".cfg");

        public event Action Changed;

        /// <summary>Every bound entry in bind order (for the F8 Settings page).</summary>
        internal IReadOnlyList<ConfigEntryInfo> Entries => _entries;

        internal string ModId => _modId;

        public IConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description = null)
            => BindCore(section, key, defaultValue, description, null, null, null);

        public IConfigEntry<float> BindRange(string section, string key, float defaultValue, float min, float max, string description = null)
        {
            if (min > max) { var t = min; min = max; max = t; }
            return BindCore(section, key, Math.Clamp(defaultValue, min, max), description, new AcceptableValueRange<float>(min, max), min, max);
        }

        public IConfigEntry<int> BindRange(string section, string key, int defaultValue, int min, int max, string description = null)
        {
            if (min > max) { var t = min; min = max; max = t; }
            return BindCore(section, key, Math.Clamp(defaultValue, min, max), description, new AcceptableValueRange<int>(min, max), min, max);
        }

        public void Save()
        {
            if (_file == null) return;   // nothing bound yet, nothing to write
            try { _file.Save(); }
            catch (Exception ex) { _log.LogError($"[{_modId}] Config.Save failed: {ex.Message}"); }
        }

        public void Reload()
        {
            if (_file == null) return;
            try { _file.Reload(); }   // BepInEx raises each changed entry's SettingChanged → our Changed events
            catch (Exception ex) { _log.LogError($"[{_modId}] Config.Reload failed: {ex.Message}"); }
        }

        private ModConfigEntry<T> BindCore<T>(string section, string key, T defaultValue, string description,
                                              AcceptableValueBase range, object min, object max)
        {
            if (string.IsNullOrWhiteSpace(section)) throw new ArgumentException("section is empty", nameof(section));
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("key is empty", nameof(key));

            var id = section + "\n" + key;
            if (_byId.TryGetValue(id, out var existing))
            {
                if (existing is ModConfigEntry<T> same) return same;
                throw new InvalidOperationException($"config entry [{section}] {key} is already bound as {existing.ValueType.Name}, not {typeof(T).Name}");
            }
            if (!TomlTypeConverter.CanConvert(typeof(T)))
                throw new NotSupportedException($"config type {typeof(T).Name} is not supported (use bool, int, float, double, string, an enum or KeyCode)");

            var file = EnsureFile();
            var raw = file.Bind(new ConfigDefinition(section, key), defaultValue, new ConfigDescription(description ?? "", range));
            var entry = new ModConfigEntry<T>(this, raw, section, key, defaultValue, description, min, max);
            _byId[id] = entry;
            _entries.Add(entry);
            return entry;
        }

        private ConfigFile EnsureFile()
        {
            if (_file != null) return _file;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var f = new ConfigFile(FilePath, false);
            f.SaveOnConfigSet = true;
            _file = f;
            return f;
        }

        internal void RaiseChanged()
        {
            var h = Changed;
            if (h == null) return;
            foreach (var d in h.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception ex) { _log.LogError($"[{_modId}] Config.Changed handler threw: {ex.Message}"); }
            }
        }

        internal void LogHandlerError(string what, Exception ex) => _log.LogError($"[{_modId}] {what} handler threw: {ex.Message}");

        private sealed class ModConfigEntry<T> : ConfigEntryInfo, IConfigEntry<T>
        {
            private readonly ModConfig _owner;
            private readonly ConfigEntry<T> _raw;
            private readonly T _default;

            public ModConfigEntry(ModConfig owner, ConfigEntry<T> raw, string section, string key, T def, string description, object min, object max)
            {
                _owner = owner;
                _raw = raw;
                _default = def;
                Section = section;
                Key = key;
                Description = description;
                ValueType = typeof(T);
                DefaultBoxed = def;
                if (min != null) { HasRange = true; Min = min; Max = max; }
                if (typeof(T).IsEnum) EnumNames = Enum.GetNames(typeof(T));
                raw.SettingChanged += OnRawChanged;
            }

            public T Value { get => _raw.Value; set => _raw.Value = value; }   // BepInEx clamps to the range and saves
            public T DefaultValue => _default;
            string IConfigEntry<T>.Section => Section;
            string IConfigEntry<T>.Key => Key;
            string IConfigEntry<T>.Description => Description;

            public event Action<T> Changed;

            public void Reset() => _raw.Value = _default;
            public override void ResetToDefault() => Reset();

            public override object BoxedValue
            {
                get => _raw.Value;
                set => _raw.Value = Coerce(value);
            }

            private void OnRawChanged(object sender, EventArgs e)
            {
                var h = Changed;
                if (h != null)
                {
                    var v = _raw.Value;
                    foreach (var d in h.GetInvocationList())
                    {
                        try { ((Action<T>)d)(v); }
                        catch (Exception ex) { _owner.LogHandlerError($"Config entry [{Section}] {Key} Changed", ex); }
                    }
                }
                RaiseValueChanged(ex => _owner.LogHandlerError($"Config entry [{Section}] {Key} ValueChanged", ex));
                _owner.RaiseChanged();
            }

            private static T Coerce(object v)
            {
                if (v is T t) return t;
                if (v == null) throw new ArgumentNullException(nameof(v));
                var tt = typeof(T);
                if (tt.IsEnum) return (T)(v is string s ? Enum.Parse(tt, s, true) : Enum.ToObject(tt, v));
                if (tt == typeof(int)) return (T)(object)(int)Math.Round(Convert.ToDouble(v, CultureInfo.InvariantCulture), MidpointRounding.AwayFromZero);
                return (T)Convert.ChangeType(v, tt, CultureInfo.InvariantCulture);
            }
        }
    }
}
