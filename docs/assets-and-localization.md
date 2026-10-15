# Assets and localization

> **Status: 🧪 Implemented, not yet verified in-game.** New in ASL 2.0. (Texture swaps in content mods are from 0.1 and ✅.)

## Assets: `ctx.Assets`

Load files from your mod folder without touching Unity's loaders. All paths are **relative to your mod folder** and
cannot escape it (rooted paths, drive colons and `..` are rejected).

```csharp
Sprite icon  = ctx.Assets.LoadSprite("icons/gear.png");
Sprite panel = ctx.Assets.LoadSprite("ui/panel.png", border: 16f);       // 9-sliced, 16 px borders
Texture2D t  = ctx.Assets.LoadTexture("textures/logo.png");
AudioClip click = ctx.Assets.LoadAudio("sounds/click.wav");
string json  = ctx.Assets.ReadText("data/items.json");
byte[] raw   = ctx.Assets.ReadBytes("data/blob.bin");

win.AddImage(icon, 48f, 48f);
win.AddIconButton(icon, OpenSettings);
```

| Method | Returns | Notes |
|---|---|---|
| `GetPath(relative)` | absolute path | Safe join inside the mod folder; **throws** (`UnauthorizedAccessException`) on traversal. |
| `ReadBytes(relative)` / `ReadText(relative)` | `byte[]` / UTF-8 `string` | Throw if the file is missing. |
| `LoadTexture(relativePng)` | `Texture2D` | Decoded in managed code; cached. |
| `LoadSprite(relativePng, border = 0)` | `Sprite` | `border > 0` = 9-sliced with that border in pixels. Cached per file and border. |
| `LoadAudio(relativeWav)` | `AudioClip` | Managed WAV decode; cached. |
| `LoadBundle(relative)` | `AssetBundle` | `AssetBundle.LoadFromFile`; cached. |
| `LoadBundleAsset(bundleRelative, assetName)` | `UnityEngine.Object` | Loads the bundle if needed, then the named asset. |

The `Load*` methods never throw for bad input: they return **`null`** and log one line (once per file) saying why, so
check for `null`.

Details and limits:

- **PNG**: 8-bit, non-interlaced; grayscale, RGB, palette, gray+alpha and RGBA. (Unity's `ImageConversion.LoadImage` cannot be
  used on this IL2CPP build, so ASL decodes PNGs itself.) Other PNGs log a decode error.
- **WAV**: PCM 8 (unsigned), 16, 24 and 32-bit integer, IEEE float 32/64, mono or stereo (more channels decode too), including
  `WAVE_FORMAT_EXTENSIBLE`; unknown chunks are skipped. Compressed WAV (ADPCM, MP3-in-WAV) is rejected with a clear message. Clips
  are fully decoded into memory (not streamed), so keep long music out; ship short effects.
- **AssetBundle**: must be built with the same Unity version as the game (Unity 6000.0.x) for the standalone Windows platform. If
  it fails ASL logs `AssetBundle.LoadFromFile returned null` (wrong version/platform, or already loaded by another mod). Bundles are unloaded when your mod unloads.
- **Caching and lifetime**: results are cached by path, so loading again is free. Textures, sprites and clips are marked `HideAndDontSave`
  and are **destroyed when your mod unloads or hot-reloads**; reload your assets in `OnLoad`.
- Don't hold a loaded sprite across a hot reload; the old one is gone.

---

## Localization: `ctx.Loc`

Put flat (or nested) JSON files in a `lang/` folder inside your mod:

```
mods/MyMod/
├── manifest.json
└── lang/
    ├── en.json
    └── ru.json
```

```json
// lang/en.json
{
  "greeting": "Hello, {0}!",
  "menu": { "open": "Open window" }          // nested keys flatten to "menu.open"
}
```

```csharp
win.AddLabel(ctx.Loc.Get("greeting", playerName));    // string.Format with args
win.AddButton(ctx.Loc.Get("menu.open"), Open);

if (ctx.Loc.Has("tip.1")) ShowTip(ctx.Loc.Get("tip.1"));

ctx.Loc.LanguageChanged += RebuildTexts;               // the player changed the game language
```

| Member | Meaning |
|---|---|
| `Language` | The game's current language token (for example `"en"`, `"ru"` or a name like `"Russian"`); `null` if unknown. |
| `Get(key, args...)` | The text for `key`. Falls back to `en`, then to **the key itself**. `string.Format` is applied when `args` are given; a bad format string returns the raw text instead of throwing. |
| `Has(key)` | True if the key exists in the current language or in English. |
| `LanguageChanged` | Raised when the player switches the game language (also available as `ctx.Events.LanguageChanged`, which carries the token). |

**Which file is used.** ASL looks at the files in `lang/` and the game's language token and picks the first match of:
exact file name (`ru` for `ru`, case-insensitive) -> region/two-letter code (`en-US` or `pt_BR` use `en`) -> a known language name
(`Russian` -> `ru`) -> the first two letters of the token -> `en` -> any file that exists. Keys missing from the chosen file are looked up in `en.json`, and
then the key itself is shown, so a half-finished translation is safe.

**File format.** Objects of strings; nested objects flatten with dots (`{"menu":{"open":"x"}}` is `menu.open`); arrays flatten to
`key.0`, `key.1`; numbers and booleans become their text; `null` is skipped; comments and trailing commas are tolerated. Files are
read lazily on first use and again after a language change.

**Rebuilding UI on language change.** Window and label texts are plain strings set when you create the widgets, so rebuild
or reassign them in a `LanguageChanged` handler (the [UiShowcase](../samples/UiShowcase) does this).

---

## Content mods v2: audio and strings

Content mods (no code) can now replace sounds and on-screen text, next to the texture swaps from 0.1. All fields are optional and
live in the manifest's `content` object ([manifest reference](manifest.md#content-object)):

```json
{
  "id": "com.you.sounds", "name": "Sound pack", "type": "content",
  "content": {
    "listAudioNames": true,
    "audio": [ { "target": "door_open", "file": "audio/door.wav" } ],
    "strings": { "Boarding pass": "Ticket to ride" }
  }
}
```

### `audio` and `listAudioNames`

- `audio[]`: each entry replaces the game `AudioClip` named `target` with the WAV `file` (a path relative to the mod folder, same
  formats as `LoadAudio`). The swap works by repointing `AudioSource.clip` on every source currently playing that clip.
- `listAudioNames: true` logs the distinct `AudioClip` names it finds (`[content] loaded AudioClips: ...`) so you can
  discover valid `target` names, just like `listTextureNames` does for textures. Enable it once, read `LogOutput.log`, then set your targets.
- The raw WAV is kept in memory and decoded the first time its target is found, so unused swaps cost nothing at boot.
- When it applies: right after each scene change and then re-checked every 2 seconds for 10 seconds, because audio sources stream in
  after a level loads.
- If two mods target the same clip, the first one wins and the other is ignored with a warning.
- **Limitation**: sounds played with `AudioSource.PlayOneShot(clip)` from a clip held in a script field are not reachable this way (there is
  no `AudioSource.clip` to repoint). If a swap seems to do nothing, that is the likely reason.

### `strings` (experimental)

- A map of exact text to replacement: any TextMeshPro label whose text equals the key is rewritten to the value.
- Applied on scene change and on the same short re-check window. Prefab/asset objects are skipped, and each pass examines at most 3000
  labels (large scenes are covered over successive passes).
- Replaced text is never replaced again, so overlapping keys and values cannot chain.
- **Experimental**: if the game re-sets a label afterwards (a language change, a label rebuilt every few seconds) the original text can reappear until the next pass.
  Only exact matches work; there is no partial or regex matching. The first mod to define a key wins.

### `ui`

A content mod can also ship windows: list [JSON layouts](ui.md#json-layouts-reference) under the manifest's top-level `"ui"`.
