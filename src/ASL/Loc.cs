namespace ASL
{
    /// <summary>
    /// Localizes the "Mods" label. The game's string tables don't contain a "Mods" entry (and a new
    /// key can't be reliably injected into the Addressables-backed, per-locale tables at runtime), so
    /// we detect the current language via the game's <c>LanguageManager</c> and supply our own text.
    /// "Mods" is a loanword in most languages, so only a few differ.
    /// </summary>
    internal static class Loc
    {
        // Matched against the current-language string (code or name), lowercased, by substring.
        private static readonly (string token, string text)[] Map =
        {
            ("ru", "Моды"), ("rus", "Моды"), ("русск", "Моды"),
            ("uk", "Моди"), ("укра", "Моди"),
            ("zh", "模组"), ("chin", "模组"), ("中", "模组"),
            ("ja", "モッド"), ("japan", "モッド"), ("日本", "モッド"),
            ("ko", "모드"), ("한국", "모드"),
            ("pl", "Mody"), ("pol", "Mody"),
        };

        public static string Mods()
        {
            var lang = CurrentLanguage();
            if (!string.IsNullOrEmpty(lang))
            {
                lang = lang.ToLowerInvariant();
                foreach (var e in Map)
                    if (lang.Contains(e.token)) return e.text;
            }
            return "Mods";
        }

        public static string CurrentLanguage()
        {
            try { return LanguageManager.CurrentLanguage; }
            catch { return null; }
        }
    }
}
