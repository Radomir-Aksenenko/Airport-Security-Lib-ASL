using System;
using System.Collections.Generic;
using ASL.Api;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>
    /// An element that holds children laid out by a Unity layout group (rows, columns, grids, groups, scroll
    /// views, tab pages and windows). Implements the whole <see cref="IUiContainer"/> builder API; the
    /// concrete look is decided by the static factories below.
    /// </summary>
    internal class UiContainer : UiElement, IUiContainer
    {
        protected readonly List<UiElement> Children = new List<UiElement>();
        public RectTransform ContentRt { get; protected set; }
        protected bool IsRow;
        internal UiScrollArea ScrollArea;

        protected UiContainer(UiHost host) : base(host) { }

        // ---- factories ------------------------------------------------------------------------

        private float Sp(float spacing) => spacing >= 0f ? spacing : Theme.Spacing;

        internal static UiContainer CreateRow(UiHost host, RectTransform parent, float spacing)
        {
            var c = new UiContainer(host) { IsRow = true };
            var rt = UiBuild.Rect("ASL_Row", parent);
            c.Init(rt);
            c.ContentRt = rt;
            UiBuild.HGroup(c.Go, spacing >= 0f ? spacing : host.Theme.Spacing, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            c.FlexInRow = 1f; c.FlexInColumn = 1f;
            return c;
        }

        internal static UiContainer CreateColumn(UiHost host, RectTransform parent, float spacing)
        {
            var c = new UiContainer(host);
            var rt = UiBuild.Rect("ASL_Column", parent);
            c.Init(rt);
            c.ContentRt = rt;
            UiBuild.VGroup(c.Go, spacing >= 0f ? spacing : host.Theme.Spacing, 0, 0, 0, 0, TextAnchor.UpperLeft);
            c.FlexInRow = 1f; c.FlexInColumn = 1f;
            return c;
        }

        internal static UiContainer CreateGrid(UiHost host, RectTransform parent, int columns, float cw, float ch, float spacing)
        {
            var c = new UiContainer(host);
            var rt = UiBuild.Rect("ASL_Grid", parent);
            c.Init(rt);
            c.ContentRt = rt;
            var g = c.Go.AddComponent<GridLayoutGroup>();
            float sp = spacing >= 0f ? spacing : host.Theme.Spacing;
            g.cellSize = new Vector2(Mathf.Max(1f, cw), Mathf.Max(1f, ch));
            g.spacing = new Vector2(sp, sp);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = Mathf.Max(1, columns);
            g.childAlignment = TextAnchor.UpperLeft;
            g.startCorner = GridLayoutGroup.Corner.UpperLeft;
            g.startAxis = GridLayoutGroup.Axis.Horizontal;
            c.FlexInRow = 0f; c.FlexInColumn = 0f;   // grid has a fixed natural width
            return c;
        }

        internal static UiContainer CreateGroup(UiHost host, RectTransform parent, string title)
        {
            var theme = host.Theme;
            var c = new UiContainer(host);
            var rt = UiBuild.Rect("ASL_Group", parent);
            c.Init(rt);
            c.ContentRt = rt;
            float r = Mathf.Min(theme.CornerRadius, 14f);
            UiBuild.Img(rt, UiShapes.Rounded(r), theme.Panel, true, true);
            var ring = UiBuild.ImgChild("ASL_Border", rt, UiShapes.Ring(r, 2f), theme.PanelBorder, false, true, out var ringRt);
            UiBuild.Stretch(ringRt);
            var ringLe = UiBuild.Le(ringRt.gameObject);
            ringLe.ignoreLayout = true;
            int pad = Mathf.RoundToInt(theme.Padding * 0.75f);
            UiBuild.VGroup(c.Go, host.Theme.Spacing, pad, pad, pad, pad, TextAnchor.UpperLeft);
            c.FlexInRow = 1f; c.FlexInColumn = 1f;
            if (!string.IsNullOrEmpty(title))
            {
                var t = c.Add(new UiText(host, rt, title, UiTextStyle.Body));
                t.Tmp.fontStyle = FontStyles.Bold;
                t.Tmp.color = theme.Accent;
            }
            return c;
        }

        internal static UiContainer CreateScroll(UiHost host, RectTransform parent, float height)
        {
            var theme = host.Theme;
            var c = new UiContainer(host);
            var rt = UiBuild.Rect("ASL_Scroll", parent);
            c.Init(rt);
            float r = Mathf.Min(theme.CornerRadius, 12f);
            UiBuild.Img(rt, UiShapes.Rounded(r), UiThemeApplier.WithAlpha(theme.InputBackground, 0.6f), true, true);
            var le = c.Le;
            le.preferredHeight = Mathf.Max(40f, height);
            le.minHeight = le.preferredHeight;
            var area = UiScrollArea.Create(rt, host, true, 8, 14, 8, 8, host.Theme.Spacing, out _);
            c.ScrollArea = area;
            c.ContentRt = area.Content;
            c.FlexInRow = 1f; c.FlexInColumn = 1f;
            return c;
        }

        /// <summary>A plain vertical page (used for tab content).</summary>
        internal static UiContainer CreatePage(UiHost host, RectTransform parent, string name)
        {
            var c = new UiContainer(host);
            var rt = UiBuild.Rect(name, parent);
            c.Init(rt);
            c.ContentRt = rt;
            UiBuild.VGroup(c.Go, host.Theme.Spacing, 0, 0, 0, 0, TextAnchor.UpperLeft);
            return c;
        }

        // ---- bookkeeping ----------------------------------------------------------------------

        protected T Add<T>(T e) where T : UiElement
        {
            Children.Add(e);
            e.Parent = this;
            if (!e.FlexLocked) e.Le.flexibleWidth = IsRow ? e.FlexInRow : e.FlexInColumn;
            Host.MarkDirty();
            return e;
        }

        internal void RemoveChild(UiElement e) { Children.Remove(e); }

        public override void MarkDead()
        {
            base.MarkDead();
            if (ScrollArea != null) ScrollArea.Dead = true;
            for (int i = 0; i < Children.Count; i++) Children[i].MarkDead();
        }

        public void Clear()
        {
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                var c = Children[i];
                if (c != null) c.Destroy();
            }
            Children.Clear();
            Host.MarkDirty();
        }

        public virtual IUiElement FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Children.Count; i++)
            {
                var c = Children[i];
                if (c.Id == id) return c;
                IUiElement deep = null;
                if (c is UiContainer cc) deep = cc.FindById(id);
                else if (c is UiTabs tabs) deep = tabs.FindById(id);
                if (deep != null) return deep;
            }
            return null;
        }

        // ---- IUiContainer ---------------------------------------------------------------------

        public IUiText AddLabel(string text, UiTextStyle style = UiTextStyle.Body) => Add(new UiText(Host, ContentRt, text, style));

        public IUiText AddHeader(string text) => AddLabel(text, UiTextStyle.Header);

        public IUiText AddLiveLabel(Func<string> text, float refreshSeconds = 0.25f)
        {
            var t = new UiText(Host, ContentRt, string.Empty, UiTextStyle.Body);
            t.MakeLive(text, refreshSeconds);
            return Add(t);
        }

        public IUiButton AddButton(string text, Action onClick, UiButtonStyle style = null) =>
            Add(new UiButton(Host, ContentRt, text, onClick, style));

        public IUiButton AddIconButton(Sprite icon, Action onClick, float size = 48f, UiShape shape = UiShape.Circle)
        {
            var st = new UiButtonStyle { Shape = shape, Width = size, Height = size, Icon = icon, IconOnly = true };
            return Add(new UiButton(Host, ContentRt, string.Empty, onClick, st));
        }

        public IUiToggle AddToggle(string label, bool initial, Action<bool> onChanged, UiToggleStyle style = UiToggleStyle.Switch) =>
            Add(new UiToggle(Host, ContentRt, label, initial, onChanged, style));

        public IUiSlider AddSlider(string label, float min, float max, float initial, Action<float> onChanged, float step = 0f, string format = "0.##") =>
            Add(new UiSlider(Host, ContentRt, label, min, max, initial, onChanged, step, format));

        public IUiInput AddInput(string placeholder, string initial = "", Action<string> onSubmit = null, Action<string> onChanged = null) =>
            Add(new UiInput(Host, ContentRt, placeholder, initial, onSubmit, onChanged));

        public IUiDropdown AddDropdown(string label, string[] options, int initial, Action<int> onChanged) =>
            Add(new UiDropdown(Host, ContentRt, label, options, initial, onChanged));

        public IUiProgress AddProgressBar(float initial = 0f, string text = null) =>
            Add(new UiProgress(Host, ContentRt, initial, text));

        public IUiImage AddImage(Sprite sprite, float width = 0f, float height = 0f) =>
            Add(new UiImage(Host, ContentRt, sprite, width, height));

        public IUiElement AddKeybind(IAslKeybind keybind) => Add(new UiKeybind(Host, ContentRt, keybind));

        public IUiElement AddSpacer(float height = 12f) => Add(new UiSpacer(Host, ContentRt, height));

        public IUiElement AddSeparator() => Add(new UiSeparator(Host, ContentRt));

        public IUiContainer AddRow(float spacing = -1f) => Add(CreateRow(Host, ContentRt, spacing));

        public IUiContainer AddColumn(float spacing = -1f) => Add(CreateColumn(Host, ContentRt, spacing));

        public IUiContainer AddGrid(int columns, float cellWidth, float cellHeight, float spacing = -1f) =>
            Add(CreateGrid(Host, ContentRt, columns, cellWidth, cellHeight, spacing));

        public IUiContainer AddScroll(float height) => Add(CreateScroll(Host, ContentRt, height));

        public IUiContainer AddGroup(string title = null) => Add(CreateGroup(Host, ContentRt, title));

        public IUiTabs AddTabs() => Add(new UiTabs(Host, ContentRt));
    }
}
