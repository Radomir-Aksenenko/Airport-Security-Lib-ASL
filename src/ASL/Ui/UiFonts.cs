using System;
using System.Collections.Generic;
using ASL.Api;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Finds the game's TMP font asset and hands it to every toolkit text. Sources, in order of preference: the
    /// captured native button template (see <see cref="NativeMenu.SetTemplate"/>), then any live TMP_Text found on a
    /// throttled retry (only while some text is still waiting for the font), else TMP's default font.
    /// Texts created before the font is known are remembered and patched when it appears; if the template turns up
    /// AFTER a fallback font was used, every existing toolkit text / input field is switched to it once. If neither a
    /// game font nor TMP's default font exists the texts would be invisible: the search then keeps retrying on a slow
    /// throttle (forever) and says so once in the log.
    /// </summary>
    internal static class UiFonts
    {
        private const int FastAttempts = 12;     // 3 s apart, then the slow throttle below

        private static TMP_FontAsset _game;
        private static bool _fromTemplate;
        private static bool _defaultMissing, _warnedNoFont;
        private static readonly List<TMP_Text> Pending = new List<TMP_Text>();
        private static readonly List<TMP_InputField> PendingInputs = new List<TMP_InputField>();
        private static float _nextTry;
        private static int _attempts;

        public static bool NeedsTick =>
            _game == null && (Pending.Count > 0 || PendingInputs.Count > 0) && (_attempts < FastAttempts || _defaultMissing);

        /// <summary>Called when the game's button template is captured.</summary>
        public static void NoteTemplate(GameObject template)
        {
            try
            {
                if (template == null) return;
                var t = template.GetComponentInChildren<TMP_Text>(true);
                if (t == null || t.font == null) return;
                if (_game == null) { _game = t.font; _fromTemplate = true; ApplyPending(); }
                else if (!_fromTemplate && _game != t.font)
                {
                    // A live-search font was already in use: the template's is the one the game's own menus use.
                    _game = t.font; _fromTemplate = true;
                    ReapplyToExisting();
                }
                else _fromTemplate = true;
            }
            catch (Exception ex) { Warn("template font capture failed: " + ex.Message); }
        }

        private static TMP_FontAsset Default()
        {
            try { return TMP_Settings.defaultFontAsset; } catch { return null; }
        }

        /// <summary>Assigns the right font to a freshly created text.</summary>
        public static void Register(TMP_Text t, UiTheme theme)
        {
            try
            {
                bool wantsGame = theme != null && theme.UseGameFont;
                if (!wantsGame)
                {
                    var d = Default();
                    if (d != null) { t.font = d; return; }
                    // No TMP default: the game's font beats an invisible label.
                    if (_game != null) { t.font = _game; return; }
                    NoteNoFont();
                    Pending.Add(t);
                    return;
                }
                if (_game != null) { t.font = _game; return; }
                var def = Default();
                if (def != null) t.font = def; else NoteNoFont();
                if (_attempts < FastAttempts || def == null) Pending.Add(t);   // patched when the game font shows up
            }
            catch (Exception ex) { Warn("font assign failed: " + ex.Message); }
        }

        /// <summary>A TMP_InputField gets the same font (and point size) as its text; patched later if the font was not known yet.</summary>
        public static void RegisterInput(TMP_InputField f)
        {
            try
            {
                if (f == null) return;
                Apply(f);                                   // the text's current font (game / TMP default) right away
                if (_game == null) PendingInputs.Add(f);    // switched to the game font when it is found
            }
            catch (Exception ex) { Warn("input font assign failed: " + ex.Message); }
        }

        private static void Apply(TMP_InputField f)
        {
            var text = f.textComponent;
            var font = _game != null ? _game : (text != null ? text.font : null);
            if (font != null) f.fontAsset = font;
            if (text != null) f.pointSize = text.fontSize;
        }

        private static void NoteNoFont()
        {
            _defaultMissing = true;
            if (_warnedNoFont) return;
            _warnedNoFont = true;
            Warn("no TMP font available yet (no game font captured, TMP default font is null); toolkit text stays invisible until one is found - searching again periodically.");
        }

        /// <summary>Throttled retry while texts are waiting for the game font.</summary>
        public static void Tick(float now)
        {
            if (!NeedsTick || now < _nextTry) return;
            _attempts++;
            _nextTry = now + (_attempts < FastAttempts ? 3f : 15f);
            try
            {
                var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_Text>());
                if (all != null)
                    for (int i = 0; i < all.Length; i++)
                    {
                        var t = all[i] != null ? all[i].TryCast<TMP_Text>() : null;
                        if (t == null || t.font == null) continue;
                        if (UiRuntime.IsOurs(t.transform)) continue;
                        _game = t.font;
                        _fromTemplate = false;
                        ApplyPending();
                        return;
                    }
            }
            catch (Exception ex) { Warn("live font search failed: " + ex.Message); }
            if (_attempts >= FastAttempts && !_defaultMissing)
            {
                Pending.Clear(); PendingInputs.Clear();
                Warn("game font not found; using TMP default.");
            }
            else PrunePending();
        }

        private static void PrunePending()
        {
            for (int i = Pending.Count - 1; i >= 0; i--) if (Pending[i] == null) Pending.RemoveAt(i);
            for (int i = PendingInputs.Count - 1; i >= 0; i--) if (PendingInputs[i] == null) PendingInputs.RemoveAt(i);
        }

        private static void ApplyPending()
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                try { if (Pending[i] != null) Pending[i].font = _game; } catch { }
            }
            Pending.Clear();
            for (int i = 0; i < PendingInputs.Count; i++)
            {
                try { if (PendingInputs[i] != null) Apply(PendingInputs[i]); } catch { }
            }
            PendingInputs.Clear();
            _defaultMissing = false;
        }

        // The template font arrived after another font was already in use: switch every live toolkit text / input once.
        private static void ReapplyToExisting()
        {
            try
            {
                var texts = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_Text>());
                if (texts != null)
                    for (int i = 0; i < texts.Length; i++)
                    {
                        var t = texts[i] != null ? texts[i].TryCast<TMP_Text>() : null;
                        if (t == null || !UiRuntime.IsOurs(t.transform) || t.font == _game) continue;
                        try { t.font = _game; } catch { }
                    }
                var inputs = Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_InputField>());
                if (inputs != null)
                    for (int i = 0; i < inputs.Length; i++)
                    {
                        var f = inputs[i] != null ? inputs[i].TryCast<TMP_InputField>() : null;
                        if (f == null || !UiRuntime.IsOurs(f.transform)) continue;
                        try { Apply(f); } catch { }
                    }
            }
            catch (Exception ex) { Warn("re-applying the game font failed: " + ex.Message); }
            ApplyPending();
        }

        private static void Warn(string m) { try { AslPlugin.Logger?.LogWarning("[ui] " + m); } catch { } }
    }
}
