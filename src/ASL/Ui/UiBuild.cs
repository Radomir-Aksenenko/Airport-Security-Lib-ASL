using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>Tiny helpers for creating uGUI objects from code (RectTransforms, Images, TMP texts, layout groups).</summary>
    internal static class UiBuild
    {
        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            if (rt == null) rt = go.GetComponent<RectTransform>();
            if (parent != null) rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        public static void Stretch(RectTransform rt, float l = 0f, float b = 0f, float r = 0f, float t = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        public static Image Img(RectTransform rt, Sprite sprite, Color color, bool raycast, bool sliced)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            if (sprite != null && sliced) img.type = Image.Type.Sliced;
            return img;
        }

        public static Image ImgChild(string name, Transform parent, Sprite sprite, Color color, bool raycast, bool sliced, out RectTransform rt)
        {
            rt = Rect(name, parent);
            return Img(rt, sprite, color, raycast, sliced);
        }

        public static TextMeshProUGUI Text(RectTransform rt, UiTheme theme, string text, float size, Color color, TextAlignmentOptions align)
        {
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            UiFonts.Register(tmp, theme);
            tmp.text = text ?? string.Empty;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            tmp.richText = true;
            return tmp;
        }

        public static HorizontalLayoutGroup HGroup(GameObject go, float spacing, int l, int r, int t, int b, TextAnchor align)
        {
            var g = go.AddComponent<HorizontalLayoutGroup>();
            Setup(g, spacing, l, r, t, b, align);
            return g;
        }

        public static VerticalLayoutGroup VGroup(GameObject go, float spacing, int l, int r, int t, int b, TextAnchor align)
        {
            var g = go.AddComponent<VerticalLayoutGroup>();
            Setup(g, spacing, l, r, t, b, align);
            return g;
        }

        private static void Setup(HorizontalOrVerticalLayoutGroup g, float spacing, int l, int r, int t, int b, TextAnchor align)
        {
            g.spacing = spacing;
            g.padding = new RectOffset(l, r, t, b);
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            g.childScaleWidth = false;
            g.childScaleHeight = false;
        }

        public static LayoutElement Le(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            return le != null ? le : go.AddComponent<LayoutElement>();
        }

        public static TextAlignmentOptions ToTmp(UiTextAlign a)
        {
            switch (a)
            {
                case UiTextAlign.Center: return TextAlignmentOptions.Center;
                case UiTextAlign.Right: return TextAlignmentOptions.Right;
                default: return TextAlignmentOptions.Left;
            }
        }
    }
}
