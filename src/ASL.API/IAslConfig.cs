using System;

namespace ASL.Api
{
    /// <summary>
    /// Typed, persisted settings for one mod, reached through <c>ctx.Config</c>. Backed by a standard
    /// BepInEx config file at <c>BepInEx/config/&lt;modId&gt;.cfg</c>, so players can also edit it by hand.
    /// Entries appear automatically on the mod's F8 page under "Settings" (see <see cref="ShowInMenu"/>).
    /// <code>
    /// var speed = ctx.Config.BindRange("General", "Speed", 2f, 0.5f, 10f, "Walk speed multiplier");
    /// var god   = ctx.Config.Bind("General", "GodMode", false);
    /// ctx.Events.Update += () => { if (god.Value) Heal(speed.Value); };
    /// </code>
    /// Supported value types: bool, int, float, double, string, enums and <c>UnityEngine.KeyCode</c>.
    /// </summary>
    public interface IAslConfig
    {
        /// <summary>
        /// Bind (create or fetch) a setting. The first call creates it with <paramref name="defaultValue"/>
        /// unless the file already holds a value. Setting <see cref="IConfigEntry{T}.Value"/> saves it.
        /// </summary>
        /// <param name="section">Group name in the file/menu.</param>
        /// <param name="key">Setting name, unique within the section.</param>
        /// <param name="defaultValue">Value used when nothing is saved.</param>
        /// <param name="description">Shown in the file and as a tooltip in the menu.</param>
        IConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description = null);

        /// <summary>Bind a float limited to [<paramref name="min"/>, <paramref name="max"/>]; shown as a slider.</summary>
        IConfigEntry<float> BindRange(string section, string key, float defaultValue, float min, float max, string description = null);

        /// <summary>Bind an int limited to [<paramref name="min"/>, <paramref name="max"/>]; shown as a slider.</summary>
        IConfigEntry<int> BindRange(string section, string key, int defaultValue, int min, int max, string description = null);

        /// <summary>
        /// When true (default) bound entries appear on the mod's F8 page under "Settings": bool as a toggle,
        /// ranged values as a slider, enums as a dropdown, strings as an input, KeyCode as a key capture.
        /// </summary>
        bool ShowInMenu { get; set; }

        /// <summary>Absolute path of the <c>.cfg</c> file.</summary>
        string FilePath { get; }

        /// <summary>Write all entries to disk now (setting a value already does this).</summary>
        void Save();

        /// <summary>Re-read the file from disk, raising <see cref="IConfigEntry{T}.Changed"/> for entries that changed.</summary>
        void Reload();

        /// <summary>Raised after any entry's value changes.</summary>
        event Action Changed;
    }

    /// <summary>One bound setting. Obtain it from <see cref="IAslConfig.Bind{T}"/>.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IConfigEntry<T>
    {
        /// <summary>Section name.</summary>
        string Section { get; }

        /// <summary>Key name.</summary>
        string Key { get; }

        /// <summary>The current value. Setting it saves the file and raises <see cref="Changed"/>.</summary>
        T Value { get; set; }

        /// <summary>The default value passed to Bind.</summary>
        T DefaultValue { get; }

        /// <summary>The description passed to Bind (may be null).</summary>
        string Description { get; }

        /// <summary>Raised with the new value whenever it changes (menu, code or file reload).</summary>
        event Action<T> Changed;

        /// <summary>Set the value back to <see cref="DefaultValue"/>.</summary>
        void Reset();
    }
}
