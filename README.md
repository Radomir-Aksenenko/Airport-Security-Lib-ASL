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
- [Troubleshooting & IL2CPP notes](docs/troubleshooting.md)

## What works today

| Capability | Status | Notes |
|---|---|---|
| `mods/` folder loader | ✅ | Each mod = a folder with `manifest.json`; per-mod isolation & logging |
| **DLL mods** (`type: dll`) | ✅ | Subclass `AslMod`, ship a `.dll` |
| **Script mods** (`type: script`) | ✅ | Plain `.cs`, compiled at runtime with Roslyn — no build setup |
| **Content mods** (`type: content`) | ✅ | Texture swaps: managed PNG decode → in-place write (readable textures) or `Material.mainTexture` reassignment (non-readable) + texture-name discovery |
| Event bus | ✅ | `Update`, `SceneChanged`, `LocalPlayerChanged` |
| Opt-in hooks | ✅ | `IModHooks.TryPostfix(type, method, cb)` — install Harmony patches safely, on demand |

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

## License

Not yet chosen. Add one before any public release.
