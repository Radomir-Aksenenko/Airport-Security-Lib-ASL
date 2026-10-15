# Upgrading to ASL 2.0

> **Status: 🧪 the 2.0 features are implemented but not yet verified in-game.** The backward-compatibility claims below
> are enforced by tooling at build time (see [how it is guaranteed](#how-compatibility-is-guaranteed)); the new runtime
> behaviour has not yet been seen in the real game.

## TL;DR

**Nothing breaks.** A mod built for ASL 0.1 (DLL, script or content) loads and runs on 2.0 unchanged: you do not need to
recompile, edit the manifest, or change a line of code. Everything in 2.0 is *additive*: new services on `ctx`, new
manifest fields that default to "off", new F8 rows. Adopt what you want, when you want.

## How compatibility is guaranteed

- **No public member of `ASL.API` was removed, renamed or changed.** 2.0 only adds. `tools/compile-check` builds
  `ASL.API` from the `main` branch and from the current branch and compares them with Microsoft's ApiCompat tool;
  any breaking change fails the check (adding members to interfaces ASL itself implements, such as `IModContext`, is allowed,
  because mods only *consume* them). See [building](building.md#compile-check-without-the-game).
- **The old samples are regression tests.** `HelloMod`, `FunPanel`, `PropHunt`, `ExampleScriptMod` and
  `ExampleContentMod` are deliberately not edited by 2.0 work; the compile-check builds them (and compiles the script
  mod through the same path the loader uses) against the new API on every run.
- **Assembly binding**: `ASL.API.dll` is version `2.0.0.0`; .NET binds a mod's reference to `0.1.0.0` to it.
- **Manifests**: every new field is optional and parsed leniently. A wrong JSON shape (say `"icon": 5` or `"hotReload": "maybe"`) is
  ignored instead of failing the manifest, so an old manifest that happened to use a new field name for something
  else still loads. A manifest with none of the v2 fields loads in folder order with the same log lines as before.
- **`AslMod`** gained only an empty virtual method (`OnAllModsLoaded`); existing subclasses are unaffected.
- **Unit tests** cover manifest parsing, dependency ordering, version ranges and the other pure logic.

## Behaviour differences worth knowing

None of these change what an existing mod's code does, but you may notice them:

| Area | 0.1 | 2.0 |
|---|---|---|
| `Update` / `LateUpdate` handlers | One bus multicast: a throwing handler could stop the handlers after it. | Each mod's handlers run in **their own try/catch**, in mod load order. After 10 failing frames in a row a mod is retried with backoff (0.5 s up to 5 s) and logs at most one line per retry; any success, scene change or player change puts it back on every frame. See [timers & events](timers-and-events.md#per-mod-isolation-and-backoff). |
| Per-mod cost | Not measured. | Measured per mod (shown in F8 > ASL and by `perf`). |
| F8 root | One button per mod. | Two new rows above the mod list: **Mod Manager** and **ASL**. When the list is longer than the screen it paginates (`< Prev` / `Next >`). Old `IModMenu` rows render exactly as before. |
| F8 and windows | n/a | Opening an ASL window or dialog **closes the F8 menu** (it draws above windows). |
| Typing | Keybinds always fired. | While an ASL text field has focus, mod keybinds (`IAslKeybind`) and F8 are suppressed. Raw `ctx.Input.GetKey*` still reads the key. |
| Load order | Folder order. | Dependency-aware, with case-insensitive folder-name order as the tie-break, so mods with no `dependencies`/`loadAfter`/`loadBefore` simply load in folder-name order (identical on every machine). |
| Unload | `OnUnload` was not reliably called on exit. | `OnUnload` runs in reverse load order, also from the application-quit message (best effort), and everything a mod created through its context is released. |
| Failed dependencies | n/a | A mod whose hard dependency failed to load is skipped (`MissingDependency`) instead of running half-working. |
| Version in the log | `v0.1.0` | `v2.0.0` (and `AslInfo.Version`). |
| Escape | n/a | Escape closes the top-most ASL window. The game's pause menu may react to the same key press (see [troubleshooting](troubleshooting.md#escape-closes-my-window-and-opens-the-pause-menu)). |
| Texture swaps, hooks, keybinds, networking | | Unchanged. |

New files ASL may create in `BepInEx/config/`: `ASL.ModStates.json` (only when a mod is disabled), `ASL.Windows.json` (when a window is dragged),
`<modId>.cfg` and `ASL/data/<modId>/` (only for mods that use config/storage).

## Adopting the new features

Start small. Each item is independent.

| You currently... | Consider | Docs |
|---|---|---|
| Write your own settings file | `ctx.Config.Bind(...)`: persisted, typed, editable in game on an automatic Settings page. | [config](config-and-storage.md) |
| Write save data with `File.WriteAllText` | `ctx.Storage.Save/Load` (atomic, per-mod folder). | [config](config-and-storage.md#storage-ctxstorage) |
| Use `ctx.Menu.AddToggle/AddSlider` for everything | Windows, HUDs, toasts and dialogs; `ctx.Menu.AddPage` for bigger F8 pages (the old rows still work). | [UI](ui.md) |
| Use `ctx.Ui.Announce` for feedback | `ctx.Ui.Toast` (kinds, stacking) and `Confirm/Prompt/Alert`. `Announce` still works. | [UI](ui.md#dialogs-toasts-and-announcements) |
| Count frames or `Time` in `Update` | `ctx.Timers.After/Every/When` and coroutines (`Wait.Seconds`...). | [timers](timers-and-events.md) |
| Debug with `Log.Info` and restarts | Console commands (`ctx.Commands.Register`); hot reload for script mods. | [console](console.md), [mod manager](mod-manager.md) |
| Hard-code `Path.Combine(ctx.ModDirectory, ...)` | `ctx.Assets` (safe paths, cached textures/sprites/audio). | [assets](assets-and-localization.md) |
| Ship English text only | `lang/*.json` + `ctx.Loc.Get`. | [assets](assets-and-localization.md#localization-ctxloc) |
| Depend on another mod informally | `dependencies` with version ranges, `ctx.Mods` services and topics. | [mod manager](mod-manager.md) |
| Run only a content mod | Add `audio` / `strings` swaps and a no-code `ui` window. | [assets](assets-and-localization.md#content-mods-v2-audio-and-strings), [UI](ui.md#no-code-ui-in-content-mods) |

### Declaring the minimum ASL version

If you use any 2.0 member, say so in your manifest so ASL 2.0+ can explain a mismatch clearly:

```json
{ "id": "com.you.mymod", "version": "1.1.0", "minAslVersion": "2.0.0" }
```

Older ASL versions do not know this field (it is ignored), so on **0.1 your mod will still start loading** and then fail
as soon as it touches a missing member (the failure is isolated and logged; nothing else is affected). If you need to ship one build to both, keep to
the 0.1 API surface, or check `AslVersion.AtLeast("2.0.0")` before using new members. Note that `AslVersion` itself is new
in 2.0, so referencing it from a mod that must also run on 0.1 would fail there too; the only way to support both is two builds
(or reflection).

For your own mod's dependencies, `dependencies` entries can name versions: `"com.you.core@>=1.2.0"`.

### Moving from the F8 menu to a window

You do not have to. If you want to: build a window with the same controls, bind it to a key, and keep
`ctx.Menu.AddWindowButton("Open", win)` so players can still find it in F8.

```csharp
// before
ctx.Menu.AddToggle("God mode", false, on => _god = on);

// after (window, persisted setting, tooltip; the F8 Settings page appears automatically)
var god = ctx.Config.Bind("General", "God", false, "Take no damage");
var win = ctx.Ui.CreateWindow("main", "My Mod");
win.AddToggle("God mode", god.Value, on => god.Value = on);
win.BindToggleKey(ctx.Input.RegisterKey("window", "Toggle window", KeyCode.F6));
ctx.Menu.AddWindowButton("Open window", win);
```

## For tool and CI authors

`tools/compile-check/build.sh` now also runs the unit tests and the API-compat check; see [building](building.md).
`dotnet new asl-dll | asl-script | asl-content | asl-ui` create 2.0 starters ([templates](../templates/README.md)).
