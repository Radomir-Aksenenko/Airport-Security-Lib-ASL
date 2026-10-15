using System;
using ASL.Api;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// Hosts a toolkit-built sub-page inside the F8 menu (<c>IModMenu.AddPage</c>): a scrollable column placed
    /// under the menu's header rows, in the F8 canvas, polled by <see cref="UiRuntime"/> while the menu is
    /// visible. Uses the <see cref="UiTheme.Game"/> theme so primary buttons match the menu's native look.
    /// </summary>
    internal static class UiPages
    {
        private static GameObject _root;

        /// <summary>Builds the page under <paramref name="parent"/> (the F8 panel). Replaces any previous page.</summary>
        public static void Show(RectTransform parent, string modName, Action<IUiContainer> build, float top, float width)
        {
            Clear();
            if (build == null) return;
            var owner = new UiOwner(modName, modName, AslPlugin.Logger);
            try
            {
                var host = new UiHost(owner, UiTheme.Game);
                var rt = UiBuild.Rect("ASL_Page", parent);
                _root = rt.gameObject;
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(-width * 0.5f, 28f);
                rt.offsetMax = new Vector2(width * 0.5f, -top);
                host.HoverRect = rt;

                var area = UiScrollArea.Create(rt, host, true, 6, 16, 6, 6, host.Theme.Spacing, out _);
                var page = new PageContainer(host, area);
                owner.Fire(() => build(page), "menu page build");
                host.MarkDirty();
                UiRuntime.PageHost = host;
                UiRuntime.EnsureEventSystem(true);
            }
            catch (Exception ex) { owner.Report("menu page", ex); }
        }

        /// <summary>Destroys the current page (the menu rebuilt, closed or navigated away).</summary>
        public static void Clear()
        {
            var host = UiRuntime.PageHost;
            UiRuntime.PageHost = null;
            if (host != null) for (int i = 0; i < host.Pollers.Count; i++) host.Pollers[i].MarkDead();
            if (host != null) for (int i = 0; i < host.Scrolls.Count; i++) host.Scrolls[i].Dead = true;
            try { if (_root != null) UnityEngine.Object.Destroy(_root); } catch { }
            _root = null;
        }

        /// <summary>The container handed to the page builder (content of the page's scroll view).</summary>
        private sealed class PageContainer : UiContainer
        {
            public PageContainer(UiHost host, UiScrollArea area) : base(host)
            {
                Init(area.Viewport.parent as RectTransform);
                ContentRt = area.Content;
                ScrollArea = area;
            }
        }
    }
}
