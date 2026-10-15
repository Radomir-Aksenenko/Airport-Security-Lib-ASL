using System;
using System.Collections.Generic;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Stacked notifications in the top-right corner (max 5, oldest dropped): a rounded card with a coloured
    /// accent stripe, fading in and out. Click-through (the canvas ignores raycasts). Tick only touches the
    /// live toasts and only writes alpha during the fade windows.
    /// </summary>
    internal static class UiToasts
    {
        private const int Max = 5;
        private const float FadeIn = 0.15f, FadeOut = 0.4f;

        private sealed class Toast
        {
            public GameObject Go;
            public CanvasGroup Group;
            public float Age, Life, LastAlpha = -1f;
        }

        private static readonly List<Toast> Live = new List<Toast>();
        private static GameObject _go;
        private static RectTransform _stack;

        public static int Count => Live.Count;

        public static void Show(UiOwner owner, UiTheme theme, string text, UiToastKind kind, float seconds)
        {
            try { owner.Log?.LogInfo($"[{owner.ModName}] toast ({kind}): {text}"); } catch { }
            try
            {
                Ensure();
                while (Live.Count >= Max) Remove(0);

                var rt = UiBuild.Rect("ASL_Toast", _stack);
                float r = Mathf.Min(theme.CornerRadius, 12f);
                UiBuild.Img(rt, UiShapes.Rounded(r), theme.Panel, false, true);
                UiBuild.HGroup(rt.gameObject, 14f, 0, 18, 12, 12, TextAnchor.MiddleLeft);
                var le = UiBuild.Le(rt.gameObject);
                le.preferredWidth = 420f; le.minWidth = 420f;
                var cg = rt.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cg.blocksRaycasts = false;

                UiBuild.ImgChild("ASL_ToastBorder", rt, UiShapes.Ring(r, 2f), theme.PanelBorder, false, true, out var brt);
                UiBuild.Stretch(brt);
                UiBuild.Le(brt.gameObject).ignoreLayout = true;

                UiBuild.ImgChild("ASL_ToastAccent", rt, UiShapes.Rounded(3f), UiThemeApplier.ToastColor(theme, kind), false, true, out var art);
                var ale = UiBuild.Le(art.gameObject);
                ale.preferredWidth = 6f; ale.minWidth = 6f; ale.flexibleHeight = 1f;

                var trt = UiBuild.Rect("ASL_ToastText", rt);
                var tmp = UiBuild.Text(trt, theme, text ?? string.Empty, theme.FontSize * 0.92f, theme.Text, TextAlignmentOptions.Left);
                UiBuild.Le(trt.gameObject).flexibleWidth = 1f;
                // children stretch to the card height so the accent stripe spans it
                var hl = rt.GetComponent<HorizontalLayoutGroup>();
                hl.childForceExpandHeight = true;

                Live.Add(new Toast { Go = rt.gameObject, Group = cg, Life = Mathf.Max(0.5f, seconds) });
            }
            catch (Exception ex) { owner.Report("toast", ex); }
        }

        private static void Ensure()
        {
            if (_go != null) return;
            _go = UiRuntime.NewCanvas("ASL_Toasts", 32745);
            var cg = _go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            _stack = UiBuild.Rect("ASL_ToastStack", _go.transform);
            _stack.anchorMin = new Vector2(1f, 1f);
            _stack.anchorMax = new Vector2(1f, 1f);
            _stack.pivot = new Vector2(1f, 1f);
            _stack.anchoredPosition = new Vector2(-24f, -24f);
            _stack.sizeDelta = new Vector2(420f, 0f);
            UiBuild.VGroup(_stack.gameObject, 8f, 0, 0, 0, 0, TextAnchor.UpperRight);
            var fit = _stack.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void Remove(int i)
        {
            var t = Live[i];
            Live.RemoveAt(i);
            try { if (t.Go != null) UnityEngine.Object.Destroy(t.Go); } catch { }
        }

        public static void Tick(float dt)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var t = Live[i];
                t.Age += dt;
                if (t.Age >= t.Life) { Remove(i); continue; }
                float a = 1f;
                if (t.Age < FadeIn) a = t.Age / FadeIn;
                else if (t.Life - t.Age < FadeOut) a = (t.Life - t.Age) / FadeOut;
                if (a != t.LastAlpha)
                {
                    t.LastAlpha = a;
                    try { t.Group.alpha = a; } catch { }
                }
            }
        }
    }
}
