using System;
using System.Collections.Generic;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// A polling scope: everything that shares one hover region and one theme (a window, an F8 sub-page, a
    /// dropdown popup). Holds only the widgets that registered for polling, so <see cref="PollAll"/> does no
    /// work for passive elements. Lists are reused (no steady-state allocations).
    /// </summary>
    internal sealed class UiHost
    {
        public readonly UiOwner Owner;
        public UiTheme Theme;
        public UiWindow Window;            // null for pages / popups
        public RectTransform HoverRect;    // region that counts as "the pointer is over this host"
        public bool IsHover;               // set by UiRuntime each tick
        public bool Dirty;                 // layout must be rebuilt (windows act on it)
        public int Repoll;                 // >0: re-test hover state even if the pointer did not move

        public readonly List<UiElement> Pollers = new List<UiElement>();
        public readonly List<UiElement> Tipped = new List<UiElement>();
        public readonly List<UiScrollArea> Scrolls = new List<UiScrollArea>();

        public UiHost(UiOwner owner, UiTheme theme) { Owner = owner; Theme = theme; }

        public void MarkDirty() { Dirty = true; Repoll = 3; }

        public void AddPoller(UiElement e) { Pollers.Add(e); }

        /// <summary>True when hover tests should be re-run this frame.</summary>
        public bool ShouldRetest => UiRuntime.PointerChanged || Repoll > 0;

        public void PollAll()
        {
            var list = Pollers;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var e = list[i];
                if (e == null || e.Dead) { list.RemoveAt(i); continue; }
                try { e.Poll(); }
                catch (Exception ex) { Owner.Report("widget poll", ex); }
            }
            if (Repoll > 0) Repoll--;
        }

        /// <summary>Mouse-wheel scrolling: the innermost scroll area under the pointer that can still move takes it.</summary>
        public bool ScrollAt(Vector2 mouse, float wheel)
        {
            for (int i = Scrolls.Count - 1; i >= 0; i--)
            {
                var s = Scrolls[i];
                if (s == null || s.Dead) { Scrolls.RemoveAt(i); continue; }
                if (!s.Viewport.gameObject.activeInHierarchy) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(s.Viewport, mouse, null)) continue;
                if (s.TryScroll(wheel)) { Repoll = 2; return true; }
            }
            return false;
        }
    }

    /// <summary>
    /// Allocation-free hover/pressed tracking for one rect. <see cref="Update"/> returns true when the visual
    /// state changed. Hover is only re-tested when the pointer moved / clicked or the host asked for it.
    /// </summary>
    internal struct UiHoverState
    {
        public bool Hover, Pressed;

        public bool Update(UiHost host, RectTransform rt, bool enabled) => Update(host, null, rt, enabled);

        /// <summary>
        /// Same, for a widget: an inactive element never counts as hovered, and neither does a point outside
        /// the scroll viewport the element sits in (its scrolled-out part is clipped on screen).
        /// </summary>
        public bool Update(UiHost host, UiElement owner, RectTransform rt, bool enabled)
        {
            bool hover = Hover, pressed = Pressed;
            if (!host.IsHover || !enabled || !UiRuntime.PointerActive)
            {
                hover = false; pressed = false;
            }
            else
            {
                if (host.ShouldRetest)
                {
                    hover = false;
                    if (owner == null || owner.IsActiveInHierarchy())
                    {
                        hover = RectTransformUtility.RectangleContainsScreenPoint(rt, UiRuntime.Mouse, null);
                        if (hover && owner != null)
                        {
                            var clip = owner.ClipViewport;
                            if (clip != null && !RectTransformUtility.RectangleContainsScreenPoint(clip, UiRuntime.Mouse, null)) hover = false;
                        }
                    }
                }
                if (!hover) pressed = false;
                else if (UiRuntime.MouseDown) pressed = true;
                else if (!UiRuntime.MouseHeld) pressed = false;
            }
            bool changed = hover != Hover || pressed != Pressed;
            Hover = hover; Pressed = pressed;
            return changed;
        }
    }

    /// <summary>A scroll view's moving parts plus wheel handling (done by polling; the ScrollRect's own wheel is disabled).</summary>
    internal sealed class UiScrollArea
    {
        public const float WheelStep = 70f;   // canvas units per wheel notch
        public ScrollRect Rect;
        public RectTransform Viewport, Content;
        public bool Dead;

        /// <summary>Scrolls by <paramref name="wheel"/> notches (positive = up). Returns true if the view moved.</summary>
        public bool TryScroll(float wheel)
        {
            if (Rect == null || Content == null || Viewport == null) return false;
            float range = Content.rect.height - Viewport.rect.height;
            if (range <= 1f) return false;
            float cur = Rect.verticalNormalizedPosition;
            float next = Mathf.Clamp01(cur + wheel * WheelStep / range);
            if (Mathf.Approximately(cur, next)) return false;
            Rect.verticalNormalizedPosition = next;
            return true;
        }

        /// <summary>Builds viewport + content (+ optional scrollbar) under <paramref name="parent"/>.</summary>
        public static UiScrollArea Create(RectTransform parent, UiHost host, bool scrollbar, int padL, int padR, int padT, int padB, float spacing, out VerticalLayoutGroup vlg)
        {
            var theme = host.Theme;
            var area = new UiScrollArea();
            var viewport = UiBuild.Rect("ASL_Viewport", parent);
            UiBuild.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            // A transparent graphic so drags that start on empty space still scroll.
            UiBuild.Img(viewport, null, UiThemeApplier.Clear, true, false);

            var content = UiBuild.Rect("ASL_Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            vlg = UiBuild.VGroup(content.gameObject, spacing, padL, padR, padT, padB, TextAnchor.UpperLeft);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = parent.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = false;
            sr.scrollSensitivity = 0f;       // wheel is polled by UiHost.ScrollAt instead
            sr.viewport = viewport;
            sr.content = content;

            if (scrollbar)
            {
                try
                {
                    var sbRt = UiBuild.Rect("ASL_Scrollbar", parent);
                    sbRt.anchorMin = new Vector2(1f, 0f);
                    sbRt.anchorMax = new Vector2(1f, 1f);
                    sbRt.pivot = new Vector2(1f, 1f);
                    sbRt.sizeDelta = new Vector2(8f, -8f);
                    sbRt.anchoredPosition = new Vector2(-2f, -4f);
                    var track = UiBuild.Img(sbRt, UiShapes.Rounded(4f), UiThemeApplier.WithAlpha(theme.PanelBorder, 0.35f), true, true);
                    var slide = UiBuild.Rect("ASL_SlideArea", sbRt);
                    UiBuild.Stretch(slide);
                    var handle = UiBuild.Rect("ASL_Handle", slide);
                    handle.sizeDelta = new Vector2(0f, 0f);
                    var hImg = UiBuild.Img(handle, UiShapes.Rounded(4f), UiThemeApplier.WithAlpha(theme.TextMuted, 0.8f), true, true);
                    var sb = sbRt.gameObject.AddComponent<Scrollbar>();
                    sb.handleRect = handle;
                    sb.targetGraphic = hImg;
                    sb.direction = Scrollbar.Direction.BottomToTop;
                    sr.verticalScrollbar = sb;
                    sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                }
                catch (Exception ex) { host.Owner.Report("scrollbar build", ex); }
            }

            area.Rect = sr;
            area.Viewport = viewport;
            area.Content = content;
            host.Scrolls.Add(area);
            return area;
        }
    }
}
