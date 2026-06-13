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

> **Status:** early but functional. The loader, the three mod tiers, the event bus and the opt-in
> hook system are all implemented and verified in-game. The public API is not yet frozen (pre-1.0).

## Quick links

- [Getting started](docs/getting-started.md) — install ASL, run your first mod
- [Mod types](docs/mod-types.md) — content / script / DLL, with full examples
- [manifest.json reference](docs/manifest.md)
- [API reference](docs/api-reference.md) — `IModContext`, events, hooks
- [Building ASL from source](docs/building.md)
- [Networking (Mirror)](docs/networking.md) — `IAslNet`, and the message-transport plan
- [Troubleshooting & IL2CPP notes](docs/troubleshooting.md)

## What works today

| Capability | Status | Notes |
|---|---|---|
| `mods/` folder loader | ✅ | Each mod = a folder with `manifest.json`; per-mod isolation & logging |
| **DLL mods** (`type: dll`) | ✅ | Subclass `AslMod`, ship a `.dll` |
| **Script mods** (`type: script`) | ✅ | Plain `.cs`, compiled at runtime with Roslyn — no build setup |
| **Content mods** (`type: content`) | ✅ | Texture swaps: managed PNG decode → in-place write (readable) or reassign `Material.mainTexture` / UI `Image.sprite` / `RawImage.texture` (non-readable) + texture-name discovery |
| Event bus | ✅ | `Update`, `SceneChanged`, `LocalPlayerChanged` |
| Opt-in hooks | ✅ | `IModHooks.TryPostfix(type, method, cb)` — install Harmony patches safely, on demand |
| In-game menu (F8) | ✅ | `IModMenu` — mods register toggles / buttons / sliders into a shared overlay |
| Networking awareness | ✅ | `IAslNet` — host/client/connected state + connection-count changes (Mirror). Message transport is planned, see [docs/networking.md](docs/networking.md) |

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
- **Throwaway experiments** — drop a `.cs` script into `mods/` and iterate with no build setup.
- **Quality-of-life & debug tools** — logging, on-screen info, value tweaks and automation built on
  the events above.

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

The build copies `ASL.dll` + its dependencies into `<GamePath>\BepInEx\plugins\` and the sample
mod into `<GamePath>\mods\`. Disable with `/p:DeployToGame=false`. Full details in
[docs/building.md](docs/building.md).

## Repository layout

```
ASL.slnx                     solution (SDK 10 .slnx format)
Directory.Build.props        shared build settings (GamePath, target framework, …)
src/ASL.API/                 the stable public contract mods compile against (ASL.API.dll)
src/ASL/                     the framework implementation (ASL.dll)
samples/HelloMod/            DLL mod example (also the project template)
samples/ExampleScriptMod/    script mod example
samples/ExampleContentMod/   content mod example
docs/                        documentation
```

## Roadmap

Rough order of what's next:

- **Multiplayer (Mirror) message transport** — awareness (host/client/connections) ships now;
  sending custom data between clients is next, and needs a two-client test (see
  [docs/networking.md](docs/networking.md)).
- **Richer content** — atlas sub-sprites and non-main shader slots for texture swaps; more content
  types (audio, prefab/value tweaks) driven from the manifest.
- **More built-in game events** — NPC spawned, round start/end, contraband scans — surfaced through
  the event bus (built on the opt-in hook system).
- **Stable 1.0 API** — freeze `ASL.API`, ship a reference/NuGet package for mod authors, adopt semver.
- **Distribution** — a Thunderstore package so players install ASL and mods in one click.

## License

**MIT** — see [LICENSE](LICENSE). Use, modify, fork, and redistribute ASL freely (including in
closed-source mods); just keep the copyright notice. MIT is the de-facto standard for BepInEx mod
libraries, so other modders can depend on ASL without legal friction.

ASL is an unofficial, fan-made modding library and is **not affiliated with the developers of
Airport Security Sucks!**. The MIT license covers ASL's own source only — not the game or its
assets, which remain under their own terms.
