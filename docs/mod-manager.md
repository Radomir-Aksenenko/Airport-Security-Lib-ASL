# Mod manager: dependencies, load order, states and hot reload

> **Status: 🧪 Implemented, not yet verified in-game.** The dependency/ordering/version logic is covered by unit tests;
> the in-game pages and hot reload are not yet verified. Manifests that use none of the new fields load exactly as in 0.1.

## How loading works

On startup ASL scans `mods/` and loads in three phases:

1. **Read every manifest** (folders are processed in case-insensitive name order). A folder with no `manifest.json` is skipped with a log line; a
   manifest with invalid JSON or no `id` is skipped too, but is listed in the Mod Manager as `Failed` with the reason.
2. **Plan**: apply the enabled/disabled switch, `minAslVersion`, dependencies and ordering preferences, and compute one
   deterministic load order. Every mod that will *not* load gets a state and a reason now.
3. **Load** the survivors in that order, each one isolated (a mod that throws is logged, marked `Failed`, and skipped).

After everything is loaded, each code mod's `OnAllModsLoaded()` runs, then the `AllModsLoaded` events are raised
(`ctx.Events.AllModsLoaded`, `ctx.Mods.AllModsLoaded`).

If the planner itself ever failed, ASL would log it and fall back to plain folder order rather than load nothing.

### Manifest fields that matter here

(Full list: [manifest.md](manifest.md).)

```json
{
  "id": "com.you.shop",
  "version": "1.2.0",
  "dependencies":         [ "com.you.economy@>=1.2.0", "com.you.core" ],
  "optionalDependencies": [ "com.you.fancyui" ],
  "loadAfter":            [ "com.other.mod" ],
  "loadBefore":           [ "com.another.mod" ],
  "minAslVersion": "2.0.0",
  "tags": [ "economy" ],
  "hotReload": true
}
```

| Field | Effect |
|---|---|
| `dependencies` | **Hard**: they load first, and if one is missing, disabled, failed or has the wrong version this mod is skipped (`MissingDependency`), transitively. |
| `optionalDependencies` | **Soft**: loaded first when present (and the version fits), ignored when absent. |
| `loadAfter` / `loadBefore` | Ordering preferences only; ignored for mods that are not installed. |
| `minAslVersion` | ASL version required; older ASL marks the mod `Incompatible`. |
| `tags` | Free-form labels shown in the Mod Manager. |

Dependency entries are `"modId"` (any version) or `"modId@<range>"`.

### Version ranges

| Range | Accepts |
|---|---|
| `>=1.2.0`, `>1.2.0`, `<2.0.0`, `<=2.0.0` | comparisons |
| `=1.2.0` | exactly that version |
| `^1.2.3` | compatible: `>=1.2.3 <2.0.0` (`^0.2.3` = `>=0.2.3 <0.3.0`; `^0.0.3` = exactly `0.0.3`) |
| `~1.2.3` | `>=1.2.3 <1.3.0` |
| `1.2.3` (bare) | same as `>=1.2.3` |
| `*` or empty | anything |
| `>=1.0.0 <2.0.0` | several, separated by spaces: all must hold |

Versions are `major.minor.patch[-prerelease][+build]`, lenient (`1.2` = `1.2.0`, a leading `v` and a 4th number are
accepted). Pre-releases sort below the release (`1.0.0-beta.1 < 1.0.0`) and, as in npm, only satisfy a range that itself
names a pre-release of the same `major.minor.patch`. The same parser is public: `SemVersion` and `VersionRange` in `ASL.Api`
(for example `VersionRange.Parse("^1.2.0").Satisfies("1.5.1")`).

### Load order

The order is a topological sort of: hard dependencies, present optional dependencies, `loadAfter` and `loadBefore`.
Ties are broken by **folder name** (case-insensitive, then ordinal), so the order is the same on every machine.

- A cycle made only of ordering preferences is broken (a warning is logged), never fatal.
- A cycle in **hard dependencies** fails every mod in the cycle (`Failed`: `circular dependency: 'a' -> 'b' -> 'a'`).
- Duplicate ids: the first folder keeps the id; later ones are `Failed` (`duplicate mod id ...`).
- A malformed `dependencies` entry (for example `"@1.0"` or a bad range) fails that mod; a malformed optional/ordering
  entry is ignored with a warning.

Update handlers run in load order each frame; unload runs in **reverse** load order, so dependents are torn down before
what they depend on.

## Mod states

| `ModState` | Meaning | Typical reason |
|---|---|---|
| `Loaded` | Running. | |
| `Failed` | Could not load. | Invalid manifest, no `id`, compile error, no `AslMod` subclass, `OnLoad` threw, duplicate id, dependency cycle. |
| `Disabled` | Switched off by the player. | Mod Manager / `disable <id>`, stored in `ASL.ModStates.json`. |
| `MissingDependency` | A hard dependency is absent. | Not installed, disabled, failed to load, or version outside the required range. |
| `Incompatible` | Needs a newer ASL. | `minAslVersion` greater than the running ASL. |

Read states from code with `ctx.Mods.All` / `ctx.Mods.Get(id)` (`IModInfo`: `Id`, `Name`, `Version`, `Author`,
`Description`, `Kind`, `Directory`, `State`, `Error`, `Dependencies`, `Tags`), or from your own mod's `ctx.Info`.

## Enabling and disabling

Switch a mod off without deleting its folder: **F8 > Mod Manager > Disable**, or the console `disable <id>`. The choice is stored in
`BepInEx/config/ASL.ModStates.json`, a flat map such as `{ "com.you.shop": false }` (a missing entry means enabled; you can
edit it by hand). The file is read **once at startup**, so the change takes effect on the **next launch**; the Mod Manager
shows "Will be disabled after restarting the game." until then. Disabling a mod that others depend on makes them
`MissingDependency` next launch.

## The Mod Manager page (F8 > Mod Manager)

One box per mod: name, version and author; kind (`dll mod`, `script mod`, `content mod`); a coloured state badge; the
error text for anything not `Loaded`; the description; `Requires: ...` for dependencies; the pending-restart notice; and
buttons **Enable / Disable** and, for script mods that can be reloaded, **Reload**. A summary line counts the states.
(ASL itself is not listed.)

## The ASL page (F8 > ASL)

ASL version, live FPS and frame time, the per-mod cost table (`Update` / `LateUpdate` / timers, averaged over one second,
with backoff flagged), a **UI demo** button, **Reset window positions** (forget dragged window positions) and pointers to the docs and log.
The F8 root lists these two rows above your mods and paginates (`< Prev`, `Next >`) when there are more mods than fit the screen.

## Hot reload (script mods)

Reload a script mod without restarting the game:

- **Mod Manager > Reload**, or console `reload <id>`.
- Or automatically: add `"hotReload": true` to the manifest. ASL checks the mod's `.cs` files (names, sizes, modified times) once a second and reloads
  after a change has settled for a full poll (editors often save in several steps). A compile error is logged, the mod stays `Failed`, and you can fix it
  and save again. It is only retried after the next edit.

What a reload does: your `OnUnload()` runs (while services still work), then everything your mod created is released (event
handlers, timers, windows and HUDs, console commands, hook callbacks, keybind handlers, cross-mod services and
subscriptions, config/localization subscriptions, cached assets). The sources are recompiled into a fresh in-memory assembly, a new instance gets a **fresh** context,
`OnLoad` runs, then `OnAllModsLoaded` and `AllModsLoaded` for the new instance. Your manifest `ui` layouts are rebuilt.

Limits:

- **Script mods only.** DLL mods cannot be unloaded from the runtime; content mods have nothing to reload.
- Saved data survives (config file, storage, remembered window positions) but **in-memory state is lost**: a
  reload is a restart of your mod.
- `manifest.json` is **not re-read**; changing the manifest (new dependencies, a new `ui` list) needs a game restart.
- Other mods are not reloaded. A mod that cached a service you `Provide`d keeps the old object: re-fetch
  services in `OnAllModsLoaded` and do not hold them forever, or publish a topic when you reload.
- Harmony patches installed through `ctx.Hooks` stay in place (they are shared); only your callbacks are removed
  and re-added by the new `OnLoad`.
- A mod that is blocked this session (Disabled, MissingDependency, Incompatible, duplicate) cannot be reloaded; fix it and restart.

## Inter-mod communication

`ctx.Mods` (`IAslMods`) lets mods find and talk to each other. Everything you provide or subscribe to is released
automatically when your mod unloads or reloads.

```csharp
// Mod A (publishes). Declare nothing special; load order only matters for when the service appears.
public override void OnLoad(IModContext ctx)
{
    ctx.Mods.Provide<Func<string, int>>(item => PriceOf(item));       // a service
    ctx.Mods.Publish("shop.opened", "Duty free");                      // fire a message
}

// Mod B (consumes). Add "com.you.shopA" to its manifest dependencies so it loads after A.
private IModContext _ctx;
private Func<string, int> _price;

public override void OnLoad(IModContext ctx)
{
    _ctx = ctx;
    if (ctx.Mods.IsLoaded("com.you.shopA")) ctx.Log.Info("shop present");
    IDisposable sub = ctx.Mods.Subscribe("shop.opened", payload => ctx.Log.Info("opened: " + payload));
    ctx.Mods.AllModsLoaded += () => ctx.Log.Info("everything is loaded");
}

public override void OnAllModsLoaded()
{
    _price = _ctx.Mods.GetService<Func<string, int>>();               // null if nobody provides it
}
```

| Member | Meaning |
|---|---|
| `All`, `Get(id)`, `IsLoaded(id)` | Inspect installed mods and their [states](#mod-states). |
| `Provide<T>(service)` | Publish an object under the type `T` (the type argument, so `Provide<IFoo>(x)` is found as `IFoo`). |
| `GetService<T>(fromModId = null)` | The first provider of exactly `T`, or that of the named mod; `null` if none. |
| `Publish(topic, payload)` | Deliver to every subscriber, synchronously on the main thread. A throwing handler is logged and does not affect the others. |
| `Subscribe(topic, handler)` | Returns an `IDisposable`; dispose it to unsubscribe. |
| `AllModsLoaded` | Raised once after every mod loaded. Same moment as `AslMod.OnAllModsLoaded()` and `ctx.Events.AllModsLoaded`. |

Guidance:

- During `OnLoad` a service from a mod that loads **later** is not there yet. Use `dependencies` to guarantee order, or
  fetch services in `OnAllModsLoaded`.
- Service lookup matches the **exact** type argument. Both sides must agree on `T`. Types from `ASL.API` and the .NET
  base library (`Func<>`, `Action<>`, `string`, `Dictionary<,>`) always work. A custom interface only works if both mods see
  the **same loaded assembly** for it; sharing interfaces between separately compiled DLLs or script mods has not been verified in-game, so prefer BCL types or
  `Publish` with simple payloads when in doubt.
- Treat payloads as untrusted: check types before casting.
- Guard optional integrations with `IsLoaded` or a null check so your mod works without the other.
- Gate new-ASL features with `AslVersion.AtLeast("2.0.0")` (see [upgrading](upgrading-to-2.0.md)).
