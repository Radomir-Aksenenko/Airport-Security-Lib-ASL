using System;

namespace ASL.Api
{
    /// <summary>
    /// On-screen UI for mods, reached through <c>ctx.Ui</c>. Two layers:
    /// <list type="bullet">
    /// <item><b>Announce</b> — the game's own announcement banner, so messages look native.</item>
    /// <item><b>UI toolkit</b> — windows, HUDs, toasts and dialogs with buttons, toggles, sliders, inputs,
    /// tabs, scroll views and more, themed and drawn by ASL (no Unity UI knowledge needed).</item>
    /// </list>
    /// <code>
    /// var win = ctx.Ui.CreateWindow("main", "My Mod", new UiWindowOptions { Width = 520 });
    /// win.AddHeader("Hello");
    /// win.AddButton("Say hi", () => ctx.Ui.Toast("Hi!", UiToastKind.Success), UiButtonStyle.Pill(UiVariant.Success));
    /// win.BindToggleKey(ctx.Input.RegisterKey("window", "Open window", UnityEngine.KeyCode.F6));
    /// </code>
    /// Window ids are namespaced per mod, so two mods can both use <c>"main"</c>.
    /// </summary>
    public interface IAslUi
    {
        /// <summary>
        /// Show <paramref name="text"/> on the game's on-screen announcement banner (the same widget the
        /// game uses for its own messages) for <paramref name="seconds"/>. Safe to call before a level
        /// loads — it logs instead if the banner isn't up yet.
        /// </summary>
        void Announce(string text, float seconds = 2.5f);

        /// <summary>
        /// Create a window. It is hidden until <see cref="IUiWindow.Open"/> is called. Creating a window
        /// with an id this mod already used returns a new window replacing the old one.
        /// </summary>
        /// <param name="id">Window id, unique within your mod.</param>
        /// <param name="title">Title bar text.</param>
        /// <param name="options">Size, anchor, behaviour; null = defaults.</param>
        IUiWindow CreateWindow(string id, string title, UiWindowOptions options = null);

        /// <summary>
        /// Create a HUD: a visible on-screen overlay with no title bar that does not capture the cursor and
        /// is click-through by default. Use it for counters, status lines and minimaps.
        /// </summary>
        /// <param name="id">HUD id, unique within your mod.</param>
        /// <param name="anchor">Screen anchor.</param>
        /// <param name="options">Optional overrides; null = defaults suited to a HUD.</param>
        IUiWindow CreateHud(string id, UiAnchor anchor, UiWindowOptions options = null);

        /// <summary>This mod's window or HUD with the given id, or null.</summary>
        IUiWindow GetWindow(string id);

        /// <summary>Show a short notification in the stacked corner toasts (also logged).</summary>
        /// <param name="text">The message.</param>
        /// <param name="kind">Colour/severity.</param>
        /// <param name="seconds">How long it stays before fading.</param>
        void Toast(string text, UiToastKind kind = UiToastKind.Info, float seconds = 3f);

        /// <summary>
        /// Show a modal yes/no dialog. Enter = yes, Escape = no. Exactly one of the callbacks runs.
        /// </summary>
        void Confirm(string title, string message, Action onYes, Action onNo = null, string yesText = "Yes", string noText = "No");

        /// <summary>
        /// Show a modal dialog with a text field. <paramref name="onSubmit"/> receives the text on
        /// Enter/OK; <paramref name="onCancel"/> runs on Escape/Cancel.
        /// </summary>
        void Prompt(string title, string message, string initial, Action<string> onSubmit, Action onCancel = null);

        /// <summary>Show a modal message with an OK button.</summary>
        void Alert(string title, string message, Action onClose = null);

        /// <summary>
        /// Build a window from a declarative JSON layout file in your mod folder (see the UI docs for the
        /// format). Returns the (hidden) window, or null if the file is missing or invalid (logged).
        /// </summary>
        /// <param name="relativeJsonPath">Path relative to the mod folder, e.g. <c>"ui/main.json"</c>.</param>
        IUiWindow LoadLayout(string relativeJsonPath);

        /// <summary>
        /// Register a named action that JSON layouts can reference (<c>"onClick": "myAction"</c>).
        /// Re-registering a name replaces the previous handler.
        /// </summary>
        void RegisterAction(string name, Action action);

        /// <summary>
        /// Register a named action that receives a value: the new text, the slider/toggle value as a
        /// string, or the dropdown option. Used by JSON layouts for widgets that produce a value.
        /// </summary>
        void RegisterAction(string name, Action<string> action);

        /// <summary>This mod's default theme for new windows. Starts as <see cref="UiTheme.Default"/>.</summary>
        UiTheme Theme { get; set; }

        /// <summary>True while any ASL text field has keyboard focus (mod keybinds and F8 are suppressed meanwhile).</summary>
        bool IsTextInputFocused { get; }
    }
}
