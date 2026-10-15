# Getting started

## For players: installing ASL

1. Install **BepInEx 6 (IL2CPP build)** into the game folder and run the game once so BepInEx
   generates its interop assemblies (`BepInEx\interop\` becomes populated).
2. Copy `ASL.dll` and its bundled dependencies into `BepInEx\plugins\`:
   - `ASL.dll`
   - `ASL.API.dll`
   - `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.CSharp.dll`,
     `System.Collections.Immutable.dll`, `System.Reflection.Metadata.dll`
     *(these power script mods; you can omit them if you never use `type: script` mods)*
3. Launch the game. In `BepInEx\LogOutput.log` you should see:
   ```
   [Info :ASL - Airport Security Lib] ASL - Airport Security Lib v2.0.0 - booting.
   [Info :ASL - Airport Security Lib] Core online.
   ```
   In the game, press **F8** to open ASL's menu (it lists **Mod Manager**, **ASL** and your mods) and the
   <kbd>`</kbd> key for the 🧪 [dev console](console.md).
4. ASL creates a `mods\` folder next to the game executable on first run. Drop mods there.

## For players: installing a mod

A mod is a single folder. Put it in `mods\`:

```
Airport Security Sucks/
└── mods/
    └── CoolMod/
        ├── manifest.json
        └── (a .dll, a .cs, or assets — depending on the mod)
```

Restart the game. The log shows each mod loading:

```
[Info :ASL - Airport Security Lib] Loading mod 'Cool Mod' v1.0.0 [Dll] (id=com.author.coolmod)
[Info :ASL - Airport Security Lib]   'com.author.coolmod': loaded OK.
[Info :ASL - Airport Security Lib] Mod loading complete: 1 mod(s) active.
```

If a mod fails, it is logged and **skipped** — other mods keep working.

## For modders: which mod type?

| You want to… | Use | Build tools needed |
|---|---|---|
| Replace textures / ship assets, no programming | [content mod](mod-types.md#content-mods) | none |
| Write C# without setting up a project | [script mod](mod-types.md#script-mods) | none (ASL compiles it) |
| Write full C#, ship a compiled library | [DLL mod](mod-types.md#dll-mods) | .NET SDK |

All three are described in [Mod types](mod-types.md). Every mod needs a
[`manifest.json`](manifest.md).

## For modders: the fastest start (script mod)

Create `mods\HelloScript\manifest.json`:

```json
{
  "id": "com.you.helloscript",
  "name": "Hello Script",
  "version": "1.0.0",
  "type": "script",
  "entry": "Main.cs"
}
```

…and `mods\HelloScript\Main.cs`:

```csharp
using ASL.Api;

public sealed class HelloScript : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        ctx.Log.Info("Hello from my first ASL mod!");
        ctx.Events.SceneChanged += scene => ctx.Log.Info($"Scene: {scene}");
    }
}
```

Launch the game — ASL compiles and runs it. Watch `LogOutput.log` for your messages.

## For modders: start from a template (`dotnet new`)

ASL ships `dotnet new` templates (🧪 new in 2.0) so you never start from an empty folder:

```bash
dotnet new install ./templates/content/asl-script       # once; or install the packed ASL.Templates nupkg
dotnet new asl-script -n CoolScript                    # script mod: Main.cs + manifest.json, no build
```

| Template | Creates |
|---|---|
| `asl-script` | A script mod with a window, a button and a toast. Copy the folder into `mods\`. |
| `asl-dll` | A compiled DLL mod project (config, key-toggled window, timer, console command) that builds into `<GamePath>\mods\<Name>\`. |
| `asl-content` | A no-code texture-swap mod. |
| `asl-ui` | A no-code window declared in `ui/main.json`. |

Install, parameters (`--param:author`) and build instructions: [templates/README.md](../templates/README.md).

## Next steps

- Make a window with buttons, toggles and sliders in minutes: [UI guide](ui.md).
- Save settings the player can edit in game: [config and storage](config-and-storage.md).
- Test faster: reload script mods with `reload <id>` or `"hotReload": true` ([mod manager](mod-manager.md)), and poke at things from the [console](console.md).
- Coming from ASL 0.1? Nothing breaks: [upgrading to 2.0](upgrading-to-2.0.md).
