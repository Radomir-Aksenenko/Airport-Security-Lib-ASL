# `manifest.json` reference

Every mod folder under `mods/` contains exactly one `manifest.json`. It is parsed with
`System.Text.Json` (case-insensitive, comments and trailing commas allowed).

## Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | **yes** | Unique mod id, reverse-DNS style: `com.author.mymod`. |
| `name` | string | no | Human-readable name (shown in logs). Falls back to `id`. |
| `version` | string | no | Semantic version. Defaults to `1.0.0`. |
| `author` | string | no | Author name. |
| `description` | string | no | Short description. |
| `type` | string | no | `"dll"`, `"script"`, or `"content"`. Auto-detected if omitted. |
| `entry` | string | no | Entry file relative to the folder: the `.dll` (dll) or `.cs` (script). |
| `content` | object | for content mods | Declarative content — see below. |
| `ui` | string[] | no | 🧪 Declarative UI layouts to load after the mod loads, e.g. `["ui/main.json"]`. Works for **every** mod kind, including content mods (no code). Each file becomes a window of this mod; windows start closed unless the layout says `"open": true`, and a layout's `toggleKey` makes it reachable in game. See [JSON layouts](#ui-json-layouts). |

### Auto-detection of `type`

If `type` is missing, ASL picks:
1. `dll` — if the folder contains a `.dll`
2. `script` — else if it contains a `.cs`
3. `content` — otherwise

Being explicit is recommended.

## `content` object

```json
"content": {
  "listTextureNames": false,
  "textures": [
    { "target": "GameTextureName", "file": "replacement.png" }
  ]
}
```

| Field | Type | Description |
|---|---|---|
| `textures` | array | Texture swaps. Each replaces the loaded game texture named `target` with the PNG at `file` (relative to the mod folder). |
| `textures[].target` | string | The in-game texture name to replace. Discover names with `listTextureNames`. |
| `textures[].file` | string | PNG file shipped in the mod folder. |
| `listTextureNames` | bool | If `true`, ASL logs a sample of loaded texture names (to find `target` values). |

See [content mods](mod-types.md#content-mods) for the pixel-write build caveat.

## `ui` (JSON layouts)

🧪 *Compile-checked, not yet verified in-game.* Each path in `ui` is a JSON window description (comments and
trailing commas allowed):

```json
{ "id": "main", "title": "My Window", "width": 600, "anchor": "Center", "theme": "Dark", "toggleKey": "F6", "open": false,
  "children": [
    { "type": "header", "text": "Hello" },
    { "type": "row", "children": [
        { "type": "button", "text": "OK", "variant": "Success", "shape": "Pill", "size": "Large", "onClick": "toast:Hi!" },
        { "type": "button", "text": "Close", "onClick": "close" } ] },
    { "type": "toggle", "label": "God mode", "bind": "config:General.God" },
    { "type": "slider", "label": "Speed", "min": 1, "max": 5, "step": 0.5, "bind": "config:General.Speed" }
  ] }
```

* **Window fields:** `id` (default: file name), `title`, `width`, `height`, `anchor`, `offsetX`, `offsetY`, `theme`
  (`Default`/`Dark`/`Light`/`Game`/`HighContrast`), `toggleKey` (a Unity `KeyCode` name; registers the rebindable
  ASL keybind `ui.<id>.toggle`, shown on the mod's F8 page), `draggable`, `closable`, `modal`, `open`, `children`.
* **Element types:** `header`, `label` (`style`), `button` (`variant`, `shape`, `size`, `onClick`), `toggle` (`label`,
  `value`, `style`, `onChange`), `slider` (`label`, `min`, `max`, `value`, `step`, `format`, `onChange`), `input`
  (`placeholder`, `value`, `onSubmit`, `onChange`), `dropdown` (`label`, `options`, `value`, `onChange`), `progress`
  (`value` 0..1, `text`), `image` (`file`, `width`, `height` — a PNG in the mod folder), `spacer` (`height`),
  `separator`, `row`/`column` (`spacing`), `grid` (`columns`, `cellWidth`, `cellHeight`), `scroll` (`height`), `group`
  (`title`), `tabs` (`tabs: [{ "title", "children" }]`). Every element also takes `id`, `tooltip`, `interactable`,
  `visible`, `width`, `height`.
* **Actions** (`onClick` / `onChange` / `onSubmit`): built-ins `close`, `open:<windowId>`, `toggle:<windowId>`,
  `toast:<text>`, `announce:<text>`, `command:<console line>` (work without code); any other string is a named
  action registered in code with `ctx.Ui.RegisterAction`. Widgets that produce a value pass it as a string
  (invariant culture; toggles `true`/`false`; dropdowns the option text).
* **`"bind": "config:Section.Key"`** two-way binds a toggle / slider / input / dropdown to one of the mod's
  `ctx.Config` entries (the entry must already be bound when the layout loads; otherwise a warning is logged and the
  control stays unbound).
* Unknown types / fields and bad values are logged as warnings (with the path inside the file) and skipped; an
  unreadable file logs an error and creates no window.

## Examples

DLL mod:
```json
{ "id": "com.you.mymod", "name": "My Mod", "version": "1.2.0", "type": "dll", "entry": "MyMod.dll" }
```

Script mod:
```json
{ "id": "com.you.myscript", "name": "My Script", "type": "script", "entry": "Main.cs" }
```

Content mod:
```json
{
  "id": "com.you.retex", "name": "Retex", "type": "content",
  "content": { "textures": [ { "target": "star_gray", "file": "star.png" } ] }
}
```
