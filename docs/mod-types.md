# Mod types

Every ASL mod is a folder under `mods/` containing a [`manifest.json`](manifest.md). The
manifest's `type` field selects how ASL loads it: `content`, `script`, or `dll`. If `type` is
omitted, ASL auto-detects (a `.dll` → dll, a `.cs` → script, otherwise content).

---

## Content mods

**No code.** Ship assets + a declarative manifest. Best for non-programmers.

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
- **Non-readable** target (most game textures) → ASL builds a replacement texture and reassigns
  `Material.mainTexture` references that pointed at the target.

> **Caveats.** The decoder handles 8-bit, non-interlaced PNGs (color types grayscale / RGB / palette /
> gray+alpha / RGBA). The reassignment path covers a material's **main texture**; textures bound to
> other shader slots, or used only through UI `Image` sprite atlases, aren't reassigned yet. Use
> `"listTextureNames": true` to discover valid `target` names (the log also shows each texture's
> `readable` flag).

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

---

## What every mod gets: `IModContext`

`OnLoad(IModContext ctx)` hands you:

- `ctx.Log` — a per-mod logger (`Info` / `Warning` / `Error`), tagged with your mod name.
- `ctx.ModDirectory` — absolute path to your mod folder; load extra assets from here.
- `ctx.ModId` / `ctx.ModName` — your manifest identity.
- `ctx.Events` — the [event bus](api-reference.md#events).
- `ctx.Hooks` — [opt-in Harmony hooks](api-reference.md#hooks).

See the full [API reference](api-reference.md).
