using System;
using System.Collections.Generic;
using ASL.Api;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Finds the game's TMP font asset ONCE and hands it to every toolkit text. Sources, in order: the
    /// captured native button template (see <see cref="NativeMenu.SetTemplate"/>), then any live TMP_Text
    /// found on a throttled retry (only while some text is still waiting for the font), else TMP's default.
    /// Texts created before the font is known are remembered and patched when it appears.
    /// </summary>
    internal static class UiFonts
    {
        private static TMP_FontAsset _game;
        private static readonly List<TMP_Text> Pending = new List<TMP_Text>();
        private static float _nextTry;
        private static int _attempts;

        public static bool NeedsTick => _game == null && Pending.Count > 0 && _attempts < 12;

        /// <summary>Called when the game's button template is captured.</summary>
        public static void NoteTemplate(GameObject template)
        {
            try
            {
                if (_game != null || template == null) return;
                var t = template.GetComponentInChildren<TMP_Text>(true);
                if (t != null && t.font != null) { _game = t.font; ApplyPending(); }
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
                if (theme == null || !theme.UseGameFont) { var d = Default(); if (d != null) t.font = d; return; }
                if (_game != null) { t.font = _game; return; }
                var def = Default();
                if (def != null) t.font = def;
                if (_attempts < 12) Pending.Add(t);
            }
            catch (Exception ex) { Warn("font assign failed: " + ex.Message); }
        }

        /// <summary>Throttled retry while texts are waiting for the game font.</summary>
        public static void Tick(float now)
        {
            if (!NeedsTick || now < _nextTry) return;
            _nextTry = now + 3f;
            _attempts++;
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
                        ApplyPending();
                        return;
                    }
            }
            catch (Exception ex) { Warn("live font search failed: " + ex.Message); }
            if (_attempts >= 12) { Pending.Clear(); Warn("game font not found; using TMP default."); }
        }

        private static void ApplyPending()
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                try { if (Pending[i] != null) Pending[i].font = _game; } catch { }
            }
            Pending.Clear();
        }

        private static void Warn(string m) { try { AslPlugin.Logger?.LogWarning("[ui] " + m); } catch { } }
    }
}
