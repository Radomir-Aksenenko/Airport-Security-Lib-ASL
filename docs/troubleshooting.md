# Troubleshooting & IL2CPP notes

This game is **Unity 6 (6000.0.72), IL2CPP, URP, Mirror networking**, modded via **BepInEx 6
IL2CPP** (Il2CppInterop + HarmonyX). A few IL2CPP-specific gotchas matter for modders.

## My mod doesn't load

Check `BepInEx\LogOutput.log`. ASL logs every step:

- `Skipping '<folder>': no manifest.json.` — add a [`manifest.json`](manifest.md).
- `manifest is missing the required 'id' field.` — add `"id"`.
- `no public AslMod subclass found.` — your class must be `public`, non-abstract, subclass
  `AslMod`, and have a parameterless constructor.
- `OnLoad threw: …` — your `OnLoad` raised an exception; the rest is the stack trace.

Each mod is isolated: one failing mod never stops the others, and `Mod loading complete: N mod(s)
active` tells you how many succeeded.

## Script mod: "Could not load … Microsoft.CodeAnalysis"

The Roslyn DLLs aren't in `plugins\`. Copy `Microsoft.CodeAnalysis.dll`,
`Microsoft.CodeAnalysis.CSharp.dll`, `System.Collections.Immutable.dll`,
`System.Reflection.Metadata.dll` next to `ASL.dll`. (A source build with `DeployToGame=true` does
this automatically.)

## Script mod: compile errors

ASL logs them with IDs and line numbers:
```
[script] 'ScriptMod_…' failed to compile (2 error(s)):
    CS0246: The type or namespace name 'Foo' could not be found @ Main.cs: (5,9)
```
Fix the source and relaunch. Remember scripts can `using ASL.Api;` and game namespaces
(`Metater`, `UnityEngine`, `Mirror`, …) — the compiler references all interop assemblies.

## Texture swaps

Texture swaps work via a marshal-safe path (Unity 6 / IL2CPP can't call the span-based
`ImageConversion.LoadImage`, so ASL decodes the PNG itself and writes the pixels directly):

```
[content] 'star_gray': applied to 1 target(s)/material(s).
[content] applied 1 texture write(s)/reassignment(s).
```

How it resolves each target:
- **Readable** target texture → written in place (`Reinitialize` + `LoadRawTextureData` + `Apply`);
  all holders update.
- **Non-readable** target (most game textures) → ASL builds a replacement texture and repoints
  `Material.mainTexture`, UI `Image.sprite`, and `RawImage.texture` references to it.

If a swap reports nothing applied:
- The `target` name may be wrong — enable `"listTextureNames": true` and read the log (it shows each
  texture's `readable` flag too).
- The texture may load only in a later scene — swaps re-apply on every scene change.
- The texture may be an **atlas sub-sprite** or bound to a **non-main shader slot** — those aren't
  reassigned yet (see caveats in [mod-types](mod-types.md#content-mods)).

PNG support: 8-bit depth, non-interlaced, color types grayscale / RGB / palette / gray+alpha / RGBA.
Other PNGs log a decode error and are skipped.

## Harmony hooks: "Parameter X not found" / "IL Compile Error"

Two real lessons from this build:

1. **HarmonyX matches injected parameters by name.** A patch declaring `Transform transform` for a
   method whose parameter is actually named `root` fails with
   `Parameter "transform" not found`. Use the *exact* parameter names from the IL2CPP dump, or use
   positional injection (`__0`, `__1`), or take only `__instance`.
2. **A broken patch poisons the method for everyone.** If another plugin registered a bad patch on
   a method, *your* patch on the same method fails too when HarmonyX recompiles it. ASL's
   `IModHooks.TryPostfix` isolates this: it returns `false` and logs, rather than throwing.

Prefer ASL **events** for frequent signals; use **hooks** only for specific methods, and never on
hot per-frame methods.

## Performance / stutter

ASL keeps per-frame work minimal: only the allocation-free `Update` event fires every frame; scene
and player polling run a few times a second. If you add a per-frame `Update` handler, keep it
cheap — heavy work or allocations every frame will stutter a Unity 6 game (GC pressure). This is
the single most common cause of "the game started lagging" after adding a mod.

## Native crashes (AccessViolation)

Do **not** read other objects' meshes (`Mesh.vertices`, `Mesh.bindposes`, …) — on this build those
calls can trigger a native AccessViolation that **`try/catch` cannot catch** (it's not a managed
exception). The only defense is not calling the dangerous API. General rule: be conservative with
IL2CPP APIs that return large native buffers.

## Useful facts about the game

- NPCs are built by `RandomNpcGenerator` from `NpcPartOption[]` parts (heads, hair, clothes, …)
  with per-part "allow none"/chance fields and a `femaleChance`.
- The local player is `Metater.MetaPlayer` (static `LocalPlayerInstance`, list `Instances`).
- Items are `HeldItemInteractable`; contraband detection is `ContrabandDetector`.
- Networking is Mirror (`Mirror.NetworkServer` / `NetworkManager`).
- A full IL2CPP dump is invaluable for finding exact type/method/parameter names before hooking.
