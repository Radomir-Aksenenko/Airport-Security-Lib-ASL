# UI Showcase (DLL mod)

A guided tour of the ASL 2.0 UI toolkit, written to be read top to bottom. It is also a handy way to *see* everything the
toolkit can do before you write your own mod.

> **Status: 🧪 Implemented, not yet verified in-game** (like everything new in ASL 2.0). Requires ASL 2.0 or newer
> (`"minAslVersion": "2.0.0"`).

## What it demonstrates

| Where | What |
|---|---|
| `UiShowcase.cs` | `OnLoad` wiring: `ctx.Config` (bool, ranged float, enum, with `Changed` handlers; shows up on the F8 Settings page), `ctx.Storage` (an open counter), `ctx.Ui.Theme`, a rebindable toggle key, `ctx.Commands`, `ctx.Menu` (`AddButton`, `AddWindowButton`, `AddPage`), `ctx.Mods.Publish`, `ctx.Loc` |
| `ShowcaseWindow.cs` | The big tabbed window: **Buttons** (every shape × size, every variant, the native game look, icon buttons), **Inputs** (toggles, slider, input, dropdown, progress), **Layout** (row, column, grid, scroll, group, tabs), **Dialogs** (Confirm, Prompt, Alert, toasts), **Themes** (live preset switcher) |
| `ShowcaseHud.cs` | A small always-on HUD (live label with FPS and time) in the top-right corner |
| `lang/en.json`, `lang/ru.json` | Translations loaded through `ctx.Loc` |

## Using it

1. Build the solution (`dotnet build ASL.slnx -c Release`); the default build copies the mod to `<GamePath>\mods\UiShowcase\`
   (skip with `/p:DeployToGame=false`). Or copy the built `UiShowcase.dll`, `manifest.json` and `lang\` folder there yourself.
2. In game press **F7** to toggle the window (rebindable in F8 > UI Showcase). **F8** opens ASL's menu: this mod adds an "Open showcase" button, an
   "About" window, a quick page and a **Settings** page with its three config entries.
3. Console (the <kbd>`</kbd> key): `showcase` opens the window, `showcase.toast hello` pops a toast. `ui.demo` opens ASL's own all-widgets demo.

## Read next

- [Build UI in minutes](../../docs/ui.md)
- [Config & storage](../../docs/config-and-storage.md), [timers & events](../../docs/timers-and-events.md), [console](../../docs/console.md)
- [`samples/ExampleUiContentMod`](../ExampleUiContentMod): the same idea with no code at all
