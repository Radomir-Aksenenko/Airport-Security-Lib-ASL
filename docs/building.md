# Building ASL from source

## Prerequisites

- **.NET SDK** 6.0 or newer (developed/tested on 10.0.301).
- A local install of *Airport Security Sucks!* with **BepInEx 6 (IL2CPP)** already set up and run
  once, so `BepInEx\interop\` is populated. ASL references those assemblies directly.

## Build

```pwsh
dotnet build ASL.slnx -c Release
```

The solution is in the SDK's `.slnx` format (SDK 9/10+). It contains:

- `src/ASL.API` → `ASL.API.dll` — the stable public contract.
- `src/ASL` → `ASL.dll` — the framework (references ASL.API + Roslyn + BepInEx/interop).
- `samples/PropHunt` → `PropHunt.dll` — the showcase mod (multiplayer Prop Hunt).
- `samples/UiShowcase` → `UiShowcase.dll` — 🧪 the UI-toolkit tour (every widget, button shape/size/variant, themes, HUD,
  dialogs, config, storage, console commands, localization). See [`samples/UiShowcase`](../samples/UiShowcase).

The default build deploys the **framework** (`src/ASL` → `plugins\`) **and UiShowcase** (it is the learning sample for 2.0, and it
copies itself to `<GamePath>\mods\UiShowcase\` whenever `DeployToGame` is on). PropHunt compiles but is **not** copied to `mods\`
unless you opt in with `-p:DeploySamples=true` (see below). `samples/HelloMod` (the minimal DLL-mod template) and
`samples/FunPanel` (real-player tricks) stay in the repo as standalone source, **not** in the solution, and so do the
`ExampleScriptMod`, `ExampleContentMod` and `ExampleUiContentMod` folders (copy them into `mods\` by hand).

Not in the solution on purpose: `tests/` (so a build never deploys a test) and `templates/` (a `dotnet new` pack, see
[templates/README.md](../templates/README.md)).

## Configuration (MSBuild properties)

Set in `Directory.Build.props`, overridable on the command line:

| Property | Default | Meaning |
|---|---|---|
| `GamePath` | `D:\Games\Steam\steamapps\common\Airport Security Sucks!` | Game install root (contains `BepInEx\`). |
| `DeployToGame` | `true` | Copy the **framework** output into `BepInEx\plugins\` (and the UiShowcase sample into `mods\UiShowcase\`) after building. |
| `DeploySamples` | `false` | Also copy the PropHunt sample into the game's `mods\` folder. Opt-in, for local testing. |

Override for your machine:

```pwsh
dotnet build ASL.slnx -c Release /p:GamePath="C:\Path\To\Airport Security Sucks!"
dotnet build ASL.slnx -c Release /p:DeployToGame=false    # build only, no copy
dotnet build ASL.slnx -c Release /p:DeploySamples=true    # also install PropHunt into mods\ for testing
```

## What deploy copies

When `DeployToGame=true` (the default):

- `src/ASL` copies **all** its output DLLs to `<GamePath>\BepInEx\plugins\`:
  `ASL.dll`, `ASL.API.dll`, and the Roslyn dependencies
  (`Microsoft.CodeAnalysis*.dll`, `System.Collections.Immutable.dll`,
  `System.Reflection.Metadata.dll`).

- `samples/UiShowcase` copies `UiShowcase.dll`, `manifest.json` and `lang\*.json` to `<GamePath>\mods\UiShowcase\`.
  This is not gated by `DeploySamples`; use `/p:DeployToGame=false` to skip it.

The PropHunt sample is **not** copied to `mods\` unless you also pass `-p:DeploySamples=true` — then its
`PropHunt.dll` + `manifest.json` land in `<GamePath>\mods\PropHunt\`. (Building `HelloMod`/`FunPanel` on
their own still copies them to `mods\<Name>\`.)

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

## Compile-check without the game

`tools/compile-check/build.sh` type-checks everything on a machine **without the game** (Linux/CI): the real BepInEx 6 IL2CPP DLLs plus *stub*
Unity/game interop assemblies. It builds `ASL.slnx`, the samples that are not in the solution (HelloMod, FunPanel), the script mods
(through the same path the loader uses) and the stub-surface self-test, also compiles the `templates/` projects and validates the template JSON, then runs the unit tests and the API-compat check:

```bash
tools/compile-check/build.sh            # needs .NET SDK 9+, curl, unzip; the first run downloads ~33 MB
tools/compile-check/build.sh --clean    # wipe out/ first
ASL_API_BASELINE=v1.0 tools/compile-check/build.sh    # compare ASL.API against another ref
```

> **It is an approximation.** A pass does **not** prove the real build or the game behaves: the stubs are written from
> knowledge of Unity/TMP/Mirror, and a signature that differs from the real interop can hide a compile error (or invent one). The real
> build against your `BepInEx\interop` and testing in-game (`BepInEx\LogOutput.log`) stay authoritative. This is also why every 2.0 feature
> is marked "🧪 not yet verified in-game" in the docs.

What it checks:

- **Compile**: every project above builds with zero errors.
- **Unit tests**: `dotnet test` on every `tests/*/*.csproj` (`ASL.Tests`: versions/ranges, dependency resolver, console tokenizer, scheduler,
  layout parser, manifest leniency, frame backoff; `ASL.Data.Tests`: WAV decoder, language resolver, storage names). They cover only the pure-C#
  logic under `src/ASL/Core` and `ModManifest.cs`; no Unity or game types.
- **API compatibility**: `ASL.API` from the baseline ref (default `main`) is built next to the current one and compared with ApiCompat.
  Removing or changing a public member **fails**; adding members passes (`CP0006`, adding to an interface, is ignored by default because
  only ASL implements those interfaces). This is what backs the "nothing breaks" promise in [upgrading](upgrading-to-2.0.md).

Logs per step are in `tools/compile-check/out/logs/`. CI runs it on every push (`.github/workflows/compile-check.yml`, needs `fetch-depth: 0`).
Details, and how to fix a missing stub member, are in [`tools/compile-check/README.md`](../tools/compile-check/README.md).

## Templates

`templates/` is the source of the `dotnet new` pack (`asl-dll`, `asl-script`, `asl-content`, `asl-ui`):

```bash
cd templates && dotnet pack -o <out-dir>
dotnet new install <out-dir>/ASL.Templates.2.0.0.nupkg
```

## Version

The version lives in `Directory.Build.props` (`<Version>2.0.0</Version>`) and in `AslInfo.Version` / `AslVersion.Current`. Keep them in sync when releasing.
