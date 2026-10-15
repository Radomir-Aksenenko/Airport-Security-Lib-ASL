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
        }

        public IUiContainer AddTab(string title)
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
            Select(i);
            try { Changed?.Invoke(i); } catch (Exception ex) { Owner.Report("tabs Changed", ex); }
        }

        private void Select(int i)
        {
            if (i < 0 || i >= _pages.Count) return;
            _selected = i;
            for (int k = 0; k < _pages.Count; k++)
            {
                _pages[k].Go.SetActive(k == i);
                _buttons[k].SetStyle(new UiButtonStyle
                {
                    Variant = k == i ? UiVariant.Primary : UiVariant.Secondary,
                    Shape = UiShape.Rounded, Size = UiSize.Small,
                });
            }
            Host.MarkDirty();
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
