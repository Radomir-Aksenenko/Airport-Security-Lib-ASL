using System;
using System.Collections.Generic;
using ASL.Api;
using UnityEngine;
using UnityEngine.UI;

namespace ASL
{
    /// <summary>A tab strip (toolkit buttons) above stacked pages; only the selected page is active.</summary>
    internal sealed class UiTabs : UiElement, IUiTabs
    {
        private readonly RectTransform _headerRt, _pagesRt;
        private readonly List<UiButton> _buttons = new List<UiButton>();
        private readonly List<UiContainer> _pages = new List<UiContainer>();
        private int _selected = -1;
        private int _styledSel = -1;   // which button currently carries the "selected" look

        public event Action<int> Changed;

        public UiTabs(UiHost host, RectTransform parent) : base(host)
        {
            var rt = UiBuild.Rect("ASL_Tabs", parent);
            Init(rt);
            UiBuild.VGroup(Go, host.Theme.Spacing, 0, 0, 0, 0, TextAnchor.UpperLeft);

            _headerRt = UiBuild.Rect("ASL_TabHeader", rt);
            UiBuild.HGroup(_headerRt.gameObject, 6f, 0, 0, 0, 0, TextAnchor.MiddleLeft);
            var sep = UiBuild.Rect("ASL_TabLine", rt);
            UiBuild.Img(sep, null, UiThemeApplier.WithAlpha(host.Theme.PanelBorder, 0.9f), false, false);
            var sle = UiBuild.Le(sep.gameObject); sle.preferredHeight = 2f; sle.minHeight = 2f; sle.flexibleWidth = 1f;

            _pagesRt = UiBuild.Rect("ASL_TabPages", rt);
            UiBuild.VGroup(_pagesRt.gameObject, 0f, 0, 0, 4, 0, TextAnchor.UpperLeft);
            UiBuild.Le(_pagesRt.gameObject).flexibleWidth = 1f;
            FlexInRow = 1f;
            FlexInColumn = 1f;
            NeedPoll();
        }

        public IUiContainer AddTab(string title)
        {
            int cc = _pagesRt.childCount, hc = _headerRt.childCount, pc = Host.Pollers.Count;
            int pages = _pages.Count, buttons = _buttons.Count;
            try { return AddTabCore(title); }
            catch (Exception ex)
            {
                // Undo the half-added tab and hand back a placeholder container, so the mod's builder keeps going.
                Owner.ReportBuildFailure("tab", ex);
                try
                {
                    while (_pages.Count > pages) { _pages[_pages.Count - 1].MarkDead(); _pages.RemoveAt(_pages.Count - 1); }
                    while (_buttons.Count > buttons) { _buttons[_buttons.Count - 1].MarkDead(); _buttons.RemoveAt(_buttons.Count - 1); }
                    for (int i = Host.Pollers.Count - 1; i >= pc; i--) Host.Pollers[i].MarkDead();
                    if (Host.Pollers.Count > pc) Host.Pollers.RemoveRange(pc, Host.Pollers.Count - pc);
                    for (int i = _pagesRt.childCount - 1; i >= cc; i--) DropObject(_pagesRt.GetChild(i).gameObject);
                    for (int i = _headerRt.childCount - 1; i >= hc; i--) DropObject(_headerRt.GetChild(i).gameObject);
                }
                catch { /* best effort */ }
                return new UiUnavailable(Host, _pagesRt, "tab");
            }
        }

        private static void DropObject(GameObject go) { go.SetActive(false); UnityEngine.Object.Destroy(go); }

        private IUiContainer AddTabCore(string title)
        {
            int index = _pages.Count;
            var page = UiContainer.CreatePage(Host, _pagesRt, "ASL_TabPage");
            page.Le.flexibleWidth = 1f;
            page.Go.SetActive(false);
            _pages.Add(page);
            page.Parent = null;

            var btn = new UiButton(Host, _headerRt, title ?? ("Tab " + (index + 1)), () => SelectUser(index),
                new UiButtonStyle { Variant = UiVariant.Secondary, Shape = UiShape.Rounded, Size = UiSize.Small });
            _buttons.Add(btn);
            if (_selected < 0) Select(0);
            Host.MarkDirty();
            return page;
        }

        public int SelectedIndex
        {
            get => _selected;
            set => Select(value);
        }

        private void SelectUser(int i)
        {
            if (i == _selected) return;
            Select(i, deferStyle: true);
            try { Changed?.Invoke(i); } catch (Exception ex) { Owner.Report("tabs Changed", ex); }
        }

        private void Select(int i) => Select(i, false);

        // Only the previously selected and the newly selected page / button change. A restyle can swap a button
        // between the native clone and the procedural look (a rebuild of its GameObject), which must not happen
        // inside that button's own onClick - so user clicks restyle on the next poll instead.
        private void Select(int i, bool deferStyle)
        {
            if (i < 0 || i >= _pages.Count) return;
            int prev = _selected;
            if (prev == i) return;
            _selected = i;
            if (prev >= 0 && prev < _pages.Count) _pages[prev].Go.SetActive(false);
            _pages[i].Go.SetActive(true);
            if (!deferStyle) ApplyStyles();
            Host.MarkDirty();
        }

        private void Restyle(int k)
        {
            if (k < 0 || k >= _buttons.Count || _buttons[k].Dead) return;
            _buttons[k].SetStyle(new UiButtonStyle
            {
                Variant = k == _selected ? UiVariant.Primary : UiVariant.Secondary,
                Shape = UiShape.Rounded, Size = UiSize.Small,
            });
        }

        private void ApplyStyles()
        {
            int old = _styledSel;
            _styledSel = _selected;
            if (old >= 0) Restyle(old);
            Restyle(_selected);
        }

        public override void Poll()
        {
            if (_styledSel != _selected) ApplyStyles();   // deferred restyle after a user click
        }

        public IUiElement FindById(string id)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                var f = _pages[i].FindById(id);
                if (f != null) return f;
            }
            return null;
        }

        public override void MarkDead()
        {
            base.MarkDead();
            for (int i = 0; i < _pages.Count; i++) _pages[i].MarkDead();
            for (int i = 0; i < _buttons.Count; i++) _buttons[i].MarkDead();
        }
    }
}
