# Building ASL from source

## Prerequisites

- **.NET SDK** 6.0 or newer (developed/tested on 10.0.301).
- A local install of *Airport Security Sucks!* with **BepInEx 6 (IL2CPP)** already set up and run
  once, so `BepInEx\interop\` is populated. ASL references those assemblies directly.

## Build

```pwsh
dotnet build ASL.slnx -c Release
```

The solution is in the SDK's `.slnx` format (SDK 9/10+). It contains three projects:

- `src/ASL.API` → `ASL.API.dll` — the stable public contract.
- `src/ASL` → `ASL.dll` — the framework (references ASL.API + Roslyn + BepInEx/interop).
- `samples/HelloMod` → `HelloMod.dll` — example DLL mod / template.

## Configuration (MSBuild properties)

Set in `Directory.Build.props`, overridable on the command line:

| Property | Default | Meaning |
|---|---|---|
| `GamePath` | `D:\Games\Steam\steamapps\common\Airport Security Sucks!` | Game install root (contains `BepInEx\`). |
| `DeployToGame` | `true` | Copy build output into the game after building. |

Override for your machine:

```pwsh
dotnet build ASL.slnx -c Release /p:GamePath="C:\Path\To\Airport Security Sucks!"
dotnet build ASL.slnx -c Release /p:DeployToGame=false   # build only, no copy
```

## What deploy copies

When `DeployToGame=true`:

- `src/ASL` copies **all** its output DLLs to `<GamePath>\BepInEx\plugins\`:
  `ASL.dll`, `ASL.API.dll`, and the Roslyn dependencies
  (`Microsoft.CodeAnalysis*.dll`, `System.Collections.Immutable.dll`,
  `System.Reflection.Metadata.dll`).
- `samples/HelloMod` copies `HelloMod.dll` + its `manifest.json` to `<GamePath>\mods\HelloMod\`.

> The Roslyn DLLs are needed only for **script mods**. `<CopyLocalLockFileAssemblies>true</…>` in
> `src/ASL/ASL.csproj` is what pulls them into the output folder (class libraries don't copy NuGet
> dependencies by default).

## Referencing rules used here

- BepInEx + game interop assemblies are referenced with `<Private>false</Private>` — they're
  provided at runtime by the game/BepInEx, so they must **not** be copied into output.
- `ASL.API` is a normal `ProjectReference` (copied), so it ships alongside `ASL.dll`.

## Target framework

`net6.0` — BepInEx 6 IL2CPP plugins run on the .NET 6 runtime bundled with the game. You can build
this with a newer SDK (the net6 reference pack is restored automatically); you do **not** need the
.NET 6 runtime installed, because the plugin runs inside the game's runtime, not via `dotnet`.

## Iterating with the game open

If the game is running, `BepInEx\plugins\ASL.dll` is locked and the deploy copy fails. Build with
`/p:DeployToGame=false` to compile-check, or close the game before a deploying build.
