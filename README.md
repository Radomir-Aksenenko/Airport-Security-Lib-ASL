# ASL — Airport Security Lib

A **modding framework** for *Airport Security Sucks!*, built on **BepInEx 6 (IL2CPP)**.

ASL turns a bare BepInEx install into a friendly modding platform: drop a folder into `mods/`
and it loads. You can make three kinds of mod — **no-code content**, **runtime C# scripts**, or
**compiled C# DLLs** — all sharing one stable API for logging, game events, and hooks.

```
Airport Security Sucks/
└── mods/
    ├── MyContentMod/      manifest.json + textures      (no code)
    ├── MyScriptMod/       manifest.json + Main.cs        (compiled at runtime)
    └── MyCodeMod/         manifest.json + MyCodeMod.dll  (compiled C#)
```

> **Status: ASL 2.0 (release candidate).** The 0.1 core (loader, the three mod tiers, event bus, opt-in hooks, keybinds,
> player control and the full networking stack) is implemented and **verified in-game**. Everything new in 2.0 (a UI toolkit,
> config & storage, timers, a dev console, mod dependencies & hot reload, assets & localization, content audio/text,
> `dotnet new` templates) is implemented and compile-checked but **🧪 not yet verified in-game**; it is marked that way below
> and in each doc. **ASL 2.0 is fully backward compatible**: mods built for 0.1 run unchanged
> ([upgrading](docs/upgrading-to-2.0.md)). The public API is not yet frozen (pre-1.0 semantics, but 2.0 only adds).

## Quick links

- [Getting started](docs/getting-started.md) — install ASL, run your first mod, `dotnet new` templates
- [Build UI in minutes](docs/ui.md) — windows, buttons, layouts, themes, JSON UI for no-code mods 🧪
- [Mod types](docs/mod-types.md) — content / script / DLL, with full examples
- [manifest.json reference](docs/manifest.md) — including 2.0 dependencies, `minAslVersion`, `ui`
- [API reference](docs/api-reference.md) — `IModContext` and every service, with links to the guides below
- [Config & storage](docs/config-and-storage.md) · [Timers & events](docs/timers-and-events.md) · [Dev console](docs/console.md)
- [Mod manager: dependencies, load order, hot reload](docs/mod-manager.md) · [Assets & localization](docs/assets-and-localization.md)
- [Upgrading to 2.0](docs/upgrading-to-2.0.md) — TL;DR: nothing breaks
- [Building ASL from source](docs/building.md) — including the no-game compile-check, tests and API-compat
- [Networking (Mirror)](docs/networking.md) — `IAslNet` awareness + the message transport (`Send`/`Subscribe`)
- [Troubleshooting & IL2CPP notes](docs/troubleshooting.md)
- [Changelog](CHANGELOG.md)

## What works today

| Capability | Status | Notes |
|---|---|---|
| `mods/` folder loader | ✅ | Each mod = a folder with `manifest.json`; per-mod isolation & logging |
| **DLL mods** (`type: dll`) | ✅ | Subclass `AslMod`, ship a `.dll` |
| **Script mods** (`type: script`) | ✅ | Plain `.cs`, compiled at runtime with Roslyn — no build setup |
| **Content mods** (`type: content`) | ✅ | Texture swaps: managed PNG decode → in-place write (readable) or reassign `Material.mainTexture` / UI `Image.sprite` / `RawImage.texture` (non-readable) + texture-name discovery |
| Event bus | ✅ | `Update`, `SceneChanged`, `LocalPlayerChanged` |
| Opt-in hooks | ✅ | `IModHooks.TryPostfix(type, method, cb)` — install Harmony patches safely, on demand |
| In-game menu | ✅ | `IModMenu` — toggles / buttons / sliders in a shared overlay; opened with **F8** or the **Mods** button ASL adds to the main menu |
| On-screen UI | ✅ | `IAslUi.Announce(text, secs)` — the game's own announcement banner (used in-game) |
| Input & **keybinds** | ✅ | `IAslInput.RegisterKey(id, name, default)` → rebindable named keys that auto-appear in the F8 menu, persist to `BepInEx/config/ASL.Keybinds.cfg`, and are conflict-checked (mod↔mod rebinds blocked; game-key clashes flagged). Plus raw `GetKeyDown/GetKey/GetKeyUp`. PropHunt drives it in-game |
| Player **look & control** | ✅ | `IAslPlayer.GetLookedAt()` (camera raycast → object + net id), `Freeze`/`Unfreeze`, `Teleport`, `SetColliderSize`/`ResetCollider`. Drives the real movement controller (the levers PropHunt uses); verified in-game |
| Networking awareness | ✅ | `IAslNet` — host/client/connected state + connection-count changes (Mirror) |
| Networking **player identity** | ✅ | `IAslNet.Players`/`LocalPlayer`/`GetPlayer(connId)` + `PlayerJoined`/`PlayerLeft` — each `IAslPlayer` pairs the game `MetaPlayer` with its Mirror identity (netId, conn id, isLocal) and a name (local from Steam); verified in-game |
| Networking **synced state** | ✅ | `IAslNet.GetSync(id)` → `IAslSync` — host-authoritative shared key/value store (host `Set`, all read, late joiners get a snapshot, `Changed` event). Host→client replication verified in-game (PropHunt replicates every disguise this way) |
| Networking **object lookup** | ✅ | `IAslNet.FindObject(netId)` → resolve a spawned networked object by net id on host or client (PropHunt uses it to rebuild a prop everywhere) |
| Networking **message transport** | ✅ | `IAslNet.Send`/`SendToServer`/`Subscribe` (bytes) **and typed `Send<T>`/`Subscribe<T>`** (`IAslMessage` + `AslWriter`/`AslReader` — send objects, not bytes) host↔client on named channels, tunnelled through a Mirror message the game already ships. Full two-peer round trip confirmed in-game (`NET TEST: PASS`); see [docs/networking.md](docs/networking.md) |
| **UI toolkit** (windows, HUDs, widgets, themes, toasts, dialogs) | 🧪 | `ctx.Ui.CreateWindow/CreateHud/Toast/Confirm/Prompt/Alert`; labels, buttons (shapes × sizes × variants, native game look), toggles, sliders, inputs, dropdowns, progress, images, tabs, grids, scroll views, tooltips, live labels. Implemented, not yet verified in-game — see [docs/ui.md](docs/ui.md) |
| **JSON UI layouts** (no-code UI) | 🧪 | `ui/*.json` windows (`bind: config:…`, built-in actions `close`/`open:`/`toast:`/`command:`…), loaded by `ctx.Ui.LoadLayout` or the manifest's `"ui"` — also in **content mods**. Implemented (parser unit-tested), not yet verified in-game |
| **F8 pages & Settings** | 🧪 | `IModMenu.AddPage/AddWindowButton/AddSeparator`; every mod with `ctx.Config` entries gets an auto-generated **Settings** page. Implemented, not yet verified in-game |
| **Config** (`ctx.Config`) | 🧪 | Typed, persisted per-mod settings in `BepInEx/config/<modId>.cfg` (bool/int/float/double/string/enum/`KeyCode`, ranges). Implemented, not yet verified in-game — [docs](docs/config-and-storage.md) |
| **Storage** (`ctx.Storage`) | 🧪 | Atomic per-mod JSON/text save data in `BepInEx/config/ASL/data/<modId>/`. Implemented, not yet verified in-game |
| **Timers & coroutines** (`ctx.Timers`) | 🧪 | `After`/`Every`/`NextFrame`/`When` + coroutine-lite (`Wait.Seconds/Until/While/Frames`). Implemented, not yet verified in-game — [docs](docs/timers-and-events.md) |
| **New events + isolation** | 🧪 | `LateUpdate`, `MenuVisibilityChanged`, `AllModsLoaded`, `LanguageChanged`, `GameQuitting`; per-mod `Update` isolation with backoff and a per-mod profiler. Implemented, not yet verified in-game |
| **Dev console** (`ctx.Commands`) | 🧪 | `` ` `` opens a console: built-in commands (`help`, `mods`, `reload`, `config`, `perf`, …) and your own commands. Implemented, not yet verified in-game — [docs](docs/console.md) |
| **Manifest v2: dependencies & load order** | 🧪 | `dependencies` with version ranges, `optionalDependencies`, `loadAfter`/`loadBefore`, `minAslVersion`; deterministic order, per-mod states. Resolver unit-tested; not yet verified in-game — [docs](docs/mod-manager.md) |
| **Mod Manager & ASL pages** | 🧪 | F8 > Mod Manager (state, errors, Enable/Disable) and F8 > ASL (FPS, per-mod cost, UI demo). Implemented, not yet verified in-game |
| **Hot reload** (script mods) | 🧪 | Mod Manager *Reload*, console `reload <id>`, or `"hotReload": true`. Implemented, not yet verified in-game |
| **Inter-mod services & messages** (`ctx.Mods`) | 🧪 | `Provide`/`GetService`, `Publish`/`Subscribe`, `OnAllModsLoaded`. Implemented, not yet verified in-game |
| **Assets** (`ctx.Assets`) | 🧪 | `LoadTexture`/`LoadSprite` (PNG, 9-slice), `LoadAudio` (WAV), `LoadBundle`/`LoadBundleAsset`, safe paths. Implemented, not yet verified in-game — [docs](docs/assets-and-localization.md) |
| **Localization** (`ctx.Loc`) | 🧪 | `lang/*.json` with game-language matching and English fallback. Implemented, not yet verified in-game |
| **Content mods v2** | 🧪 | `audio` swaps (WAV), `strings` text overrides (experimental), `listAudioNames`, plus `ui`. Implemented, not yet verified in-game |
| **`dotnet new` templates** | 🧪 | `asl-dll`, `asl-script`, `asl-content`, `asl-ui` — [templates/](templates/README.md). Generated mods are not yet verified in-game |

✅ = verified in the running game. 🧪 = **Implemented, not yet verified in-game** (compiles, unit-tested where the logic is pure, but
not run in the game yet). Dev tooling that is not an in-game feature: `tools/compile-check` type-checks everything without the game,
runs the unit tests and checks that `ASL.API` stays backward compatible ([docs/building.md](docs/building.md#compile-check-without-the-game)).

## The 30-second mod

A complete DLL mod:

```csharp
using ASL.Api;

public sealed class MyMod : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        ctx.Log.Info("MyMod loaded!");
        ctx.Events.SceneChanged += scene => ctx.Log.Info($"Now in scene: {scene}");
    }
}
```

…or skip the build entirely and drop the same code as `Main.cs` in a **script mod**. See
[Mod types](docs/mod-types.md).

### The 30-second UI 🧪

A window with a header and a button, toggled by a rebindable key (new in 2.0; implemented, not yet verified in-game):

```csharp
using ASL.Api;
using UnityEngine;

public sealed class MyUiMod : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        var win = ctx.Ui.CreateWindow("main", "My Mod");                 // hidden until opened
        win.AddHeader("Hello");
        win.AddButton("Say hi", () => ctx.Ui.Toast("Hi!", UiToastKind.Success), UiButtonStyle.Pill(UiVariant.Success));
        win.BindToggleKey(ctx.Input.RegisterKey("window", "Toggle window", KeyCode.F6));
    }
}
```

…or with **no code at all**, a content mod whose manifest lists `"ui": ["ui/main.json"]`:

```json
{ "id": "main", "title": "My Mod", "toggleKey": "F6",
  "children": [
    { "type": "header", "text": "Hello" },
    { "type": "button", "text": "Say hi", "shape": "Pill", "variant": "Success", "onClick": "toast:Hi!" } ] }
```

Start from a template with `dotnet new asl-dll | asl-script | asl-content | asl-ui` ([templates](templates/README.md)),
and read [Build UI in minutes](docs/ui.md).

## What you can build right now

With today's API you can already make, for example:

- **Reskins / retextures** — a no-code content mod that swaps game textures for your own PNGs
  (UI icons, materials, …): just a folder with `manifest.json` + images.
- **Per-frame behaviour** — subscribe to `Events.Update` to read or tweak game state every frame
  (the workhorse: drive logic, automate actions, build overlays).
- **React to the game's flow** — `Events.SceneChanged` (entered a level/menu) and
  `Events.LocalPlayerChanged` (your player spawned/despawned) let a mod kick in at the right moment.
- **React to specific game actions** — `Hooks.TryPostfix(type, "Method", …)` runs your code after a
  chosen game method, so you can respond to game logic without writing Harmony yourself.
- **Multiplayer mods** — `ctx.Net` tells you host/client/connection state, gives you player identity
  and a host-authoritative synced store, and the message transport (`Net.Send` / `Net.SendToServer` /
  `Net.Subscribe`, bytes or typed objects) lets a mod talk host↔client on named channels. PropHunt is
  a full multiplayer game mode built on it — see [networking](docs/networking.md).
- **Throwaway experiments** — drop a `.cs` script into `mods/` and iterate with no build setup.
- **Quality-of-life & debug tools** — logging, on-screen info, value tweaks and automation built on
  the events above.

And with the 2.0 toolkit (🧪 implemented, not yet verified in-game):

- **Full mod UIs** — windows, HUDs, toasts and dialogs with themed buttons, sliders, tabs and scroll views,
  in code or as a JSON file ([ui](docs/ui.md)).
- **Mods with real settings and save data** — `ctx.Config` shows up in an auto-generated F8 Settings page;
  `ctx.Storage` keeps progress ([config & storage](docs/config-and-storage.md)).
- **Timed and scripted behaviour** — delays, intervals and coroutines ([timers](docs/timers-and-events.md)).
- **Debug commands** — register commands in the dev console, hot-reload script mods while the game runs
  ([console](docs/console.md), [mod manager](docs/mod-manager.md)).
- **Mod ecosystems** — dependencies with version ranges, shared services and messages between mods.
- **Sound and text packs** — a no-code content mod can swap audio clips and on-screen text, and ship a window.
- **Localized mods** — `lang/*.json` with automatic language matching ([assets & localization](docs/assets-and-localization.md)).

Every mod is isolated: a broken one is logged and skipped, not crashed into the game.

## Building ASL

Requires the **.NET SDK** (6.0+, tested on 10.x) and a local install of the game with BepInEx 6
IL2CPP set up (so `BepInEx\interop\` is populated).

```pwsh
dotnet build ASL.slnx -c Release
```

`GamePath` defaults to a Steam install; override it for yours:

```pwsh
dotnet build ASL.slnx -c Release /p:GamePath="C:\Path\To\Airport Security Sucks!"
```

The build copies `ASL.dll` + its dependencies into `<GamePath>\BepInEx\plugins\` and the UiShowcase sample
into `<GamePath>\mods\UiShowcase\` (PropHunt only with `/p:DeploySamples=true`). Disable copying with
`/p:DeployToGame=false`. Full details in [docs/building.md](docs/building.md).

No game on this machine (Linux, CI)? `tools/compile-check/build.sh` type-checks everything against stub game
assemblies, runs the unit tests and verifies that `ASL.API` stays backward compatible. It is an approximation: a pass
is not proof the game works.

## Repository layout

```
ASL.slnx                     solution (SDK 10 .slnx format)
Directory.Build.props        shared build settings (GamePath, target framework, …)
src/ASL.API/                 the stable public contract mods compile against (ASL.API.dll)
src/ASL/                     the framework implementation (ASL.dll)
samples/HelloMod/            DLL mod example (the 0.1 minimal mod; also a backward-compat regression test)
samples/FunPanel/            DLL mod: F8 menu that drives the real player (speed/coffee/jump/ragdoll)
samples/PropHunt/            DLL mod: multiplayer Prop Hunt (B = disguise, N = freeze, left-click to catch)
samples/ExampleScriptMod/    script mod example
samples/ExampleContentMod/   content mod example (texture swap)
samples/UiShowcase/          DLL mod (2.0): guided tour of the UI toolkit, config, storage, console, localization
samples/ExampleUiContentMod/ content mod (2.0): a window declared in JSON, no code
templates/                   `dotnet new` template pack: asl-dll, asl-script, asl-content, asl-ui
tests/                       unit tests for the pure logic (versions, dependencies, layouts, scheduler, WAV, ...)
tools/compile-check/         no-game compile check: stub interop + tests + API-compat (for Linux/CI)
.github/workflows/           CI: runs the compile-check
docs/                        documentation
CHANGELOG.md                 release history
```

## Roadmap

Rough order of what's next:

- **Multiplayer (Mirror) message transport** — done and verified in-game (`IAslNet.Send`/
  `SendToServer`/`Subscribe`, bytes or typed objects, host↔client on named channels;
  see [docs/networking.md](docs/networking.md)). Next on top: an unreliable/unordered channel option.
  The higher-level **RPC helper** was considered for 2.0 and is deferred.
- **Verify 2.0 in-game** — the 🧪 features above (UI toolkit, config/storage, timers, console, mod manager,
  hot reload, assets, localization, content audio/text) are implemented and compile-checked; the next step is
  running them in the game and flipping each ✅ once `BepInEx\LogOutput.log` confirms it.
- **Richer content** — atlas sub-sprites and non-main shader slots for texture swaps; prefab/value tweaks
  driven from the manifest (audio and text swaps landed in 2.0, 🧪).
- **More built-in game events** — NPC spawned, round start/end, contraband scans — surfaced through
  the event bus (built on the opt-in hook system). These need a **game dump** (exact type and method names),
  so they are not part of 2.0; until then use `ctx.Hooks.TryPostfix`.
- **Stable 1.0 API** — freeze `ASL.API`, ship a reference/NuGet package for mod authors, adopt semver.
  (2.0 already enforces "only add, never break" with the API-compat check.)
- **Distribution** — a Thunderstore package so players install ASL and mods in one click.

## License

**MIT** — see [LICENSE](LICENSE). Use, modify, fork, and redistribute ASL freely (including in
closed-source mods); just keep the copyright notice. MIT is the de-facto standard for BepInEx mod
libraries, so other modders can depend on ASL without legal friction.

ASL is an unofficial, fan-made modding library and is **not affiliated with the developers of
Airport Security Sucks!**. The MIT license covers ASL's own source only — not the game or its
assets, which remain under their own terms.

## Star History

<a href="https://www.star-history.com/?repos=Radomir-Aksenenko%2FAirport-Security-Lib-ASL&type=date&legend=top-left">
 <picture>
   <source media="(prefers-color-scheme: dark)" srcset="https://api.star-history.com/chart?repos=Radomir-Aksenenko/Airport-Security-Lib-ASL&type=date&theme=dark&legend=top-left" />
   <source media="(prefers-color-scheme: light)" srcset="https://api.star-history.com/chart?repos=Radomir-Aksenenko/Airport-Security-Lib-ASL&type=date&legend=top-left" />
   <img alt="Star History Chart" src="https://api.star-history.com/chart?repos=Radomir-Aksenenko/Airport-Security-Lib-ASL&type=date&legend=top-left" />
 </picture>
</a>
