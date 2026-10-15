# Changelog

All notable changes to **ASL — Airport Security Lib** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); ASL is pre-1.0, so the public API
(`ASL.API`) may still change between minor versions until 1.0.

## [Unreleased] — 2.0 hardening (not yet verified in-game)

- JSON UI layouts (`ui/*.json`): pure `LayoutParser` (`ASL.Core`, unit-tested) + `LayoutBuilder`; `ctx.Ui.LoadLayout`
  and `RegisterAction` implemented; the manifest's top-level `ui` loads layouts for any mod kind, content mods
  included (built-in actions `close`, `open:`, `toggle:`, `toast:`, `announce:`, `command:`; `bind: config:S.K`
  two-way config binding; `toggleKey` registers a rebindable keybind). A mod's windows, HUDs and bindings are
  destroyed on unload / hot reload.
- F8 root: built-in **Mod Manager** (summary, per-mod state badge / error / description / dependencies,
  Enable/Disable via `ASL.ModStates.json`, Reload for script mods) and **ASL** (version, FPS, per-mod
  Update/LateUpdate/timer cost, UI demo, reset window positions) pages above the unchanged mod list.
- F8 mod pages with `ctx.Config` entries (and `ShowInMenu`) get an auto-generated **Settings** page.

- `ctx.Events.Update` / `LateUpdate`: a throwing handler is no longer switched off after 10 failing frames; it is
  retried with backoff (0.5 s ... 5 s) and returns to every-frame on the first success or a scene / player change.
- Hot reload and unload now clear a mod's keybind `Pressed` handlers, localization/config subscriptions and cached
  assets (bundles unloaded, textures/sprites/clips destroyed). A failed `OnLoad` or reload compile also clears the
  mod's F8 rows.
- `GameQuitting` / `OnUnload` also run from the application-quit message (best effort; keep handlers quick). Mods
  unload in reverse load order. Mods whose hard dependency failed to load are skipped (`MissingDependency`).
- `SemVersion` accepts a 4th numeric component (`1.0.0.0`). `VersionRange` excludes pre-releases unless a bound is a
  pre-release of the same `major.minor.patch` (npm semantics), so `^1.0.0` no longer accepts `2.0.0-beta.1`.
- Manifest fields `icon`, `website`, `minAslVersion`, `hotReload`, `content.*` tolerate a wrong JSON shape
  (ignored / default) instead of failing the whole manifest. `hotReload` accepts `true`/`false`/`"true"`/`1`.
- Storage / config file names that had to be sanitized now get a short stable hash suffix (`a/b` -> `a_b~a22d4fe9`),
  so `a/b` and `a_b` no longer share a file; already-clean names are unchanged. WAV `data` chunks of size 0 use the
  rest of the file. Content audio swaps keep raw bytes and decode on first use; string overrides skip prefabs/assets
  and cap work per pass. `MenuVisibilityChanged` is raised after the native menu has reacted.

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

[0.1.0]: https://github.com/Radomir-Aksenenko/Airport-Security-Lib-ASL/releases/tag/v0.1.0
