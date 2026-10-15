# Changelog

All notable changes to **ASL — Airport Security Lib** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); ASL is pre-1.0, so the public API
(`ASL.API`) may still change between minor versions until 1.0.

## [2.0.0] — Unreleased

ASL 2.0 is **fully backward compatible with 0.1**: no public member of `ASL.API` was removed or changed (enforced by an
ApiCompat check against `main`), the 0.1 samples are kept as regression tests, and every new manifest field is optional. See
[docs/upgrading-to-2.0.md](docs/upgrading-to-2.0.md).

> **Status: 🧪 everything below is implemented and compile-checked but has not been verified in-game yet.** Features are
> only promoted to "working" in the docs after `BepInEx\LogOutput.log` confirms them. Pure logic (versions, dependency
> ordering, layout parsing, console tokenizer, scheduler, WAV/language/storage-name helpers, manifest parsing) is covered by unit tests.

### Added

- **UI toolkit** (`ctx.Ui`, [docs/ui.md](docs/ui.md)): `CreateWindow`, `CreateHud`, `GetWindow`, `Toast`, `Confirm`, `Prompt`,
  `Alert`, `Theme`, `IsTextInputFocused`. Widgets: labels (`UiTextStyle`), live labels, buttons, icon buttons, toggles (switch /
  checkbox / button), sliders, text inputs, dropdowns, progress bars, images, keybind rows, spacers, separators; containers: row,
  column, grid, scroll view, group, tabs. Every element has `Id`, `Visible`, `Interactable`, `Tooltip`, `SetSize`, `SetFlexible`.
  - Buttons: `UiShape` (Rect, Rounded, Pill, Circle, Square, Native game clone) × `UiSize` (32/44/60/76 px) × `UiVariant`
    (Primary, Secondary, Success, Danger, Warning, Ghost, Outline), custom width/height/colours/icon via `UiButtonStyle`.
  - Themes: `UiTheme` presets Default, Dark, Light, Game, HighContrast, or build your own; `UiWindowOptions` for size, anchor, modal,
    draggable, cursor/input capture, escape-to-close, layer order.
  - Windows are drawn by one driver that polls only open windows; shapes are generated procedurally; the game's own TextMeshPro font is used.
  - Cursor and input handling: windows free the cursor and block player input; a focused text field mutes mod keybinds and F8;
    Escape closes the top-most window; windows remember where they were dragged (`BepInEx/config/ASL.Windows.json`); opening a window closes F8.
  - Toasts (stacked, top-right, max 5) and modal dialogs; widgets that fail to build are replaced by an inert `[widget unavailable]` placeholder.
- **JSON UI layouts** (`ui/*.json`): pure, unit-tested `LayoutParser` + `LayoutBuilder`; `ctx.Ui.LoadLayout`, `RegisterAction`
  (with and without a value); the manifest's top-level `ui` loads layouts for any mod kind, **content mods included**. Built-in actions
  `close`, `open:`, `toggle:`, `toast:`, `announce:`, `command:`; `"bind": "config:Section.Key"` two-way config binding; `toggleKey`
  registers a rebindable keybind. Unknown types/fields are warnings, never errors.
- **F8 menu**: `IModMenu.AddPage`, `AddWindowButton`, `AddSeparator`; built-in **Mod Manager** (state badge, error, description,
  dependencies, Enable/Disable, script Reload) and **ASL** (version, FPS, per-mod cost, UI demo, reset window positions) pages above the
  unchanged mod list; the root list paginates when there are more mods than fit.
- **Config** (`ctx.Config`, [docs](docs/config-and-storage.md)): typed, persisted per-mod settings (`Bind`, `BindRange`, `ShowInMenu`,
  `Save`, `Reload`, `Changed`) in `BepInEx/config/<modId>.cfg`; an automatic **Settings** page in F8 (toggle / slider / dropdown /
  text / key capture / reset to defaults).
- **Storage** (`ctx.Storage`): atomic per-mod JSON and text save data in `BepInEx/config/ASL/data/<modId>/`, sanitised names.
- **Timers** (`ctx.Timers`, [docs](docs/timers-and-events.md)): `After`, `Every`, `NextFrame`, `When`, `CancelAll` and coroutine-lite
  (`Start` + `Wait.Seconds/Until/While/Frames`); allocation-free when idle, cancelled on unload.
- **Events**: `LateUpdate`, `MenuVisibilityChanged`, `AllModsLoaded`, `LanguageChanged`, `GameQuitting`; `AslMod.OnAllModsLoaded()`.
- **Profiler**: per-mod `Update` / `LateUpdate` / timer cost, in F8 > ASL and the console `perf` command.
- **Dev console** (`ctx.Commands`, [docs](docs/console.md)): `` ` `` opens it; tokenizer with quotes and `;`, history, Tab completion, a 500-line
  buffer, optional BepInEx log mirror, ~20 built-ins (`help`, `mods`, `mod`, `reload`, `enable`, `disable`, `config`, `perf`, `ui.demo`,
  `timescale`, …); mods register commands with qualified `<shortId>.<name>` fallbacks.
- **Mods** (`ctx.Mods`, `ctx.Info`, [docs](docs/mod-manager.md)): `IModInfo` / `ModState`, installed-mod list, cross-mod services
  (`Provide` / `GetService`) and topics (`Publish` / `Subscribe`).
- **Manifest v2** (optional fields): `dependencies` and `optionalDependencies` with version ranges, `loadAfter`, `loadBefore`,
  `minAslVersion`, `tags`, `website`, `icon`, `ui`, `hotReload`. Three-phase loading with a deterministic dependency-aware order;
  per-mod states `Loaded` / `Failed` / `Disabled` / `MissingDependency` / `Incompatible`; enable/disable persisted in
  `BepInEx/config/ASL.ModStates.json` (applies next launch); duplicate ids and dependency cycles fail the affected mods.
- **Hot reload** for script mods: Mod Manager *Reload*, console `reload <id>`, or `"hotReload": true` (sources polled once a second).
- **Versioning helpers**: `AslVersion` (`Current`, `AtLeast`), `SemVersion`, `VersionRange` (`>=` `>` `<` `<=` `=` `^` `~`, bare, `*`).
- **Assets** (`ctx.Assets`) and **localization** (`ctx.Loc`, [docs](docs/assets-and-localization.md)): safe-path file access, cached PNG
  textures/sprites (9-slice), managed WAV decode to `AudioClip`, AssetBundles; `lang/*.json` with game-language matching and English fallback.
- **Content mods v2**: `content.audio` (WAV clip swaps), `content.strings` (experimental exact-text overrides), `content.listAudioNames`.
- **Templates** (`templates/`): `dotnet new asl-dll | asl-script | asl-content | asl-ui`; `tools/compile-check` also compiles them and validates the template JSON.
- **Samples**: `UiShowcase` (DLL, in the solution and deployed by the default build) and `ExampleUiContentMod` (no-code UI).
- **Tooling**: `tools/compile-check` (real BepInEx core + stub interop; compiles ASL, samples and script mods without the game,
  runs the unit tests, and checks `ASL.API` for breaking changes against `main`), `tests/ASL.Tests` and `tests/ASL.Data.Tests`, and a CI workflow.
- **Docs**: UI guide, config & storage, timers & events, console, mod manager, assets & localization, upgrading to 2.0; the API reference,
  manifest, mod types, getting started, building and troubleshooting pages updated.

### Changed

- Version is `2.0.0` (`Directory.Build.props`, `AslInfo.Version`, `AslVersion.Current`).
- `ctx.Events` is a per-mod view of the bus: each mod's `Update` / `LateUpdate` handlers run in their own try/catch in mod load order and
  are timed. A mod that throws 10 frames in a row is retried with backoff (0.5 s … 5 s, one log line per retry) and returns to every
  frame on the first success or on a scene / player change; it is never switched off permanently.
- Mods load in a dependency-aware order (ties by folder name); manifests with no v2 fields load in folder order with the same log lines
  as before. A mod whose hard dependency failed to load is skipped (`MissingDependency`).
- Mods unload in reverse load order; `GameQuitting` and `OnUnload` also run from the application-quit message (best effort).
- Unload, failed `OnLoad` and hot reload release everything a mod created through its context (event handlers, timers, windows and HUDs,
  commands, hook callbacks, keybind `Pressed` handlers, cross-mod services and subscriptions, config/localization subscriptions, cached
  assets: bundles unloaded, textures/sprites/clips destroyed) and clear its F8 rows.
- Opening a window or dialog closes the F8 menu; while an ASL text field has focus, `IAslKeybind` and F8 are suppressed (raw
  `ctx.Input.GetKey*` is unchanged).
- Manifest fields `icon`, `website`, `minAslVersion`, `hotReload`, `ui`, the dependency lists and `content.*` tolerate a wrong JSON shape
  (ignored / default) instead of failing the whole manifest. `hotReload` accepts `true`/`false`/`"true"`/`1`.
- `SemVersion` accepts a 4th numeric component (`1.0.0.0`). `VersionRange` follows npm pre-release semantics: a pre-release only satisfies a range
  that names a pre-release of the same `major.minor.patch`, so `^1.0.0` does not accept `2.0.0-beta.1`.
- Storage / config file names that had to be sanitised get a short stable hash suffix (`a/b` becomes `a_b~a22d4fe9`), so `a/b` and `a_b` never share a file;
  already-clean names are unchanged.
- WAV `data` chunks of size 0 use the rest of the file. Content audio swaps keep the raw bytes and decode on first use; string overrides skip
  prefabs/assets and cap work per pass. `MenuVisibilityChanged` is raised after the native menu has reacted.
- `docs/building.md`: the solution now contains `UiShowcase`; `tests/` and `templates/` are intentionally outside it.

- **UI readiness**: windows/HUDs opened during `OnLoad`/`OnAllModsLoaded` are built at once and shown once the UI is Ready (first scene change or
  ~60 frames); `IsOpen` is true meanwhile. Manifest `ui` windows are built after Ready (look them up later, e.g. on `SceneChanged`).
- **EventSystem** is created only on demand (interactive host open, none exists) and ASL removes its own when the game brings one; HUDs never create one.
- `ctx.Net` is a per-mod wrapper: `GetSync(id)` returns a per-mod wrapper (same instance per id); Net subscriptions and event handlers are removed on unload / hot reload.
- `LanguageChanged` can fire once shortly after load if the detected language differs from the startup one.
- The console key is rebindable on the F8 **ASL** page; saved key rebinds survive while a mod is disabled / not loaded.
- `log.mirror` keeps only the last 200 lines while the console is closed.
- `hotReload` also watches `ui/*.json`, `lang/*.json` and manifest-listed `ui` files.
- Content mods may ship only `ui` (no `content` object).
- Template manifests (dll, script, ui) and `ExampleUiContentMod` declare `minAslVersion: 2.0.0`.

### Fixed

- A throwing `Update` handler in one mod no longer stops the handlers of the mods after it (0.1 ran them as one multicast delegate).
- `OnUnload` / `GameQuitting` were not reliably run when the game exits (BepInEx IL2CPP does not call plugin unload); they now also run from
  the application-quit message.

### Known limitations

- None of the 2.0 features has been run in the game yet (see the status note above).
- Game-specific events (NPC spawned, round start/end, contraband scans) and an RPC helper for the networking layer are not part of 2.0.

## [0.1.0] — 2026-06-17

First public release. Everything below is implemented and verified in-game.

### Added

- **Mod loader** — drop a folder into `mods/`; each mod is a `manifest.json` plus its payload, loaded
  in isolation (a broken mod is logged and skipped, never crashed into the game).
- **Three mod tiers, one API**
  - **Content mods** (`type: content`) — no-code texture swaps via managed PNG decode (in-place write
    for readable textures, reference reassignment for non-readable ones) with texture-name discovery.
  - **Script mods** (`type: script`) — plain `.cs`, compiled at runtime with Roslyn, no build setup.
  - **DLL mods** (`type: dll`) — subclass `AslMod` and ship a compiled `.dll`.
- **Event bus** — `Update`, `SceneChanged`, `LocalPlayerChanged`.
- **Opt-in hooks** — `IModHooks.TryPostfix(type, method, cb)` installs Harmony patches safely and on
  demand, isolating a broken patch from other mods.
- **In-game menu** — `IModMenu` toggles / buttons / sliders / labels in a shared overlay, opened with
  **F8** or the **Mods** button ASL adds to the main menu. The menu frees the cursor the game's own
  way (registering as a `MetaCursor` user) and blocks player look/move while open.
- **On-screen UI** — `IAslUi.Announce(text, secs)` reuses the game's announcement banner.
- **Input & keybinds** — `IAslInput.RegisterKey(id, name, default)` for rebindable named keys that
  appear in the F8 menu, persist to `BepInEx/config/ASL.Keybinds.cfg`, and are conflict-checked
  (mod↔mod rebinds blocked; game-key clashes flagged). Plus raw `GetKeyDown/GetKey/GetKeyUp`.
- **Player look & control** — `IAslPlayer.GetLookedAt()` (camera raycast → object + net id),
  `Freeze`/`Unfreeze`, `Teleport`, `SetColliderSize`/`ResetCollider`, driving the real movement
  controller.
- **Networking (Mirror)**
  - Awareness — host/client/connected state and connection-count changes.
  - Player identity — `Players`/`LocalPlayer`/`GetPlayer(connId)` and `PlayerJoined`/`PlayerLeft`,
    pairing the game `MetaPlayer` with its Mirror identity (netId, conn id, isLocal, name).
  - Object lookup — `FindObject(netId)` resolves a spawned networked object on host or client.
  - Synced state — `GetSync(id)` → host-authoritative shared key/value store (host `Set`, all read,
    late joiners get a snapshot, `Changed` event).
  - Message transport — `Send`/`SendToServer`/`SendToAll`/`SendToClient`/`Subscribe` for bytes, plus
    typed `Send<T>`/`Subscribe<T>` (`IAslMessage` + `AslWriter`/`AslReader`), host↔client on named
    channels, tunnelled through a Mirror message the game already ships. Full two-peer round trip
    confirmed in-game.
- **Samples** — `HelloMod` (DLL template), `FunPanel` (F8 menu driving the player), `PropHunt` (a full
  multiplayer Prop Hunt game mode), plus `ExampleScriptMod` and `ExampleContentMod`.
- **Documentation** — getting started, mod types, manifest reference, API reference, building from
  source, networking, and troubleshooting.

[2.0.0]: https://github.com/Radomir-Aksenenko/Airport-Security-Lib-ASL/compare/v0.1.0...2.0
[0.1.0]: https://github.com/Radomir-Aksenenko/Airport-Security-Lib-ASL/releases/tag/v0.1.0
