namespace ASL.Api
{
    /// <summary>
    /// Per-mod save data, reached through <c>ctx.Storage</c>. Files live in
    /// <see cref="DataDirectory"/> (outside the mod folder, so they survive mod updates). Use it for
    /// progress, unlocks, high scores — anything that is data rather than a setting (for settings use
    /// <see cref="IAslConfig"/>).
    /// <code>
    /// class Save { public int Coins; public string[] Unlocked = new string[0]; }
    /// var save = ctx.Storage.Load("save", new Save());
    /// save.Coins += 10;
    /// ctx.Storage.Save("save", save);
    /// </code>
    /// Names are sanitised: path separators and ".." are replaced with '_', and when a name had to change a short
    /// hash suffix (<c>~xxxx</c>) is appended so different names stay distinct; a name can never escape the folder.
    /// </summary>
    public interface IAslStorage
    {
        /// <summary>Absolute folder holding this mod's data: <c>BepInEx/config/ASL/data/&lt;modId&gt;/</c>.</summary>
        string DataDirectory { get; }

        /// <summary>Serialise <paramref name="value"/> as JSON (System.Text.Json) to <c>&lt;name&gt;.json</c>, atomically (temp file + replace).</summary>
        void Save<T>(string name, T value);

        /// <summary>Load <c>&lt;name&gt;.json</c>. Returns <paramref name="fallback"/> when the file is missing or corrupt (corruption is logged).</summary>
        T Load<T>(string name, T fallback = default);

        /// <summary>True if <c>&lt;name&gt;.json</c> exists.</summary>
        bool Exists(string name);

        /// <summary>Delete <c>&lt;name&gt;.json</c> if it exists.</summary>
        void Delete(string name);

        /// <summary>Write raw text to <c>&lt;name&gt;.txt</c>, atomically.</summary>
        void SaveText(string name, string text);

        /// <summary>Read <c>&lt;name&gt;.txt</c>, or <paramref name="fallback"/> if missing.</summary>
        string LoadText(string name, string fallback = null);
    }
}
