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
