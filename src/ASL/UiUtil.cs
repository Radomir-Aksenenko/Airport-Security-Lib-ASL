using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    internal static class UiUtil
    {
        /// <summary>
        /// Best-effort: the sprite of the game's Settings panel, so the mod menu can wear the same
        /// background. Returns null if the Settings screen isn't available yet.
        /// </summary>
        public static Sprite SettingsPanelSprite()
        {
            try
            {
                var inst = SettingsUi.Instance;
                if (inst == null) return null;

                var t = inst.contentsGo != null ? inst.contentsGo.transform : inst.transform;
                for (int up = 0; up < 3 && t != null; up++)
                {
                    var img = t.GetComponent<Image>();
                    if (img != null && img.sprite != null) return img.sprite;
                    t = t.parent;
                }
                var any = inst.GetComponentInChildren<Image>(true);
                return any != null && any.sprite != null ? any.sprite : null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Sets a cloned button's TMP label, first removing any extra script on the label object
        /// (e.g. a localizer) that would otherwise overwrite the text back to the original — which
        /// is why a relabelled clone was still showing "Settings".
        /// </summary>
        public static void SetLabel(GameObject root, string text)
        {
            if (root == null) return;
            var tmp = root.GetComponentInChildren<TMP_Text>(true);
            if (tmp == null) return;

            var labelGo = tmp.gameObject;
            var monos = labelGo.GetComponents<MonoBehaviour>();
            for (int i = 0; i < monos.Length; i++)
            {
                var m = monos[i];
                if (m == null) continue;
                if (m.TryCast<TMP_Text>() == null) UnityEngine.Object.Destroy(m);  // drop localizer / overrides
            }

            tmp.text = text;
        }

        private static Sprite _modsIcon;

        /// <summary>The custom Mods-button icon sprite (built once from the embedded PNG).</summary>
        public static Sprite ModsIcon()
        {
            if (_modsIcon != null) return _modsIcon;
            try { _modsIcon = SpriteFromPng(Convert.FromBase64String(IconAsset.ModsIconPngBase64)); }
            catch { _modsIcon = null; }
            return _modsIcon;
        }

        /// <summary>Decodes a PNG (managed) and builds a readable RGBA32 sprite — the marshal-safe path.</summary>
        public static Sprite SpriteFromPng(byte[] png)
        {
            int w, h;
            var rgba = PngDecoder.Decode(png, out w, out h);     // top-down RGBA32

            int stride = w * 4;
            var px = new Il2CppStructArray<byte>(rgba.Length);
            for (int row = 0; row < h; row++)
            {
                int src = (h - 1 - row) * stride, dst = row * stride;   // flip to Unity bottom-up
                for (int b = 0; b < stride; b++) px[dst + b] = rgba[src + b];
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.LoadRawTextureData(px);
            tex.Apply(false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            var spr = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
            spr.hideFlags = HideFlags.HideAndDontSave;
            return spr;
        }
    }
}
