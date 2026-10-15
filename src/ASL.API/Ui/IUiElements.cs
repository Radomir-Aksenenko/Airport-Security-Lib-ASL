using System;
using UnityEngine;

namespace ASL.Api
{
    /// <summary>
    /// Base of every widget in the UI toolkit. Elements are created through an <see cref="IUiContainer"/>
    /// (<c>window.AddButton(...)</c>, <c>row.AddLabel(...)</c>, ...) and returned so you can keep a handle
    /// and change them later. Setters that return <see cref="IUiElement"/> return the element itself,
    /// so calls chain.
    /// </summary>
    public interface IUiElement
    {
        /// <summary>Optional id, used by <see cref="IUiContainer.FindById"/> and JSON layouts.</summary>
        string Id { get; set; }

        /// <summary>Shown/hidden. A hidden element takes no layout space.</summary>
        bool Visible { get; set; }

        /// <summary>When false the element is greyed out and ignores input.</summary>
        bool Interactable { get; set; }

        /// <summary>Tooltip text shown after the pointer rests on the element for ~0.5 s. Null/empty = none.</summary>
        string Tooltip { get; set; }

        /// <summary>
        /// Fix the element's size in pixels. A value &lt;= 0 means "auto / stretch" for that axis.
        /// Returns this element. <code>row.AddButton("OK", Ok).SetSize(120, 40);</code>
        /// </summary>
        IUiElement SetSize(float width, float height);

        /// <summary>Set the layout flexible width (share of leftover horizontal space). Returns this element.</summary>
        IUiElement SetFlexible(float flexibleWidth);

        /// <summary>Escape hatch: the underlying Unity GameObject, for advanced customisation.</summary>
        GameObject GameObject { get; }

        /// <summary>Remove the element from its parent and destroy it.</summary>
        void Destroy();
    }

    /// <summary>A text label.</summary>
    public interface IUiText : IUiElement
    {
        /// <summary>The displayed text.</summary>
        string Text { get; set; }
        /// <summary>Text colour.</summary>
        Color Color { get; set; }
        /// <summary>Font size in pixels.</summary>
        float FontSize { get; set; }
        /// <summary>Horizontal alignment.</summary>
        UiTextAlign Align { get; set; }
    }

    /// <summary>A clickable button.</summary>
    public interface IUiButton : IUiElement
    {
        /// <summary>The button caption.</summary>
        string Text { get; set; }
        /// <summary>Raised when the button is clicked (in addition to the callback passed on creation).</summary>
        event Action Clicked;
        /// <summary>Re-style the button (shape, size, colour role).</summary>
        void SetStyle(UiButtonStyle style);
        /// <summary>Show an icon on the button.</summary>
        void SetIcon(Sprite icon);
    }

    /// <summary>An on/off toggle (switch, checkbox or button).</summary>
    public interface IUiToggle : IUiElement
    {
        /// <summary>Current state. Setting it does not raise <see cref="Changed"/>.</summary>
        bool Value { get; set; }
        /// <summary>Raised with the new value when the player flips the toggle.</summary>
        event Action<bool> Changed;
        /// <summary>The caption next to the toggle.</summary>
        string Label { get; set; }
    }

    /// <summary>A horizontal slider.</summary>
    public interface IUiSlider : IUiElement
    {
        /// <summary>Current value (clamped to Min..Max). Setting it does not raise <see cref="Changed"/>.</summary>
        float Value { get; set; }
        /// <summary>Lower bound.</summary>
        float Min { get; set; }
        /// <summary>Upper bound.</summary>
        float Max { get; set; }
        /// <summary>Snap increment; 0 = continuous.</summary>
        float Step { get; set; }
        /// <summary>Raised with the new value when the player moves the slider.</summary>
        event Action<float> Changed;
    }

    /// <summary>A single-line text input.</summary>
    public interface IUiInput : IUiElement
    {
        /// <summary>The current text. Setting it does not raise <see cref="Changed"/>.</summary>
        string Text { get; set; }
        /// <summary>Grey hint text shown while empty.</summary>
        string Placeholder { get; set; }
        /// <summary>Maximum characters; 0 = unlimited.</summary>
        int CharacterLimit { get; set; }
        /// <summary>True while this field has keyboard focus. (While any ASL field is focused, mod keybinds and F8 are suppressed.)</summary>
        bool IsFocused { get; }
        /// <summary>Give this field keyboard focus.</summary>
        void Focus();
        /// <summary>Raised with the new text whenever the player edits it.</summary>
        event Action<string> Changed;
        /// <summary>Raised with the text when the player presses Enter.</summary>
        event Action<string> Submitted;
    }

    /// <summary>A drop-down list.</summary>
    public interface IUiDropdown : IUiElement
    {
        /// <summary>Selected option index. Setting it does not raise <see cref="Changed"/>.</summary>
        int Index { get; set; }
        /// <summary>Text of the selected option (null if none).</summary>
        string Value { get; }
        /// <summary>Replace the option list.</summary>
        void SetOptions(string[] options);
        /// <summary>Raised with the new index when the player picks an option.</summary>
        event Action<int> Changed;
    }

    /// <summary>A progress bar.</summary>
    public interface IUiProgress : IUiElement
    {
        /// <summary>Progress 0..1.</summary>
        float Value { get; set; }
        /// <summary>Fill colour.</summary>
        Color FillColor { get; set; }
        /// <summary>Optional text drawn over the bar (e.g. "42%").</summary>
        string Text { get; set; }
    }

    /// <summary>An image.</summary>
    public interface IUiImage : IUiElement
    {
        /// <summary>The sprite shown.</summary>
        Sprite Sprite { get; set; }
        /// <summary>Tint colour (white = unchanged).</summary>
        Color Color { get; set; }
        /// <summary>Keep the sprite's aspect ratio when sized.</summary>
        bool PreserveAspect { get; set; }
    }

    /// <summary>A tab strip with one content container per tab.</summary>
    public interface IUiTabs : IUiElement
    {
        /// <summary>Add a tab and get the container its content goes into.</summary>
        IUiContainer AddTab(string title);
        /// <summary>The selected tab index. Setting it does not raise <see cref="Changed"/>.</summary>
        int SelectedIndex { get; set; }
        /// <summary>Raised with the new index when the player switches tab.</summary>
        event Action<int> Changed;
    }

    /// <summary>
    /// Something that can hold child elements: a window, row, column, grid, scroll view, group or tab.
    /// All <c>Add*</c> callbacks are wrapped — an exception in your callback is logged with your mod name
    /// and never propagated into the game.
    /// <code>
    /// var win = ctx.Ui.CreateWindow("main", "Hello");
    /// win.AddHeader("Settings");
    /// var row = win.AddRow();
    /// row.AddButton("Yes", () => ctx.Log.Info("yes"), UiButtonStyle.Success);
    /// row.AddButton("No",  () => win.Close(),         UiButtonStyle.Danger);
    /// </code>
    /// </summary>
    public interface IUiContainer : IUiElement
    {
        /// <summary>Add a text label.</summary>
        IUiText AddLabel(string text, UiTextStyle style = UiTextStyle.Body);

        /// <summary>Add a header label (same as <c>AddLabel(text, UiTextStyle.Header)</c>).</summary>
        IUiText AddHeader(string text);

        /// <summary>Add a label whose text is re-evaluated from <paramref name="text"/> while visible, at most every <paramref name="refreshSeconds"/>.</summary>
        IUiText AddLiveLabel(Func<string> text, float refreshSeconds = 0.25f);

        /// <summary>Add a button. <paramref name="style"/> null = the theme default (Primary, Rounded, Medium).</summary>
        IUiButton AddButton(string text, Action onClick, UiButtonStyle style = null);

        /// <summary>Add an icon-only button of <paramref name="size"/> pixels in the given <paramref name="shape"/>.</summary>
        IUiButton AddIconButton(Sprite icon, Action onClick, float size = 48f, UiShape shape = UiShape.Circle);

        /// <summary>Add a toggle with a caption. <paramref name="onChanged"/> receives the new value.</summary>
        IUiToggle AddToggle(string label, bool initial, Action<bool> onChanged, UiToggleStyle style = UiToggleStyle.Switch);

        /// <summary>
        /// Add a labelled slider. <paramref name="step"/> 0 = continuous; <paramref name="format"/> is a
        /// .NET float format for the value readout (e.g. "0.##", "0%").
        /// </summary>
        IUiSlider AddSlider(string label, float min, float max, float initial, Action<float> onChanged, float step = 0f, string format = "0.##");

        /// <summary>Add a text input. <paramref name="onSubmit"/> fires on Enter, <paramref name="onChanged"/> on every edit.</summary>
        IUiInput AddInput(string placeholder, string initial = "", Action<string> onSubmit = null, Action<string> onChanged = null);

        /// <summary>Add a labelled drop-down.</summary>
        IUiDropdown AddDropdown(string label, string[] options, int initial, Action<int> onChanged);

        /// <summary>Add a progress bar (0..1) with optional overlay text.</summary>
        IUiProgress AddProgressBar(float initial = 0f, string text = null);

        /// <summary>Add an image. A width/height of 0 uses the sprite's size.</summary>
        IUiImage AddImage(Sprite sprite, float width = 0f, float height = 0f);

        /// <summary>Add a click-to-rebind row for a keybind registered with <c>ctx.Input.RegisterKey</c>.</summary>
        IUiElement AddKeybind(IAslKeybind keybind);

        /// <summary>Add empty vertical space.</summary>
        IUiElement AddSpacer(float height = 12f);

        /// <summary>Add a thin horizontal divider line.</summary>
        IUiElement AddSeparator();

        /// <summary>Add a horizontal layout. <paramref name="spacing"/> -1 = theme spacing.</summary>
        IUiContainer AddRow(float spacing = -1f);

        /// <summary>Add a vertical layout. <paramref name="spacing"/> -1 = theme spacing.</summary>
        IUiContainer AddColumn(float spacing = -1f);

        /// <summary>Add a grid layout with fixed cell size. <paramref name="spacing"/> -1 = theme spacing.</summary>
        IUiContainer AddGrid(int columns, float cellWidth, float cellHeight, float spacing = -1f);

        /// <summary>Add a vertical scroll view of the given pixel height (mouse wheel supported).</summary>
        IUiContainer AddScroll(float height);

        /// <summary>Add a boxed panel with an optional title.</summary>
        IUiContainer AddGroup(string title = null);

        /// <summary>Add a tab strip.</summary>
        IUiTabs AddTabs();

        /// <summary>Find a descendant by its <see cref="IUiElement.Id"/>; null if none.</summary>
        IUiElement FindById(string id);

        /// <summary>Destroy all children (keeps the container itself).</summary>
        void Clear();
    }

    /// <summary>
    /// A top-level window or HUD created by <see cref="IAslUi.CreateWindow"/> / <see cref="IAslUi.CreateHud"/>.
    /// Windows start hidden — call <see cref="Open"/>.
    /// </summary>
    public interface IUiWindow : IUiContainer
    {
        /// <summary>The id you passed on creation (without the internal mod prefix).</summary>
        string WindowId { get; }

        /// <summary>The title bar text.</summary>
        string Title { get; set; }

        /// <summary>True while the window is showing.</summary>
        bool IsOpen { get; }

        /// <summary>Show the window (bringing it to front).</summary>
        void Open();

        /// <summary>Hide the window.</summary>
        void Close();

        /// <summary>Open if closed, close if open.</summary>
        void Toggle();

        /// <summary>Raised after the window opens.</summary>
        event Action Opened;

        /// <summary>Raised after the window closes.</summary>
        event Action Closed;

        /// <summary>Move the window to an anchor with an optional pixel offset.</summary>
        void SetPosition(UiAnchor anchor, float offsetX = 0f, float offsetY = 0f);

        /// <summary>Pressing <paramref name="key"/> toggles the window (ignored while the player is typing in a field).</summary>
        void BindToggleKey(IAslKeybind key);

        /// <summary>Draw this window above the other windows.</summary>
        void BringToFront();
    }
}
