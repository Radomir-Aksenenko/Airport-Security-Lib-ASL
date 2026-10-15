# `manifest.json` reference

Every mod folder under `mods/` contains exactly one `manifest.json`. It is parsed with
`System.Text.Json` (case-insensitive property names, comments and trailing commas allowed).

> **Status.** The 0.1 fields (`id` ... `content.textures`/`listTextureNames`) are ✅ verified in-game. The fields marked
> **(2.0)** are 🧪 implemented but not yet verified in-game. All 2.0 fields are optional: a 0.1 manifest keeps working unchanged
> ([upgrading](upgrading-to-2.0.md)).

## Fields

| Field | Type | Required | Description |
|---|---|---|---|
| `id` | string | **yes** | Unique mod id, reverse-DNS style: `com.author.mymod`. |
| `name` | string | no | Human-readable name (shown in logs and the Mod Manager). Falls back to `id`. |
| `version` | string | no | Semantic version. Defaults to `1.0.0`. Other mods' `dependencies` ranges are checked against it. |
| `author` | string | no | Author name. |
| `description` | string | no | Short description. |
| `type` | string | no | `"dll"`, `"script"`, or `"content"`. Auto-detected if omitted. |
| `entry` | string | no | Entry file relative to the folder: the `.dll` (dll) or `.cs` (script). |
| `content` | object | for content mods | Declarative content - see [`content`](#content-object). |
| `dependencies` | string[] | no | **(2.0)** Hard dependencies: `"modId"` or `"modId@>=1.2.0"`. They load first; if one is missing, disabled, failed or the wrong version, this mod is skipped (`MissingDependency`). |
| `optionalDependencies` | string[] | no | **(2.0)** Soft dependencies: loaded first when installed, ignored otherwise. |
| `loadAfter` | string[] | no | **(2.0)** Prefer to load after these mods (ordering only). |
| `loadBefore` | string[] | no | **(2.0)** Prefer to load before these mods (ordering only). |
| `minAslVersion` | string | no | **(2.0)** Minimum ASL version, for example `"2.0.0"`. A newer requirement than the running ASL marks the mod `Incompatible`. |
| `tags` | string[] | no | **(2.0)** Free-form labels shown in the Mod Manager. |
| `website` | string | no | **(2.0)** Project or home page URL. |
| `icon` | string | no | **(2.0)** Icon PNG path relative to the mod folder, for the Mod Manager. |
| `ui` | string[] | no | **(2.0)** Declarative UI layouts to load after the mod loads, for example `["ui/main.json"]`. Works for **every** mod kind, content mods included. See [JSON layouts](ui.md#json-layouts-reference). |
| `hotReload` | bool | no | **(2.0)** Script mods only: watch the `.cs` sources and reload the mod when they change ([details](mod-manager.md#hot-reload-script-mods)). |

Dependency entries follow the [version-range syntax](mod-manager.md#version-ranges) (`>=`, `>`, `<`, `<=`, `=`, `^`, `~`, bare version = `>=`,
several joined by spaces). Load order, states and what happens to a mod with a missing dependency are in
[mod-manager.md](mod-manager.md).

### Auto-detection of `type`

If `type` is missing, ASL picks:
1. `dll` - if the folder contains a `.dll`
2. `script` - else if it contains a `.cs`
3. `content` - otherwise

Being explicit is recommended.

### Lenient parsing

Every 2.0 field tolerates the wrong JSON shape instead of failing the whole manifest, so an old manifest that happened to use a new
field name for something else still loads:

- String arrays (`dependencies`, `optionalDependencies`, `loadAfter`, `loadBefore`, `tags`, `ui`) also accept a single string; non-string items are skipped; any other shape is ignored.
- String fields (`minAslVersion`, `website`, `icon`) ignore a non-string value.
- `hotReload` accepts `true`/`false`, the strings `"true"`/`"yes"`/`"on"`/`"1"` (anything else is false), or a number (non-zero = true).
- `content` and its sub-fields ignore a wrong shape; invalid array items are skipped.

A manifest that is not valid JSON, or has no `id`, is skipped with a log line (and listed as `Failed` in the Mod Manager).
Unknown fields are ignored.

## `content` object

For `"type": "content"` mods (no code). All parts are optional.

```json
"content": {
  "listTextureNames": false,
  "textures": [ { "target": "GameTextureName", "file": "replacement.png" } ],
  "listAudioNames": false,
  "audio":    [ { "target": "AudioClipName", "file": "replacement.wav" } ],
  "strings":  { "Exact on-screen text": "Replacement" }
}
```

| Field | Type | Since | Description |
|---|---|---|---|
| `textures` | array | 0.1 | Texture swaps. Each replaces the loaded game texture named `target` with the PNG at `file` (relative to the mod folder). |
| `textures[].target` / `.file` | string | 0.1 | The in-game texture name (discover with `listTextureNames`) and the PNG shipped in the mod. |
| `listTextureNames` | bool | 0.1 | If `true`, ASL logs a sample of loaded texture names (to find `target` values). |
| `audio` | array | 2.0 | Audio swaps: each replaces the `AudioClip` named `target` with the WAV at `file`. 🧪 |
| `audio[].target` / `.file` | string | 2.0 | The clip name and a PCM or float WAV in the mod folder. |
| `listAudioNames` | bool | 2.0 | If `true`, ASL logs the distinct `AudioClip` names it finds (to find `target` values). 🧪 |
| `strings` | object | 2.0 | **Experimental.** Map of exact on-screen text to replacement text. 🧪 |

Behaviour and limits of each: texture swaps in [mod-types](mod-types.md#content-mods); audio and strings in
[assets-and-localization.md](assets-and-localization.md#content-mods-v2-audio-and-strings).

## `ui` (JSON layouts)

A list of layout files; each becomes a window of this mod. The file format (window fields, every element type, actions, config binding) is documented in the
[UI guide](ui.md#json-layouts-reference). In short:

```json
{ "id": "com.you.panel", "name": "Panel", "type": "content", "content": {}, "ui": [ "ui/panel.json" ] }
```

```json
{ "id": "main", "title": "My Window", "toggleKey": "F6",
  "children": [
    { "type": "header", "text": "Hello" },
    { "type": "button", "text": "OK", "variant": "Success", "shape": "Pill", "onClick": "toast:Hi!" } ] }
```

Windows start closed; `"open": true` or a `toggleKey` makes them reachable. A bad layout logs warnings or an error and never
stops the mod from loading. A `"type": "content"` mod that only ships UI still needs a `content` object (`"content": {}` is
enough); without it ASL logs `type=content but no 'content' section` and skips the mod.

## Examples

DLL mod:
```json
{ "id": "com.you.mymod", "name": "My Mod", "version": "1.2.0", "type": "dll", "entry": "MyMod.dll" }
```

DLL mod using the 2.0 API with a dependency:
```json
{
  "id": "com.you.shop", "name": "Shop", "version": "1.0.0", "type": "dll", "entry": "Shop.dll",
  "minAslVersion": "2.0.0",
  "dependencies": [ "com.you.economy@^1.2.0" ],
  "optionalDependencies": [ "com.you.fancyui" ],
  "tags": [ "economy" ]
}
```

Script mod with hot reload:
```json
{ "id": "com.you.myscript", "name": "My Script", "type": "script", "entry": "Main.cs", "hotReload": true }
```

Content mod:
```json
{
  "id": "com.you.retex", "name": "Retex", "type": "content",
  "content": { "textures": [ { "target": "star_gray", "file": "star.png" } ] }
}
```

No-code UI mod:
```json
{ "id": "com.you.panel", "name": "Panel", "type": "content", "content": {}, "ui": [ "ui/panel.json" ] }
```
