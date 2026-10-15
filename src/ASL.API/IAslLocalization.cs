using System;

namespace ASL.Api
{
    /// <summary>
    /// Per-mod translations, reached through <c>ctx.Loc</c>. Put flat JSON files in your mod's
    /// <c>lang/</c> folder (<c>lang/en.json</c>, <c>lang/ru.json</c>, ...) mapping keys to text.
    /// Lookup order: the game's current language file, then <c>en.json</c>, then the key itself.
    /// <code>
    /// // lang/en.json: { "greeting": "Hello, {0}!" }
    /// win.AddLabel(ctx.Loc.Get("greeting", playerName));
    /// </code>
    /// </summary>
    public interface IAslLocalization
    {
        /// <summary>The game's current language token (e.g. <c>"en"</c>, <c>"ru"</c>); null if unknown.</summary>
        string Language { get; }

        /// <summary>Translate <paramref name="key"/>; <paramref name="args"/> are applied with <c>string.Format</c>. Falls back to en, then to the key.</summary>
        string Get(string key, params object[] args);

        /// <summary>True if the key exists in the current language or in English.</summary>
        bool Has(string key);

        /// <summary>Raised when the player changes the game language.</summary>
        event Action LanguageChanged;
    }
}
