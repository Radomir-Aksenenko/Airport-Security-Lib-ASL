# Build UI in minutes

> **Status: 🧪 Implemented, not yet verified in-game.** Everything on this page is new in ASL 2.0. It compiles,
> its pure logic (JSON layout parser) is unit-tested, and it was written against patterns already proven in the
> game, but nobody has run it in the real game yet. Treat it as a release candidate and check
> `BepInEx\LogOutput.log` if something looks off. The one exception is `ctx.Ui.Announce`, which is from 0.1 and ✅.

ASL 2.0 ships a UI toolkit: windows, HUDs, toasts, dialogs and ~15 widgets that look good out of the box,
follow a theme, and need no Unity UI knowledge. You reach it through `ctx.Ui` (`IAslUi`). All types live in the
`ASL.Api` namespace.

- [Quick start](#quick-start)
- [Concepts](#concepts)
- [Windows](#windows) · [HUDs](#huds) · [Dialogs & toasts](#dialogs-toasts-and-announcements)
- [Widgets](#widgets)
- [Buttons](#buttons) (shapes × sizes × variants, custom colours, native look)
- [Layout containers](#layout-containers)
- [Live labels & tooltips](#live-labels-and-tooltips)
- [Keybind-toggled windows](#keybind-toggled-windows)
- [Themes](#themes)
- [F8 menu pages](#f8-menu-pages)
- [Cursor & input behaviour](#cursor-and-input-behaviour)
- [Window position memory](#window-position-memory)
- [JSON layouts (no code)](#json-layouts-reference)
- [Performance](#performance-notes) · [Limitations](#limitations-and-il2cpp-caveats)

Want to see everything at once? Press the console key and type `ui.demo`, or open **F8 > ASL > UI demo**. The
[`samples/UiShowcase`](../samples/UiShowcase) mod is the same thing as readable source.

---

## Quick start

A window with a header and a button, toggled by F6 (rebindable):

```csharp
using ASL.Api;
using UnityEngine;

public sealed class MyMod : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        var win = ctx.Ui.CreateWindow("main", "My Mod");                  // hidden until opened
        win.AddHeader("Hello");
        win.AddButton("Say hi", () => ctx.Ui.Toast("Hi!", UiToastKind.Success), UiButtonStyle.Pill(UiVariant.Success));
        win.BindToggleKey(ctx.Input.RegisterKey("window", "Toggle My Mod window", KeyCode.F6));
    }
}
```

Drop that in a [script mod](mod-types.md#script-mods) (`Main.cs`) and you are done: no project, no build. Prefer
no code at all? The same window as [JSON](#json-layouts-reference):

```json
{ "id": "main", "title": "My Mod", "toggleKey": "F6",
  "children": [
    { "type": "header", "text": "Hello" },
    { "type": "button", "text": "Say hi", "shape": "Pill", "variant": "Success", "onClick": "toast:Hi!" } ] }
```

`dotnet new asl-dll`, `asl-script` and `asl-ui` generate working starters ([templates](../templates/README.md)).

---

## Concepts

| Thing | What it is | Created with | Cursor | Closed by |
|---|---|---|---|---|
| **Window** | Draggable panel with title bar, scrolls when tall | `ctx.Ui.CreateWindow(id, title, options)` | freed, player input blocked (default) | close button, Escape, code |
| **HUD** | Always-visible overlay, no title bar, click-through | `ctx.Ui.CreateHud(id, anchor, options)` | never touched | code |
| **F8 page** | A sub-page inside ASL's F8 menu | `ctx.Menu.AddPage(title, build)` | the F8 menu owns it | F8 / Back |
| **Dialog** | Modal Confirm / Prompt / Alert | `ctx.Ui.Confirm/Prompt/Alert` | freed, input blocked | a button, Enter, Escape |
| **Toast** | Stacked corner notification | `ctx.Ui.Toast(text, kind, seconds)` | click-through | fades out |
| **Announce** | The game's own banner (0.1, ✅) | `ctx.Ui.Announce(text, seconds)` | n/a | times out |

Every window is built from the same pieces: **containers** (`IUiContainer`: the window itself, rows, columns,
grids, scroll views, groups, tabs) hold **widgets** (`IUiText`, `IUiButton`, `IUiToggle`, `IUiSlider`, `IUiInput`,
`IUiDropdown`, `IUiProgress`, `IUiImage`, ...). Every `Add*` call returns the new element so you can keep a handle:

```csharp
var status = win.AddLabel("Idle");
win.AddButton("Go", () => status.Text = "Running");
```

Good to know:

- Windows are namespaced per mod; two mods can both create `"main"`.
- Creating a window with an id you already used **destroys the old one** and returns a fresh window.
- `ctx.Ui.GetWindow(id)` returns your window or HUD by id (or `null`).
- Everything your mod creates is destroyed when the mod unloads or hot-reloads. Dialogs are destroyed without
  calling their callbacks.
- Call the UI from the main thread (all ASL callbacks already run there).

---

## Windows

```csharp
var win = ctx.Ui.CreateWindow("settings", "Settings", new UiWindowOptions
{
    Width = 520,                       // pixels at the 1920x1080 reference resolution (scales with the screen)
    Anchor = UiAnchor.Right,
    OffsetX = -24,
});
win.AddHeader("Settings");
win.Open();
```

A window is **hidden until you call `Open()`**. Members of `IUiWindow` (it is also an `IUiContainer`, so every
`Add*` method works on it directly):

| Member | What it does |
|---|---|
| `Open()` / `Close()` / `Toggle()` | Show (and bring to front) / hide. Opening a window closes the F8 menu. |
| `IsOpen`, `Visible` | State; setting `Visible` opens or closes. |
| `Opened`, `Closed` | Events raised after the window opens / closes. |
| `Title` | Title bar text (settable). |
| `WindowId` | The id you passed. |
| `SetPosition(anchor, offsetX, offsetY)` | Move to an anchor plus a pixel offset. |
| `BindToggleKey(keybind)` | A key that toggles the window ([details](#keybind-toggled-windows)). |
| `BringToFront()` | Draw above other windows. |
| `Destroy()` | Remove the window for good. |

### `UiWindowOptions`

All optional. `null` (or `new UiWindowOptions()`) gives a centred, draggable, 640 px wide window.

| Field | Default | Meaning |
|---|---|---|
| `Width` | `640` | Width in pixels. |
| `Height` | `0` | Fixed height; `0` = fit the content up to 85% of the screen, then scroll. |
| `Anchor`, `OffsetX`, `OffsetY` | `Center`, 0, 0 | Where it sits. Positive `OffsetY` moves up. |
| `Draggable` | `true` | Drag by the title bar. |
| `Closable` | `true` | Title bar has a close button. |
| `Modal` | `false` | Dim backdrop that blocks clicks outside the window. |
| `CaptureCursor` | `true` | Free the mouse cursor while open. |
| `BlockGameInput` | `true` | Block the local player's look/move input while open. |
| `ShowTitleBar` | `true` | Show the title bar. |
| `Scrollable` | `true` | Scroll when the content is taller than the window. |
| `RememberPosition` | `true` | Remember where the player dragged it ([details](#window-position-memory)). |
| `CloseOnEscape` | `true` | Escape closes the window (top-most first). |
| `Theme` | `null` | Theme for this window; `null` = your mod's `ctx.Ui.Theme`. |
| `Padding` | `-1` | Inner padding; `-1` = the theme's. |
| `SortOrder` | `0` | Layer order relative to other windows; higher draws on top. |

Handy combinations:

```csharp
// A tool panel that leaves the game playable (cursor stays locked, player can still move):
new UiWindowOptions { CaptureCursor = false, BlockGameInput = false, Draggable = false, ShowTitleBar = false };

// A blocking popup:
new UiWindowOptions { Modal = true, Width = 420, Closable = false };
```

---

## HUDs

A HUD is a window without the chrome: visible as soon as it is created, no title bar, never captures the cursor,
click-through. Use it for counters, status lines and timers.

```csharp
var hud = ctx.Ui.CreateHud("fps", UiAnchor.TopRight, new UiWindowOptions { Width = 240, OffsetX = -16, OffsetY = -16 });
hud.AddLiveLabel(() => $"Round {_round}   {_secondsLeft:0}s");
// hud.Close() hides it, hud.Open() shows it again.
```

From the options a HUD honours only `Width`, `Height`, `OffsetX`, `OffsetY`, `Theme`, `Padding` and `SortOrder`;
the interaction flags (`CaptureCursor`, `Draggable`, `Modal`, ...) are fixed to "none". A HUD stays visible while the
F8 menu is open.

---

## Dialogs, toasts and announcements

```csharp
ctx.Ui.Toast("Saved!", UiToastKind.Success);                 // Info (default), Success, Warning, Error
ctx.Ui.Toast("Disk almost full", UiToastKind.Warning, 6f);   // stays 6 s

ctx.Ui.Confirm("Reset", "Delete all progress?",
    onYes: () => Reset(), onNo: () => ctx.Log.Info("cancelled"),
    yesText: "Delete", noText: "Keep");

ctx.Ui.Prompt("Name", "What should we call you?", "Traveller",
    onSubmit: name => ctx.Log.Info("Hello " + name), onCancel: null);

ctx.Ui.Alert("Heads up", "Something happened.", onClose: null);

ctx.Ui.Announce("Round starting!");                          // the game's own banner
```

- **Toasts** stack in the top-right corner (max 5; the oldest is dropped), are click-through, appear above the F8
  menu, fade in/out, and are also written to the log. Minimum lifetime 0.5 s.
- **Dialogs** are modal (dim backdrop blocks clicks), centred, and show above all windows. **Enter** confirms
  (`Confirm` yes, `Alert` OK, `Prompt` submits when typing in its field), **Escape** cancels (`Confirm` no, `Prompt`
  cancel, `Alert` closes). Exactly one callback runs, then the dialog is destroyed. Long messages scroll.
- Toasts and dialogs use your mod's current `ctx.Ui.Theme`.

---

## Widgets

Every element (`IUiElement`) shares these members:

| Member | Meaning |
|---|---|
| `Id` | Optional id for `FindById` and JSON layouts. |
| `Visible` | Show/hide. A hidden element takes no layout space. |
| `Interactable` | `false` greys it out and ignores input. |
| `Tooltip` | Text shown after the pointer rests on it for ~0.5 s. |
| `SetSize(w, h)` | Fix the size in pixels (`<= 0` = auto/stretch on that axis). Returns the element, so calls chain. |
| `SetFlexible(f)` | Share of leftover width in a row. Returns the element. |
| `GameObject` | Escape hatch to the underlying Unity object. |
| `Destroy()` | Remove it. |

### Text

```csharp
win.AddHeader("Section");                                   // = AddLabel(text, UiTextStyle.Header)
win.AddLabel("Body text");                                  // UiTextStyle.Body
win.AddLabel("Hint", UiTextStyle.Muted);                    // Body, Small, Header, Title, Muted, Mono

var t = win.AddLabel("Danger");
t.Color = Color.red; t.FontSize = 26f; t.Align = UiTextAlign.Center;   // Left, Center, Right
t.Text = "Changed later";
```

Text accepts TextMeshPro rich text (`<b>`, `<color=#ff6b6b>`).

### Buttons

`AddButton(text, onClick, style)`; see [Buttons](#buttons) for the full style system.

```csharp
var b = win.AddButton("Start", () => Start());
b.Text = "Stop";
b.Clicked += () => ctx.Log.Info("also fires");               // extra listener
b.Interactable = false;
win.AddIconButton(ctx.Assets.LoadSprite("icons/gear.png"), OpenSettings, size: 48f, shape: UiShape.Circle);
```

### Toggle

```csharp
var god = win.AddToggle("God mode", false, on => _god = on);                      // UiToggleStyle.Switch (default)
win.AddToggle("Show hints", true, on => _hints = on, UiToggleStyle.Checkbox);     // Switch, Checkbox, Button
god.Value = true;                                                                  // setting Value does not raise the callback
```

### Slider

```csharp
var speed = win.AddSlider("Speed", 1f, 10f, 5f, v => _speed = v, step: 0.5f, format: "0.0");
speed.Value = 7f;          // clamped to Min..Max, no callback
// speed.Min, speed.Max, speed.Step are settable. step 0 = continuous. format is a .NET float format ("0.##", "0%").
```

### Text input

```csharp
var name = win.AddInput("Your name", "Traveller",
    onSubmit: text => ctx.Log.Info("Enter pressed: " + text),    // Enter
    onChanged: text => _name = text);                              // every edit
name.CharacterLimit = 24;      // 0 = unlimited
name.Focus();
// name.Text, name.Placeholder, name.IsFocused; events Changed / Submitted.
```

Single-line only. While any ASL text field has focus, mod keybinds and F8 are suppressed ([details](#cursor-and-input-behaviour)).

### Dropdown

```csharp
var mode = win.AddDropdown("Mode", new[] { "Easy", "Normal", "Hard" }, 1, i => _mode = i);
mode.Index = 2;                         // no callback
string chosen = mode.Value;             // "Hard"
mode.SetOptions(new[] { "A", "B" });
```

The list opens as a popup above everything (even the F8 menu) and closes on a pick, outside click or Escape.

### Progress bar and image

```csharp
var bar = win.AddProgressBar(0.3f, "30%");
bar.Value = 0.6f; bar.Text = "60%"; bar.FillColor = Color.green;      // Value is 0..1

var img = win.AddImage(ctx.Assets.LoadSprite("logo.png"), 96f, 96f);  // 0 = the sprite's own size
img.Color = new Color(1, 1, 1, 0.8f); img.PreserveAspect = true;
```

### Keybind row, spacer, separator

```csharp
win.AddKeybind(ctx.Input.RegisterKey("fire", "Fire", KeyCode.B));    // click-to-rebind row
win.AddSpacer(20f);                                                   // empty vertical space
win.AddSeparator();                                                   // thin divider line
```

### Finding and clearing

```csharp
win.AddLabel("Score: 0").Id = "score";                  // Id is settable
((IUiText)win.FindById("score")).Text = "Score: 10";    // searches the whole subtree; null if absent
box.Clear();                                            // destroy all children, keep the container
```

If a widget fails to build for any reason, ASL logs it once and returns a harmless **"[widget unavailable]"
placeholder** that implements every widget interface as a no-op. Your code keeps running and the rest of the window
still builds ([troubleshooting](troubleshooting.md#ui)).

---

## Buttons

A button is described by a `UiButtonStyle`: a colour role (**variant**), a silhouette (**shape**), a height preset
(**size**) and optional overrides. Pass one to `AddButton`, or change it later with `button.SetStyle(style)`.

### Variants (colour roles, from the theme)

| `UiVariant` | Look |
|---|---|
| `Primary` | Main call to action (theme Primary colour). The default. |
| `Secondary` | Neutral grey-blue. |
| `Success` | Green, confirming. |
| `Danger` | Red, destructive. |
| `Warning` | Amber, careful. |
| `Ghost` | No background until hovered; text only. |
| `Outline` | Transparent with a coloured border ring. |

### Shapes

| `UiShape` | Look |
|---|---|
| `Rect` | Sharp corners. |
| `Rounded` | Rounded corners (theme radius). The default. |
| `Pill` | Fully rounded ends (capsule). |
| `Circle` | A circle; width and height both = the button height. |
| `Square` | Square with small corners; width and height both = the button height. |
| `Native` | A clone of the game's own menu button (see below). |

### Sizes

| `UiSize` | Height |
|---|---|
| `Small` | 32 px |
| `Medium` | 44 px (default) |
| `Large` | 60 px |
| `XLarge` | 76 px |

`Circle` and `Square` use the height for both dimensions, so a `Large` circle is 60 x 60.

### Presets and building a style

```csharp
UiButtonStyle.Primary      // also Secondary, Success, Danger, Warning, Ghost, Outline  (fresh instance each time)
UiButtonStyle.Native       // the game's own button look
UiButtonStyle.Pill(UiVariant.Success)
UiButtonStyle.Circle(48f, UiVariant.Danger)                       // diameter 48
UiButtonStyle.Danger.With(shape: UiShape.Pill, size: UiSize.Large) // With() returns a modified copy
```

### Every combination

```csharp
var row = win.AddRow();
foreach (UiShape shape in new[] { UiShape.Rect, UiShape.Rounded, UiShape.Pill, UiShape.Square, UiShape.Circle })
    row.AddButton(shape.ToString(), null, new UiButtonStyle { Shape = shape, Size = UiSize.Large });

foreach (UiVariant v in System.Enum.GetValues(typeof(UiVariant)))
    win.AddButton(v.ToString(), null, new UiButtonStyle { Variant = v, Shape = UiShape.Pill, Size = UiSize.Small });
```

(`ui.demo` renders the full shape × size × variant grid for you.)

### Custom width, height and colours

```csharp
var style = new UiButtonStyle
{
    Variant = UiVariant.Primary,
    Shape = UiShape.Rounded,
    Width = 220f,                                  // 0 = fit the text / stretch
    Height = 52f,                                  // 0 = from Size
    CornerRadius = 20f,                            // -1 = the theme's
    Background = new Color(0.85f, 0.2f, 0.5f),     // null = from theme/variant
    TextColor = Color.white,                       // null = automatic readable colour
    FontSize = 24f,                                // 0 = automatic
};
win.AddButton("Custom", () => { }, style);
```

Icons: set `Icon` (a `Sprite`, for example from `ctx.Assets.LoadSprite`) and optionally `IconOnly = true`; or call
`button.SetIcon(sprite)` or `win.AddIconButton(...)`.

Layout behaviour: a button in a **row** is as wide as its text (set `Width`, or `SetFlexible(1f)`, to change that); in a
**column or window** it stretches to the full width unless `Width` is set.

### Native (game) look

`UiShape.Native` (or `UiButtonStyle.Native`) clones the game's own menu button, so it carries the LED-panel look,
font and hover animation of the main menu:

```csharp
win.AddButton("Play", Play, UiButtonStyle.Native);
```

To make a whole mod look like the game, use the `Game` theme: with `UiTheme.Game`, every **Primary + Rounded**
button is drawn as a native clone ([themes](#themes)).

Native buttons take their colours from the game, so `Variant`, `Background` and `TextColor` do not recolour them
(`Size`, `Width`, `Height` and `FontSize` still apply). The clone needs the game's button template, which ASL
captures when the game's main menu has been shown. Until then (or if it cannot be found) a `Native` button falls back to a
normal themed button instead of failing.

---

## Layout containers

Containers are layout groups. Create them with `AddRow`, `AddColumn`, `AddGrid`, `AddScroll`, `AddGroup`,
`AddTabs`; each returns an `IUiContainer` with the same `Add*` methods.

```csharp
var row = win.AddRow();                               // horizontal; spacing defaults to the theme's (AddRow(12f) to set)
row.AddButton("Yes", Yes, UiButtonStyle.Success);
row.AddButton("No", No, UiButtonStyle.Danger);

var col = win.AddColumn();                            // vertical (windows are already columns)

var grid = win.AddGrid(columns: 3, cellWidth: 120f, cellHeight: 48f);   // fixed-size cells, filled left to right
for (int i = 1; i <= 6; i++) grid.AddButton("Slot " + i, null, UiButtonStyle.Secondary);

var list = win.AddScroll(240f);                       // vertical scroll view, 240 px tall; mouse wheel scrolls it
for (int i = 0; i < 40; i++) list.AddLabel("Entry " + i);

var box = win.AddGroup("Options");                    // boxed panel with an optional title
box.AddToggle("Sound", true, on => _sound = on);

var tabs = win.AddTabs();                             // IUiTabs
var general = tabs.AddTab("General");                 // each AddTab returns the container for that tab's content
general.AddLabel("General settings");
tabs.AddTab("Advanced").AddLabel("Advanced settings");
tabs.SelectedIndex = 1;                               // no callback; tabs.Changed fires when the player switches
```

Sizing tips:

- Right-align a row's content with a flexible spacer first: `row.AddSpacer(0f).SetFlexible(1f);` then the buttons.
- Give a label the remaining width: `label.SetFlexible(1f)`.
- `element.SetSize(120, 40)` fixes both; `SetSize(0, 40)` fixes only the height.
- Nest freely: a `row` inside a `group` inside a `tab`.
- The window height follows its content up to 85% of the screen, then scrolls.

---

## Live labels and tooltips

A live label re-evaluates a function while it is visible, at most every `refreshSeconds` (default 0.25 s; minimum
0.05 s). Perfect for HUDs and status lines; you never touch it again:

```csharp
win.AddLiveLabel(() => $"Position: {_player.position}");              // refreshes ~4x a second
hud.AddLiveLabel(() => $"{Time.timeSinceLevelLoad:0}s", 1f);           // once a second
```

Live labels do not wrap (so the window height does not jitter). If your function throws, the error is logged with your
mod name and the label keeps its last text.

Tooltips work on any element:

```csharp
slider.Tooltip = "How fast you walk.\nDefault: 5";
```

They appear after ~0.5 s of hovering, next to the pointer, above everything, and never block clicks.

---

## Keybind-toggled windows

```csharp
var key = ctx.Input.RegisterKey("window", "Toggle my window", KeyCode.F6);   // rebindable; listed on your F8 page
win.BindToggleKey(key);
```

Pressing the key toggles the window. It is ignored while the player is typing in an ASL text field. Because it is a
normal ASL keybind, the player can rebind it in F8 and ASL checks for conflicts ([input docs](api-reference.md#input--keybinds)).
In JSON layouts, `"toggleKey": "F6"` does this for you.

---

## Themes

A `UiTheme` is plain data: colours, font sizes, spacing. Pick a preset, tweak it, and give it to your mod or a window.

| Preset | Look |
|---|---|
| `UiTheme.Default` | Dark navy panels, saturated blue accents. |
| `UiTheme.Dark` | Neutral dark grey. |
| `UiTheme.Light` | Pale panels, dark text. |
| `UiTheme.Game` | Default look, but Primary buttons use the game's native button clone. |
| `UiTheme.HighContrast` | Black and white with yellow accents, bigger fonts. |

Each preset getter returns a fresh copy, so editing one never affects another.

```csharp
ctx.Ui.Theme = UiTheme.Dark;                          // default for windows you create afterwards

var pink = UiTheme.Dark;                              // custom: start from a preset
pink.Primary = new Color(0.9f, 0.3f, 0.5f);
pink.Accent = new Color(1f, 0.6f, 0.8f);
pink.CornerRadius = 20f;
pink.FontSize = 24f;
var win = ctx.Ui.CreateWindow("pink", "Pink", new UiWindowOptions { Theme = pink });   // per-window override
```

Fields: `Background`, `Panel`, `PanelBorder`, `Header`, `Primary`, `Secondary`, `Success`, `Danger`, `Warning`, `Text`,
`TextMuted`, `Accent`, `InputBackground` (colours); `FontSize` (22), `HeaderFontSize` (30), `CornerRadius` (12),
`Spacing` (8), `Padding` (16); `UseGameFont` (true), `UseGameButtons` (false). `theme.Clone()` copies one.

A theme is applied **when a window is created**. Changing `ctx.Ui.Theme` later affects new windows, toasts and
dialogs only; to re-theme an existing window, create it again (the showcase rebuilds its window on theme change).

---

## F8 menu pages

The F8 menu stays the quick home for toggles, buttons and sliders (`ctx.Menu.AddToggle/AddButton/AddSlider/AddLabel`,
unchanged from 0.1). 2.0 adds richer entries:

```csharp
ctx.Menu.AddSeparator("Tools");                           // group header row (null title = plain divider)
ctx.Menu.AddWindowButton("Open settings", settingsWin);   // a row that opens/toggles one of your windows
ctx.Menu.AddPage("Advanced", page =>                      // a row that opens a rich sub-page, built with the toolkit
{
    page.AddHeader("Advanced");
    page.AddToggle("Debug", false, on => _debug = on);
    page.AddLiveLabel(() => "Entities: " + _count);
});
```

`AddPage`'s build callback runs **each time** the sub-page is shown, with a container to fill; the page is built
inside the F8 menu. Pages are polled only while the F8 menu is open.

Automatic pages:

- If you bind settings with [`ctx.Config`](config-and-storage.md), your mod's F8 page gets a **Settings** page for free
  (toggles, sliders, dropdowns, text fields, key capture).
- The F8 root has built-in **Mod Manager** and **ASL** pages ([mod manager](mod-manager.md)). When there are too many
  mods for one screen the root list paginates with `< Prev` / `Next >`.

---

## Cursor and input behaviour

ASL decides centrally whether the mouse cursor is free and the player's look/move input is blocked, from a set of
reasons: the F8 menu, each open window, and a focused text field. While any reason is active it frees the cursor
(the game's own `MetaCursor` way, the same mechanism F8 always used) and blocks local player input; when the last
reason goes away it undoes both.

- **Windows**: `CaptureCursor` frees the cursor; `BlockGameInput` blocks the player. HUDs never do either.
- **Typing**: while an ASL text field has keyboard focus, the cursor stays free and input is blocked, **mod keybinds
  (`IAslKeybind`) do not fire** (`WasPressed`, `IsHeld`, `WasReleased`, `Pressed`), and **F8 does not toggle**. Raw
  `ctx.Input.GetKey*` is *not* suppressed; check `ctx.Ui.IsTextInputFocused` yourself if you use it.
- **Escape** closes the top-most open window that has `CloseOnEscape` and is closable (never windows beneath it, never
  HUDs). An open dropdown popup closes first. While typing, Escape does nothing, except that a `Prompt` dialog cancels. While
  the F8 menu is open, Escape behaves as it always did. **Caveat:** a mod cannot consume input, so the game's own
  Escape handling (the pause menu) can react to the same key press that closed your window. Give a window
  `CloseOnEscape = false` if that is a problem for your mod.
- **Enter** confirms the top-most dialog (not while typing).
- **F8 and windows**: opening any window or dialog closes the F8 menu (F8 draws above windows). While F8 is open,
  windows are frozen behind it and HUDs keep running.
- **Mouse**: clicking a window raises it; the wheel scrolls the innermost scroll view under the pointer; dragging a
  title bar moves the window (clamped so it cannot leave the screen).

---

## Window position memory

Windows with `RememberPosition = true` (default; dialogs and HUDs never) remember where the player dragged them in
`BepInEx/config/ASL.Windows.json`, keyed `modId/windowId` with the anchor and offsets. It is written when a drag ends
(never per frame) and applied the next time the window is created, as long as the window uses the same anchor.
Players can wipe it with **F8 > ASL > Reset window positions** (windows pick their default place the next time they are created).

---

## JSON layouts reference

A layout is a JSON file in your mod folder that describes one window. Load it in code or list it in the manifest:

```csharp
IUiWindow w = ctx.Ui.LoadLayout("ui/main.json");    // hidden window; null (and a logged error) if unreadable
ctx.Ui.RegisterAction("save", () => Save());        // named actions the JSON can call
ctx.Ui.RegisterAction("rename", text => Rename(text));
```

```json
{ "id": "mymod", "name": "My Mod", "type": "script", "ui": [ "ui/main.json" ] }
```

The manifest's `ui` list loads each file for **any** mod kind, content mods included. Rules:

- Property names and enum values are case-insensitive; comments and trailing commas are allowed.
- The top level is always a window. Windows start **closed**; use `"open": true` or `toggleKey`.
- Unknown types or fields, wrong value types and bad enum names are **logged as warnings** (with the path inside the
  file, for example `children[2].children[0]: unknown field 'colour'`) and skipped. The rest of the window still builds. An
  unreadable or non-JSON file logs an error and creates no window.
- Nesting deeper than 32 levels is cut off with a warning.

### Window fields

| Field | Type | Meaning |
|---|---|---|
| `id` | string | Window id; default = the file name without extension. |
| `title` | string | Title bar text; default = the id. |
| `width`, `height` | number | Pixels (see [`UiWindowOptions`](#uiwindowoptions)); `0`/absent = defaults. |
| `anchor` | `TopLeft` `Top` `TopRight` `Left` `Center` `Right` `BottomLeft` `Bottom` `BottomRight` | Screen anchor. |
| `offsetX`, `offsetY` | number | Offset from the anchor. |
| `theme` | `Default` `Dark` `Light` `Game` `HighContrast` | Preset; unknown name = warning, mod theme used. |
| `toggleKey` | string | A Unity `KeyCode` name (`F6`, `BackQuote`, `Alpha1`). Registers the rebindable ASL keybind `ui.<id>.toggle`, shown on the mod's F8 page. |
| `draggable`, `closable`, `modal` | bool | As in `UiWindowOptions`. |
| `open` | bool | Open as soon as it is built. |
| `children` | array | The content. |

### Element types

Every element also accepts the **common fields** `id`, `tooltip`, `interactable`, `visible`, `width`, `height`
(`width`/`height` fix the size in pixels; on `image` they size the picture, on `spacer` `height` is the gap).

| `type` | Fields | Notes |
|---|---|---|
| `header` | `text` | |
| `label` | `text`, `style` | `style`: `Body` `Small` `Header` `Title` `Muted` `Mono`. |
| `button` | `text`, `variant`, `shape`, `size`, `onClick` | Enums as in [Buttons](#buttons). |
| `toggle` | `label` (or `text`), `value` (bool), `style`, `onChange`, `bind` | `style`: `Switch` `Checkbox` `Button`. |
| `slider` | `label` (or `text`), `min`, `max`, `value`, `step`, `format`, `onChange`, `bind` | Defaults `0..1`; if `max <= min` it becomes `min..min+1` with a warning. |
| `input` | `placeholder`, `value` (string), `onSubmit`, `onChange`, `bind` | |
| `dropdown` | `label` (or `text`), `options` (string array), `value` (index or text), `onChange`, `bind` | |
| `progress` | `value` (0..1), `text` | |
| `image` | `file`, `width`, `height` | A PNG in the mod folder; missing/unreadable = warning, skipped. |
| `spacer` | `height` | Default 12. |
| `separator` | | |
| `row`, `column` | `spacing`, `children` | `spacing` default = theme spacing. |
| `grid` | `columns`, `cellWidth`, `cellHeight`, `spacing`, `children` | Defaults 2 columns, 120 x 40 cells. |
| `scroll` | `children` (and `height`) | Default height 200. |
| `group` | `title`, `children` | |
| `tabs` | `tabs`: `[ { "title": "One", "children": [ ... ] } ]` | |

### Actions

`onClick` / `onChange` / `onSubmit` take an action string:

| Action | Effect |
|---|---|
| `close` | Close this window. |
| `open:<windowId>` | Open another window of the same mod. |
| `toggle:<windowId>` | Toggle another window of the same mod. |
| `toast:<text>` | Show a toast. |
| `announce:<text>` | Show the game's announcement banner. |
| `command:<console line>` | Run a [console](console.md) line (for example `command:help`). |
| anything else | A **named action** registered in code with `ctx.Ui.RegisterAction(name, ...)`. |

A named action that is not registered logs a warning once and does nothing. Widgets that produce a value hand it to
the action as a string: toggles `true`/`false`, sliders a number in invariant culture (`1.5`), inputs the text, dropdowns the
option text; buttons pass an empty string. Register `Action<string>` to receive it; a plain `Action` ignores it.

### Binding to config

`"bind": "config:Section.Key"` two-way binds a control to one of your [`ctx.Config`](config-and-storage.md) entries:
the control starts at the entry's value, writes changes back (and saves), and follows changes made elsewhere
(F8 Settings page, the `.cfg` file, code).

| Control | Accepts entries of type |
|---|---|
| `toggle` | `bool` |
| `slider` | `int`, `float`, `double` (a `BindRange` entry supplies `min`/`max`; `int` entries step by 1) |
| `input` | any type (text must convert; otherwise the old value is restored) |
| `dropdown` | enum (options default to the enum names), `int` (index) or `string` |

The entry must already be bound when the layout loads. For `ui` in the manifest that is automatic for code mods (it
loads after `OnLoad`); if you call `LoadLayout` yourself, bind first. A missing entry, wrong type or a content mod (no
config) logs a warning and leaves the control unbound (it still works, it just is not persisted).

### Full example

```json
{
  "id": "tools", "title": "Tools", "width": 560, "anchor": "Right", "offsetX": -24, "theme": "Dark", "toggleKey": "F6",
  "children": [
    { "type": "header", "text": "Tools" },
    { "type": "toggle", "label": "God mode", "bind": "config:General.God", "tooltip": "Take no damage" },
    { "type": "slider", "label": "Speed", "bind": "config:General.Speed" },
    { "type": "dropdown", "label": "Mode", "options": ["Easy", "Normal", "Hard"], "value": 1, "onChange": "setMode" },
    { "type": "row", "children": [
        { "type": "button", "text": "Save", "variant": "Success", "shape": "Pill", "onClick": "save" },
        { "type": "button", "text": "Console", "variant": "Secondary", "onClick": "command:help" },
        { "type": "button", "text": "Close", "variant": "Ghost", "onClick": "close" } ] },
    { "type": "tabs", "tabs": [
        { "title": "Info", "children": [ { "type": "label", "text": "Hello", "style": "Muted" } ] },
        { "title": "More", "children": [ { "type": "progress", "value": 0.3, "text": "30%" } ] } ] }
  ]
}
```

### No-code UI in content mods

A content mod has no code, so it can only use the built-in actions (`close`, `open:`, `toggle:`, `toast:`,
`announce:`, `command:`), but that already makes useful panels: shortcut buttons for console commands, info windows,
toggle-key HUD-like panels. `config:` binds are not available (a content mod has no config). Images load from the
mod folder. Keep `"content": {}` in the manifest (a content mod needs a `content` object even if it only ships UI). See [`samples/ExampleUiContentMod`](../samples/ExampleUiContentMod) and `dotnet new asl-ui`.

---

## Performance notes

- With nothing open, the UI costs one cheap check per frame. Only **open** windows (and visible HUDs, toasts and the
  F8 page) are polled, and only the widgets that need polling (inputs, sliders, toggles, hover, tooltips, live labels).
- Live labels refresh on a timer (default 4x a second), not per frame. Prefer them to setting `Text` from `Update`.
- If you do set `Text` from `Update`, assign only when it changed; every assignment rebuilds the text mesh.
- Build windows once in `OnLoad` and keep references. Don't create widgets per frame. Use `Clear()` and rebuild only when
  the data changes.
- Big lists: every row is a few Unity objects. Hundreds of rows in a `scroll` are fine; thousands are not.
- The shapes (rounded rectangles, circles, rings) are generated once and cached; many buttons share a few sprites.
- Don't allocate in a live-label function more than a string; it runs several times a second.

---

## Limitations and IL2CPP caveats

- Widget values are **polled**, not event-driven (ASL avoids Unity event listeners on this IL2CPP build). `Changed`
  callbacks therefore fire up to one frame after the player's action, and only while the window is open. Setting a
  value from code (`toggle.Value = true`) does not raise the callback.
- No multi-line text input, drag and drop, right-click, keyboard navigation (Tab) or free (absolute) positioning.
  Layout is rows, columns, grids and scroll views.
- The font is the game's own TextMeshPro font, found at runtime. If it cannot be found ASL falls back to TMP's default
  and logs `[ui] game font not found`. In the unlikely case no font exists, text stays invisible until one is found.
- `UiShape.Native` needs the game's captured menu button (see [Buttons](#native-game-look)).
- Themes apply at creation time (see [Themes](#themes)); creating a window with an existing id replaces the old one,
  so references to the old window are dead.
- Your callbacks are wrapped: an exception is logged with your mod name (repeats are rate-limited) and never reaches
  the game.
- Never touch the underlying `GameObject`s of other mods' windows, and never read other objects' meshes (native
  crash, see [troubleshooting](troubleshooting.md#native-crashes-accessviolation)). Use `element.GameObject` only
  for things the toolkit does not expose.
- Everything here is 🧪 until verified in-game: layouts, native buttons, cursor handling and Escape behaviour are
  the first things to check.
