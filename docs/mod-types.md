# Mod types

Every ASL mod is a folder under `mods/` containing a [`manifest.json`](manifest.md). The
manifest's `type` field selects how ASL loads it: `content`, `script`, or `dll`. If `type` is
omitted, ASL auto-detects (a `.dll` → dll, a `.cs` → script, otherwise content).

---

## Content mods

**No code.** Ship assets + a declarative manifest. Best for non-programmers. A content mod can swap
**textures** (✅), and in 2.0 also **audio**, **on-screen text** and ship **windows** declared in JSON (all 🧪 not yet
verified in-game, see below).

Start one with `dotnet new asl-content` (textures) or `dotnet new asl-ui` (a window) - see [templates](../templates/README.md).

```
mods/RetextureStars/
├── manifest.json
└── star.png
```

```json
{
  "id": "com.you.retexturestars",
  "name": "Retexture Stars",
  "version": "1.0.0",
  "type": "content",
  "content": {
    "listTextureNames": true,
    "textures": [
      { "target": "star_gray", "file": "star.png" }
    ]
  }
}
```

- `textures[]` — each entry replaces the loaded game texture named `target` with the PNG `file`
  (a path relative to the mod folder).
- `listTextureNames` — when `true`, ASL logs a sample of loaded texture names to `LogOutput.log`.
  This is how you discover valid `target` names: enable it once, read the log, then set your
  `target`.

Swaps are applied (and re-applied) on every scene change, since textures stream in as levels load.

**How the swap works.** Unity's `ImageConversion.LoadImage` is span-based on this Unity 6 / IL2CPP
build and can't be called from managed code, so ASL decodes the PNG itself (a small managed decoder)
into RGBA32 and writes it the marshal-safe way:
- **Readable** target texture → written in place; every material/sprite using it updates at once.
- **Non-readable** target (most game textures) → ASL builds a replacement texture and repoints
  references that used the target: `Material.mainTexture`, UI `Image.sprite` (rebuilt via
  `Sprite.Create`), and `RawImage.texture`.

> **Caveats.** The decoder handles 8-bit, non-interlaced PNGs (color types grayscale / RGB / palette /
> gray+alpha / RGBA). Not covered for non-readable targets: textures bound to **non-main shader
> slots**, and **atlas sub-sprites** (an `Image` showing one region of a packed atlas — the rebuilt
> sprite uses the whole replacement, centred pivot). Use `"listTextureNames": true` to discover valid
> `target` names (the log also shows each texture's `readable` flag).

### Content v2: audio, text and UI (2.0, 🧪)

All optional, all in the same `content` object (full details: [assets-and-localization.md](assets-and-localization.md#content-mods-v2-audio-and-strings)):

```json
{
  "id": "com.you.pack", "name": "Pack", "version": "1.0.0", "type": "content",
  "content": {
    "listAudioNames": true,
    "audio":   [ { "target": "door_open", "file": "audio/door.wav" } ],
    "strings": { "Boarding pass": "Ticket to ride" }
  },
  "ui": [ "ui/panel.json" ]
}
```

- `audio[]` replaces the `AudioClip` named `target` with your WAV on every AudioSource using it. `listAudioNames: true` logs the
  clip names so you can find targets (like `listTextureNames`).
- `strings` rewrites on-screen text that equals a key exactly. **Experimental**: the game may re-set a label and undo it.
- `ui` lists [JSON layout](ui.md#json-layouts-reference) files; each becomes a window with built-in actions (`toast:`, `announce:`,
  `command:`, `open:`, `toggle:`, `close`). Example: [`samples/ExampleUiContentMod`](../samples/ExampleUiContentMod). A content mod
  must still contain a `content` object (`{}` is fine).

---

## Script mods

**C# without a build.** Drop a `.cs` file; ASL compiles it at runtime with Roslyn.

```
mods/MyScript/
├── manifest.json
└── Main.cs
```

```json
{
  "id": "com.you.myscript",
  "name": "My Script",
  "version": "1.0.0",
  "type": "script",
  "entry": "Main.cs"
}
```

```csharp
using ASL.Api;
// using Metater;            // game types live here (e.g. MetaPlayer)

public sealed class MyScript : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        ctx.Log.Info("Script mod up.");
        ctx.Events.Update += () => { /* per-frame logic */ };
        ctx.Events.LocalPlayerChanged += p =>
            ctx.Log.Info(p != null ? "player ready" : "player gone");
    }
}
```

- `entry` is optional; if omitted ASL compiles every `.cs` in the folder together.
- The script is compiled against everything ASL has loaded (System.*, `ASL.API`) **plus** all of
  the game's interop assemblies, so you can `using ASL.Api;`, `using Metater;`, `using UnityEngine;`
  and touch game types exactly like a compiled mod.
- Compile errors are written to `LogOutput.log` with line numbers; the mod is skipped, the game
  runs on.
- 🧪 **Hot reload (2.0):** reload a script mod without restarting the game from F8 > Mod Manager > Reload or the console
  command `reload <id>`, or add `"hotReload": true` to the manifest to reload automatically when you save the `.cs`
  files. Details and limits: [mod-manager.md](mod-manager.md#hot-reload-script-mods).
- Start from a template: `dotnet new asl-script -n MyScript` ([templates](../templates/README.md)).

Requires the Roslyn DLLs to be present in `plugins\` (shipped with ASL) — see
[getting started](getting-started.md).

---

## DLL mods

**Full C#, compiled by you.** The most powerful option; needs the .NET SDK.

```
mods/MyCodeMod/
├── manifest.json
└── MyCodeMod.dll
```

```json
{
  "id": "com.you.mycodemod",
  "name": "My Code Mod",
  "version": "1.0.0",
  "type": "dll",
  "entry": "MyCodeMod.dll"
}
```

Project setup (`.csproj`), mirroring [`samples/HelloMod`](../samples/HelloMod):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <!-- ASL supplies this at runtime; don't copy it (Private=false). -->
    <Reference Include="ASL.API">
      <HintPath>PATH\TO\BepInEx\plugins\ASL.API.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <!-- Reference game interop only for the types you use, e.g.: -->
    <Reference Include="Assembly-CSharp">
      <HintPath>PATH\TO\BepInEx\interop\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

```csharp
using ASL.Api;

public sealed class MyCodeMod : AslMod
{
    public override void OnLoad(IModContext ctx)
    {
        ctx.Log.Info("Compiled mod loaded.");
    }

    public override void OnUnload() { /* optional cleanup */ }
}
```

Notes:
- A DLL mod is **not** a BepInEx plugin — it has no `[BepInPlugin]`. ASL discovers the `AslMod`
  subclass by reflection and runs it.
- `entry` is optional; ASL otherwise loads the first `.dll` in the folder.
- Reference `ASL.API.dll` with `Private=false` so your mod resolves against the single copy ASL
  already loaded (avoids type-identity mismatches).
- Start from a template: `dotnet new asl-dll -n MyMod` creates this project (with `GamePath` handling and a deploy step)
  plus a working 2.0 starter ([templates](../templates/README.md)). DLL mods cannot be hot reloaded.
- 🧪 2.0 adds `AslMod.OnAllModsLoaded()` (runs after every mod's `OnLoad`) and a manifest `minAslVersion`/`dependencies`
  ([manifest](manifest.md)).

---

## What every mod gets: `IModContext`

`OnLoad(IModContext ctx)` hands you:

- `ctx.Log` — a per-mod logger (`Info` / `Warning` / `Error`), tagged with your mod name.
- `ctx.ModDirectory` — absolute path to your mod folder; load extra assets from here.
- `ctx.ModId` / `ctx.ModName` — your manifest identity.
- `ctx.Events` — the [event bus](api-reference.md#events): frame tick, scene and local-player changes.
- `ctx.Hooks` — [opt-in Harmony hooks](api-reference.md#hooks) on game methods.
- `ctx.Menu` — the [in-game menu](api-reference.md#menu) (F8): register toggles, buttons, sliders.
- `ctx.Ui` — [on-screen UI](api-reference.md#on-screen-ui): the game's announcement banner (and, in 2.0, the UI toolkit).
- `ctx.Input` — [input & keybinds](api-reference.md#input--keybinds): rebindable named keys plus raw key queries.
- `ctx.Net` — [networking](networking.md): host/client state, player identity, a host-authoritative synced store, and a host↔client message transport.

New in 2.0 (🧪, all non-null for DLL and script mods):

- `ctx.Ui` is now a full toolkit: windows, HUDs, toasts, dialogs and JSON layouts - [ui.md](ui.md).
- `ctx.Config` / `ctx.Storage` — typed settings (auto-shown in F8) and save data - [config-and-storage.md](config-and-storage.md).
- `ctx.Timers` — delays, intervals, conditions, coroutines - [timers-and-events.md](timers-and-events.md).
- `ctx.Mods` / `ctx.Info` — other mods, shared services, topics, your own info - [mod-manager.md](mod-manager.md).
- `ctx.Commands` — dev-console commands - [console.md](console.md).
- `ctx.Assets` / `ctx.Loc` — textures, sprites, audio, bundles; translations - [assets-and-localization.md](assets-and-localization.md).

See the full [API reference](api-reference.md).
