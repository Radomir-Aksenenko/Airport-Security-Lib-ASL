# Troubleshooting & IL2CPP notes

This game is **Unity 6 (6000.0.72), IL2CPP, URP, Mirror networking**, modded via **BepInEx 6
IL2CPP** (Il2CppInterop + HarmonyX). A few IL2CPP-specific gotchas matter for modders.

## My mod doesn't load

Check `BepInEx\LogOutput.log`. ASL logs every step:

- `Skipping '<folder>': no manifest.json.` — add a [`manifest.json`](manifest.md).
- `manifest is missing the required 'id' field.` — add `"id"`.
- `no public AslMod subclass found.` — your class must be `public`, non-abstract, subclass
  `AslMod`, and have a parameterless constructor.
- `OnLoad threw: …` — your `OnLoad` raised an exception; the rest is the stack trace.

Each mod is isolated: one failing mod never stops the others, and `Mod loading complete: N mod(s)
active` tells you how many succeeded.

🧪 **2.0:** every mod, loaded or not, is listed with a state and a reason in **F8 > Mod Manager** and by the console
commands `mods` and `mod <id>`. The usual non-loading states and their log lines:

| State | Log line | Fix |
|---|---|---|
| `Disabled` | `Mod '...' is disabled - not loading.` | Enable it in the Mod Manager (or `enable <id>`) and restart. The switch is stored in `BepInEx/config/ASL.ModStates.json`. |
| `MissingDependency` | `Skipping mod '...': requires 'x' which is not installed` / `...but version 1.0.0 is installed` / `...which did not load (Failed: ...)` | Install, enable or update the dependency; check its `version` against the range; fix whatever made it fail. Dependents are skipped transitively. |
| `Incompatible` | `requires ASL 2.0.0 or newer (this is ASL ...)` | Update ASL, or lower `minAslVersion` if the mod does not really need it. |
| `Failed` | `duplicate mod id '...' (already used by folder '...')` | Two folders share an `id`; the first (by folder name) wins. Remove or change one. |
| `Failed` | `circular dependency: 'a' -> 'b' -> 'a'` | Break the cycle in `dependencies`. (Cycles in `loadAfter`/`loadBefore` only produce a warning.) |
| `Failed` | `invalid dependency list: ...` | A `dependencies` entry is malformed (for example `"@1.0"` or a bad range); see [version ranges](mod-manager.md#version-ranges). |
| `Failed` | `type=content but no 'content' section in manifest.` | A content mod needs a `content` object, even if it only ships `ui` (`"content": {}`). |

Dependencies and the enabled/disabled switch are evaluated once at startup, so a blocked mod can only be fixed by a restart.

## Script mod: "Could not load … Microsoft.CodeAnalysis"

The Roslyn DLLs aren't in `plugins\`. Copy `Microsoft.CodeAnalysis.dll`,
`Microsoft.CodeAnalysis.CSharp.dll`, `System.Collections.Immutable.dll`,
`System.Reflection.Metadata.dll` next to `ASL.dll`. (A source build with `DeployToGame=true` does
this automatically.)

## Script mod: compile errors

ASL logs them with IDs and line numbers:
```
[script] 'ScriptMod_…' failed to compile (2 error(s)):
    CS0246: The type or namespace name 'Foo' could not be found @ Main.cs: (5,9)
```
Fix the source and relaunch. Remember scripts can `using ASL.Api;` and game namespaces
(`Metater`, `UnityEngine`, `Mirror`, …) — the compiler references all interop assemblies.

## Texture swaps

Texture swaps work via a marshal-safe path (Unity 6 / IL2CPP can't call the span-based
`ImageConversion.LoadImage`, so ASL decodes the PNG itself and writes the pixels directly):

```
[content] 'star_gray': applied to 1 target(s)/material(s).
[content] applied 1 texture write(s)/reassignment(s).
```

How it resolves each target:
- **Readable** target texture → written in place (`Reinitialize` + `LoadRawTextureData` + `Apply`);
  all holders update.
- **Non-readable** target (most game textures) → ASL builds a replacement texture and repoints
  `Material.mainTexture`, UI `Image.sprite`, and `RawImage.texture` references to it.

If a swap reports nothing applied:
- The `target` name may be wrong — enable `"listTextureNames": true` and read the log (it shows each
  texture's `readable` flag too).
- The texture may load only in a later scene — swaps re-apply on every scene change.
- The texture may be an **atlas sub-sprite** or bound to a **non-main shader slot** — those aren't
  reassigned yet (see caveats in [mod-types](mod-types.md#content-mods)).

PNG support: 8-bit depth, non-interlaced, color types grayscale / RGB / palette / gray+alpha / RGBA.
Other PNGs log a decode error and are skipped.

## UI

🧪 *The whole toolkit is new in 2.0 and not yet verified in-game; these are the failure modes designed for.*

### A `[widget unavailable]` placeholder appears

A widget failed to build (a Unity or TextMeshPro call failed on this game build). ASL logs it **once** and shows a muted
`[button unavailable]`-style label instead, so your code and the rest of the window keep working. The placeholder accepts
every widget call and does nothing. Look for:

```
[MyMod] UI 'slider' could not be built (NullReferenceException: ...); showing a placeholder instead.
```

Report it with the stack trace. If an entire window fails you get a bare window containing `[window unavailable]`.

### My window does not show up

- Windows are **hidden until `Open()`** (or a bound toggle key, or `"open": true` in JSON). HUDs are visible immediately.
- Creating a window with an id you already used destroys the old one, so a stale reference you kept is dead.
- Opening a window closes the F8 menu, and while F8 is open windows are frozen behind it (HUDs keep running).
- A callback in your builder threw: check the log for `UI callback '...' threw`.
- ASL creates an `EventSystem` only on demand (an interactive window or dialog is open and none exists;
  `[ui] created an EventSystem (none was present).`) and removes its own if the game brings one. It heals a missing one within
  a second while a window is open (a scene change can destroy the game's). HUDs never create one.
- A window or HUD created in `OnLoad` / `OnAllModsLoaded` is built at once but appears only once the UI is Ready (first scene change, or
  after ~60 frames); `IsOpen` is true meanwhile. Windows declared in the manifest `ui` are built after Ready, so `GetWindow(id)` returns
  `null` for them during `OnLoad` / `OnAllModsLoaded`; look them up later (for example in `SceneChanged`).

### Text is invisible or the wrong font

ASL borrows the game's TextMeshPro font from its menu button, then from any live label, then falls back to TMP's default
(`[ui] game font not found; using TMP default.`). If it logs `no TMP font available yet ... toolkit text stays invisible until one is
found`, no font exists yet; ASL keeps searching periodically and it usually resolves once the main menu has been shown.

### Escape closes my window and opens the pause menu

A mod cannot consume input, so the game's own Escape handling can react to the same key press that closed an ASL window. ASL
limits this by only acting on Escape for the top-most window that opted in (`CloseOnEscape`) and never for HUDs. If it bothers your
mod, set `CloseOnEscape = false` on the window and give it a visible close button or a key. See
[ui.md](ui.md#cursor-and-input-behaviour).

### My keybind stops working while I type

While an ASL text field has keyboard focus, `IAslKeybind` (`WasPressed`, `IsHeld`, `WasReleased`, `Pressed`) and F8 are
suppressed on purpose, so typing a letter does not trigger a mod. Raw `ctx.Input.GetKey*` is not muted. Click out of the field
(or press Enter/Escape) to release focus.

### The player cannot move / the cursor is free

Any open window with `BlockGameInput`/`CaptureCursor` (default true), or a focused text field, blocks the local player and frees
the cursor; both are undone automatically when the window closes or its mod unloads. For a window that should leave the game playable use
`new UiWindowOptions { CaptureCursor = false, BlockGameInput = false }` or a HUD.

### A `Native` button does not look like the game's

`UiShape.Native` clones the game's own menu button, which ASL captures once the main menu has been shown. Until it is captured (or if it cannot
be found) the button falls back to a normal themed button. Variant and colour settings never recolour a native button.

### JSON layout problems

Layouts never throw: problems are logged as warnings with the path inside the file, and the offending field or node is skipped.

| Log line (example) | Meaning |
|---|---|
| `children[2]: unknown type 'butn'; skipped` | Typo in `type`. Valid types: `header label button toggle slider input dropdown progress image spacer separator row column grid scroll group tabs`. |
| `children[0]: unknown field 'colour'; ignored` / `field 'options' has no effect on 'button'; ignored` | Misspelt or misplaced field. |
| `field 'variant': 'Blue' is not a valid Variant (use one of: ...)` | Bad enum value (names are case-insensitive). |
| `bind 'config:General.Speed': no such config entry (bind it with ctx.Config.Bind in OnLoad first)` | Bind the entry before the layout loads. |
| `layout action 'save' is not registered (ctx.Ui.RegisterAction)` | A named action with no handler. |
| `toggleKey 'F66' is not a Unity KeyCode name ...` | See Unity's `KeyCode` list. |
| `Ui.LoadLayout: file not found` / `invalid JSON: ...` | Wrong path (relative to the mod folder) or broken JSON. |

## Config, storage and settings

- `config type X is not supported` - `Bind<T>` supports `bool`, `int`, `float`, `double`, `string`, enums and `KeyCode`.
- `config entry [S] K is already bound as Int32, not Single` - the same section/key was bound with two different types.
- The F8 **Settings** page shows an unranged `int`/`float` as read-only text (`edit in the .cfg file`); bind it with `BindRange` to get a slider.
  `double` entries are always read-only there.
- `Storage file '...' is corrupt ...; using the fallback. A copy was kept as .bad.` - the JSON was damaged; the fallback was used and
  the broken file kept next to it. Files live in `BepInEx/config/ASL/data/<modId>/`.
- `Config.Save failed` / `Storage.Save(...) failed` - usually a locked file or a read-only folder. These are logged, never thrown.

## Hot reload

- **Only script mods** can be reloaded. DLL mods need a restart; content mods have nothing to reload.
- A compile error leaves the mod `Failed` (the log has line numbers). Fix the source and save again (hot reload) or press **Reload** again.
- `manifest.json` is not re-read on reload; restart after changing dependencies, `ui` lists or ids.
- In-memory state is lost (a reload is a restart of your mod); config, storage and window positions persist.
- Harmony patches you installed stay installed; only your callbacks are replaced.
- Other mods are not reloaded and may still hold a service object you provided before the reload; re-fetch services in `OnAllModsLoaded`.
- `reload <id>` answers `'x' is a dll mod; only script mods can be reloaded` or `did not load this session` for blocked mods.

## My `Update` handler stopped running every frame

If a handler throws 10 frames in a row, ASL logs `'<mod>': Update handler keeps throwing (...); retrying with backoff, up to every 5 s` and calls it only every
0.5-5 s until it succeeds once (or the scene / local player changes). Fix the exception (often a missing null check for the player). The
**ASL** F8 page and the `perf` command flag a mod in backoff.

## Console

- The console opens with the <kbd>`</kbd> key (BackQuote); that character is not typed into the field.
- The console key is rebindable on the F8 **ASL** page (or by editing `asl/console = F10` in `BepInEx/config/ASL.Keybinds.cfg`)
  ([console.md](console.md)).
- `Unknown command 'x'. Type help.` - the command was never registered, or it clashed and you must use the qualified name
  `<shortId>.<name>` (see `help <name>`). Clashes log `Console command '...' is already registered by '...'`.
- `timescale` in multiplayer only affects your machine.

## Content: audio and strings

- An `audio` swap does nothing: enable `"listAudioNames": true` to check the exact clip name; sounds played with `PlayOneShot(clip)` from a clip held
  in a script field cannot be repointed. Look for `[content] audio: replaced the clip on N AudioSource(s)` in the log.
- `audio target '...' ignored: already replaced by '...'` / `string override for "..." ignored: already set by another mod` - two mods
  claim the same target; the first wins.
- A `strings` replacement keeps reverting: the game re-sets that label (language change, a label rebuilt periodically). The feature is
  experimental and exact-match only.
- `could not load audio ...`: ASL decodes PCM 8/16/24/32-bit and float WAV; ADPCM or MP3-in-WAV are rejected.

## Harmony hooks: "Parameter X not found" / "IL Compile Error"

Two real lessons from this build:

1. **HarmonyX matches injected parameters by name.** A patch declaring `Transform transform` for a
   method whose parameter is actually named `root` fails with
   `Parameter "transform" not found`. Use the *exact* parameter names from the IL2CPP dump, or use
   positional injection (`__0`, `__1`), or take only `__instance`.
2. **A broken patch poisons the method for everyone.** If another plugin registered a bad patch on
   a method, *your* patch on the same method fails too when HarmonyX recompiles it. ASL's
   `IModHooks.TryPostfix` isolates this: it returns `false` and logs, rather than throwing.

Prefer ASL **events** for frequent signals; use **hooks** only for specific methods, and never on
hot per-frame methods.

## Performance / stutter

ASL keeps per-frame work minimal: only the allocation-free `Update` event fires every frame; scene
and player polling run a few times a second. If you add a per-frame `Update` handler, keep it
cheap — heavy work or allocations every frame will stutter a Unity 6 game (GC pressure). This is
the single most common cause of "the game started lagging" after adding a mod.

## Native crashes (AccessViolation)

Do **not** read other objects' meshes (`Mesh.vertices`, `Mesh.bindposes`, …) — on this build those
calls can trigger a native AccessViolation that **`try/catch` cannot catch** (it's not a managed
exception). The only defense is not calling the dangerous API. General rule: be conservative with
IL2CPP APIs that return large native buffers.

## Useful facts about the game

- NPCs are built by `RandomNpcGenerator` from `NpcPartOption[]` parts (heads, hair, clothes, …)
  with per-part "allow none"/chance fields and a `femaleChance`.
- The local player is `Metater.MetaPlayer` (static `LocalPlayerInstance`, list `Instances`).
- Items are `HeldItemInteractable`; contraband detection is `ContrabandDetector`.
- Networking is Mirror (`Mirror.NetworkServer` / `NetworkManager`).
- A full IL2CPP dump is invaluable for finding exact type/method/parameter names before hooking.
