using System;
using ASL.Api;
using TMPro;
using UnityEngine;

namespace ASL
{
    /// <summary>
    /// Placeholder returned when a widget (or container) failed to build. It shows a muted
    /// "[widget unavailable]" label and implements every widget interface as a harmless no-op, so the mod's
    /// builder code keeps running (no InvalidCastException, no null reference) and the rest of the window is
    /// still built. The failure itself is logged once by <see cref="UiOwner.ReportBuildFailure"/>.
    /// </summary>
    internal sealed class UiUnavailable : UiElement,
        IUiText, IUiButton, IUiToggle, IUiSlider, IUiInput, IUiDropdown, IUiProgress, IUiImage, IUiTabs, IUiContainer
    {
        public UiUnavailable(UiHost host, RectTransform parent, string kind) : base(host)
        {
            var rt = UiBuild.Rect("ASL_Unavailable", parent);
            Init(rt);
            try
            {
                var theme = host.Theme;
                UiBuild.Text(rt, theme, "[" + (kind ?? "widget") + " unavailable]", theme.FontSize * 0.85f, theme.TextMuted, TextAlignmentOptions.Left);
            }
            catch { /* even the label failed: keep an empty, harmless rect */ }
            FlexInRow = 0f;
            FlexInColumn = 1f;
        }

        // ---- IUiText / IUiButton / IUiInput / IUiProgress (shared) ----
        public string Text { get; set; }
        public Color Color { get; set; }
        public float FontSize { get; set; }
        public UiTextAlign Align { get; set; }

        // ---- IUiButton ----
        public event Action Clicked { add { } remove { } }
        public void SetStyle(UiButtonStyle style) { }
        public void SetIcon(Sprite icon) { }

        // ---- IUiToggle ----
        bool IUiToggle.Value { get; set; }
        event Action<bool> IUiToggle.Changed { add { } remove { } }
        public string Label { get; set; }

        // ---- IUiSlider ----
        float IUiSlider.Value { get; set; }
        public float Min { get; set; }
        public float Max { get; set; }
        public float Step { get; set; }
        event Action<float> IUiSlider.Changed { add { } remove { } }

        // ---- IUiInput ----
        public string Placeholder { get; set; }
        public int CharacterLimit { get; set; }
        public bool IsFocused => false;
        public void Focus() { }
        event Action<string> IUiInput.Changed { add { } remove { } }
        public event Action<string> Submitted { add { } remove { } }

        // ---- IUiDropdown ----
        public int Index { get; set; }
        string IUiDropdown.Value => null;
        public void SetOptions(string[] options) { }
        event Action<int> IUiDropdown.Changed { add { } remove { } }

        // ---- IUiProgress ----
        float IUiProgress.Value { get; set; }
        public Color FillColor { get; set; }

        // ---- IUiImage ----
        public Sprite Sprite { get; set; }
        public bool PreserveAspect { get; set; }

        // ---- IUiTabs ----
        public IUiContainer AddTab(string title) => this;
        public int SelectedIndex { get; set; }
        event Action<int> IUiTabs.Changed { add { } remove { } }

        // ---- IUiContainer (everything added lands nowhere) ----
        public IUiText AddLabel(string text, UiTextStyle style = UiTextStyle.Body) => this;
        public IUiText AddHeader(string text) => this;
        public IUiText AddLiveLabel(Func<string> text, float refreshSeconds = 0.25f) => this;
        public IUiButton AddButton(string text, Action onClick, UiButtonStyle style = null) => this;
        public IUiButton AddIconButton(Sprite icon, Action onClick, float size = 48f, UiShape shape = UiShape.Circle) => this;
        public IUiToggle AddToggle(string label, bool initial, Action<bool> onChanged, UiToggleStyle style = UiToggleStyle.Switch) => this;
        public IUiSlider AddSlider(string label, float min, float max, float initial, Action<float> onChanged, float step = 0f, string format = "0.##") => this;
        public IUiInput AddInput(string placeholder, string initial = "", Action<string> onSubmit = null, Action<string> onChanged = null) => this;
        public IUiDropdown AddDropdown(string label, string[] options, int initial, Action<int> onChanged) => this;
        public IUiProgress AddProgressBar(float initial = 0f, string text = null) => this;
        public IUiImage AddImage(Sprite sprite, float width = 0f, float height = 0f) => this;
        public IUiElement AddKeybind(IAslKeybind keybind) => this;
        public IUiElement AddSpacer(float height = 12f) => this;
        public IUiElement AddSeparator() => this;
        public IUiContainer AddRow(float spacing = -1f) => this;
        public IUiContainer AddColumn(float spacing = -1f) => this;
        public IUiContainer AddGrid(int columns, float cellWidth, float cellHeight, float spacing = -1f) => this;
        public IUiContainer AddScroll(float height) => this;
        public IUiContainer AddGroup(string title = null) => this;
        public IUiTabs AddTabs() => this;
        public IUiElement FindById(string id) => null;
        public void Clear() { }
    }
}
