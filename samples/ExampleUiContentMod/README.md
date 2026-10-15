# Example UI Content Mod

A mod with **no code**: its whole interface is one JSON file. Drop this folder into the game's `mods/`
folder, start the game and press **F9**.

> **Status: 🧪 Implemented, not yet verified in-game.** Needs ASL 2.0 or newer. Full format reference:
> [docs/ui.md](../../docs/ui.md#json-layouts-reference).

## How it works

1. `manifest.json` has `"type": "content"` and a top-level `"ui"` list naming the layout files:

   ```json
   { "id": "com.you.mymod", "name": "My Mod", "version": "1.0.0", "type": "content",
     "content": {}, "ui": [ "ui/panel.json" ] }
   ```

   (A `content` object is required for `"type": "content"`, even an empty one.)

2. Each layout file describes one window. The top level is always the window; its `children` are widgets:

   ```json
   {
     "id": "panel", "title": "My window", "width": 600, "anchor": "Center", "theme": "Dark", "toggleKey": "F9",
     "children": [
       { "type": "header", "text": "Hello" },
       { "type": "button", "text": "Hi", "shape": "Pill", "variant": "Success", "onClick": "toast:Hi!" }
     ]
   }
   ```

   `toggleKey` is the key that shows and hides the window. Property names and enum values are not
   case-sensitive, and comments and trailing commas are allowed.

## Node types

`header`, `label`, `button`, `toggle`, `slider`, `input`, `dropdown`, `progress`, `image`, `spacer`,
`separator`, and the containers `row`, `column`, `grid`, `scroll`, `group`, `tabs` (a `"tabs"` array of `{ "title": ..., "children": [...] }`).

Buttons take `shape` (Rect, Rounded, Pill, Circle, Square, Native), `size` (Small, Medium, Large, XLarge) and
`variant` (Primary, Secondary, Success, Danger, Warning, Ghost, Outline).

## Built-in actions (for `onClick`, `onChange`, `onSubmit`)

| Action | Effect |
|---|---|
| `toast:text` | Pop a toast. |
| `announce:text` | Show text on the game's announcement banner. |
| `command:line` | Run a console command, e.g. `command:help`. |
| `open:id` / `toggle:id` | Open or toggle another window of your mod. |
| `close` | Close this window. |

Anything else is a *named action* that a code mod registers with `ctx.Ui.RegisterAction`. A layout with only
the built-ins, like this one, needs no code at all.

## Mistakes are forgiving

An unknown type, field or enum value is reported in `BepInEx\LogOutput.log` as a warning and skipped; the rest of
the window still builds. The unit tests parse every `samples/*/ui/*.json` and require **zero** warnings.
