# ASL templates

`dotnet new` templates for [ASL](../README.md) (Airport Security Lib) mods for
*Airport Security Sucks!*. Four templates cover the three mod tiers plus a declarative UI:

| Short name | Kind | What you get |
|---|---|---|
| `asl-dll` | compiled C# (`type: dll`) | A project (`.csproj`) and `AslMod` subclass: saved setting, F6 keybind that toggles a window, toast, timer, console command. Builds into `<GamePath>\mods\<Name>\`. |
| `asl-script` | runtime C# (`type: script`) | A `Main.cs` and `manifest.json`, no build. A window with a button and a toast. |
| `asl-content` | no code (`type: content`) | A `manifest.json` that swaps a game texture for your PNG, plus a `README.txt` on finding texture names. You add the PNG. |
| `asl-ui` | no code, declarative UI | A `manifest.json` with `"ui": ["ui/main.json"]` and a JSON window (F6) that uses built-in actions only. |

## Install

From a built package:

```bash
cd templates
dotnet pack -o <out-dir>
dotnet new install <out-dir>/ASL.Templates.2.0.0.nupkg
```

From a clone, without packing (installs the single template folder):

```bash
dotnet new install ./templates/content/asl-dll
```

Install the others the same way (`./templates/content/asl-script`, and so on), or install the whole
pack. List what is installed with `dotnet new list asl`. Remove the pack with
`dotnet new uninstall ASL.Templates`.

## Usage

```bash
dotnet new asl-dll -n CoolMod                    # compiled DLL mod
dotnet new asl-script -n CoolScript              # script mod (no build)
dotnet new asl-content -n CoolSkin               # content mod (texture swap)
dotnet new asl-ui -n CoolPanel                   # declarative UI mod
```

Each name becomes the project and folder name, and it replaces `MyMod` in the files. Use a name without
spaces. The mod id is `com.<author>.<name>` in lowercase, with the author set by `--param:author`:

```bash
dotnet new asl-dll -n CoolMod --param:author radomir   # id: com.radomir.coolmod
dotnet new asl-dll -n CoolMod -o ./mods/CoolMod        # choose the output folder
```

Then build the DLL mod. The template's `GamePath` defaults to the Steam install on Windows. Set your
own (no edit needed):

```pwsh
dotnet build -c Release /p:GamePath="D:\Games\Steam\steamapps\common\Airport Security Sucks!"
```

The build copies `CoolMod.dll` and `manifest.json` into `<GamePath>\mods\CoolMod\`. Skip the copy with
`/p:DeployToGame=false`. `ASL.API.dll` and the game's interop assemblies are compile-time references
only (`Private=false`), so they are not copied into your mod.

Script, content and UI mods need no build. Copy the folder into the game's `mods\` folder, or zip it
for players.

## Notes

- **Status:** the templates target the ASL 2.0 API, so **ASL 2.0 or newer must be installed** (the generated manifests do
  not set `minAslVersion` yet; add `"minAslVersion": "2.0.0"` so ASL reports a clear `Incompatible` state instead of a runtime
  error on older versions). The 2.0 services they use (config, timers, the UI toolkit, JSON layouts, the console) are implemented
  but 🧪 **not yet verified in-game**; texture swaps in `asl-content` are the proven 0.1 feature. The templates themselves have
  been compile-checked, not run in the game.
- Next steps after generating a mod: [Build UI in minutes](../docs/ui.md), [config & storage](../docs/config-and-storage.md),
  [manifest reference](../docs/manifest.md).
- The DLL template targets `net6.0` (the game's runtime) and references `ASL.API.dll` from
  `<GamePath>\BepInEx\plugins\`. ASL must already be installed in that game folder.
- `asl-content` ships no images. Add your own PNG and set `"file"` in `manifest.json`. The README in the
  template explains how to find texture names.
- `--param:author` sets the author and the middle part of the mod id. There is no separate id option:
  edit `"id"` in `manifest.json` if you want a different one.
