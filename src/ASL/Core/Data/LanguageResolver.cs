using System;
using System.Collections.Generic;

namespace ASL.Core
{
    /// <summary>
    /// Picks which language file to use (pure C#). Inputs: the available file base names ("en", "ru",
    /// "pt-BR"...) and the game's language token, which may be a code ("ru", "en-US") or an English/native
    /// name ("Russian", "Русский"). Order: exact (case-insensitive) → region/2-letter code prefix → known
    /// name→code map → first two letters → "en" → first available.
    /// </summary>
    public static class LanguageResolver
    {
        // Lowercase name (or prefix thereof) → ISO code. Matched with StartsWith so "chinese (simplified)" works.
        private static readonly (string name, string code)[] Names =
        {
            ("english", "en"), ("английский", "en"),
            ("russian", "ru"), ("русский", "ru"), ("русская", "ru"),
            ("ukrainian", "uk"), ("українська", "uk"), ("украинский", "uk"),
            ("chinese", "zh"), ("中文", "zh"), ("简体", "zh"), ("繁體", "zh"), ("繁体", "zh"), ("китайский", "zh"),
            ("japanese", "ja"), ("日本語", "ja"), ("японский", "ja"),
            ("korean", "ko"), ("한국어", "ko"), ("корейский", "ko"),
            ("german", "de"), ("deutsch", "de"), ("немецкий", "de"),
            ("french", "fr"), ("français", "fr"), ("francais", "fr"), ("французский", "fr"),
            ("spanish", "es"), ("español", "es"), ("espanol", "es"), ("castilian", "es"), ("испанский", "es"),
            ("italian", "it"), ("italiano", "it"), ("итальянский", "it"),
            ("portuguese", "pt"), ("português", "pt"), ("portugues", "pt"), ("португальский", "pt"),
            ("polish", "pl"), ("polski", "pl"), ("польский", "pl"),
            ("turkish", "tr"), ("türkçe", "tr"), ("turkce", "tr"),
            ("czech", "cs"), ("čeština", "cs"), ("cestina", "cs"),
            ("dutch", "nl"), ("nederlands", "nl"),
            ("swedish", "sv"), ("svenska", "sv"),
            ("arabic", "ar"), ("العربية", "ar"),
            ("hungarian", "hu"), ("magyar", "hu"),
            ("romanian", "ro"), ("română", "ro"),
            ("thai", "th"), ("ไทย", "th"),
            ("vietnamese", "vi"), ("tiếng việt", "vi"),
            ("indonesian", "id"), ("bahasa indonesia", "id"),
        };

        /// <summary>
        /// Choose a language file. Returns one of <paramref name="available"/> (original spelling), or null if
        /// the list is empty.
        /// </summary>
        public static string Pick(IReadOnlyList<string> available, string token)
        {
            if (available == null || available.Count == 0) return null;

            var t = Normalize(token);
            if (t.Length > 0)
            {
                // 1. exact file name
                var hit = Find(available, t);
                if (hit != null) return hit;

                // 2. code with region ("en-us", "pt_br") or a plain 2-3 letter code → base code, matched against names and bases
                var baseCode = BaseCode(t);
                if (baseCode != null)
                {
                    hit = FindByCode(available, baseCode);
                    if (hit != null) return hit;
                }

                // 3. language name → code
                var code = CodeForName(t);
                if (code != null)
                {
                    hit = FindByCode(available, code);
                    if (hit != null) return hit;
                }

                // 4. first two letters ("deu..." → "de")
                if (t.Length > 2)
                {
                    hit = FindByCode(available, t.Substring(0, 2));
                    if (hit != null) return hit;
                }
            }

            // 5. English, 6. whatever exists
            return FindByCode(available, "en") ?? available[0];
        }

        /// <summary>The ISO code for a language name/token ("Russian" → "ru"), or null if unknown.</summary>
        public static string CodeForName(string token)
        {
            var t = Normalize(token);
            if (t.Length == 0) return null;
            foreach (var (name, code) in Names)
                if (t.StartsWith(name, StringComparison.Ordinal)) return code;
            return null;
        }

        private static string Normalize(string s) => (s ?? "").Trim().Replace('_', '-').ToLowerInvariant();

        // "en-us" → "en"; "ru" → "ru"; "russian" → null (not code-like).
        private static string BaseCode(string t)
        {
            int dash = t.IndexOf('-');
            var head = dash < 0 ? t : t.Substring(0, dash);
            if (head.Length < 2 || head.Length > 3) return null;
            foreach (var c in head) if (c < 'a' || c > 'z') return null;
            return head;
        }

        private static string Find(IReadOnlyList<string> available, string normalized)
        {
            for (int i = 0; i < available.Count; i++)
                if (Normalize(available[i]) == normalized) return available[i];
            return null;
        }

        // Exact name == code, else a file whose own base code is the code ("en-US" for "en").
        private static string FindByCode(IReadOnlyList<string> available, string code)
        {
            var exact = Find(available, code);
            if (exact != null) return exact;
            for (int i = 0; i < available.Count; i++)
            {
                var n = Normalize(available[i]);
                int dash = n.IndexOf('-');
                if (dash > 0 && n.Substring(0, dash) == code) return available[i];
            }
            return null;
        }
    }
}
