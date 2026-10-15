# compile-check

Compile-checks ASL (`src/`), the samples and script mods on a machine **without the game** (Linux/CI).

The projects normally reference `$(GamePath)\BepInEx\core\*.dll` (BepInEx) and
`$(GamePath)\BepInEx\interop\*.dll` (Il2CppInterop assemblies generated from the paid game). Here:

- **core** = the *real* BepInEx 6 IL2CPP (be.788) DLLs, downloaded once into `.cache/`
  (`BepInEx.Core`, `BepInEx.Unity.IL2CPP`, `Il2CppInterop.Runtime`, `0Harmony`, ...). Never stubbed.
- **interop** = *stub* assemblies built from `stubs/` (same assembly names, namespaces and type placement
  as the real interop output; members just `throw null`). They reference the real `Il2CppInterop.Runtime`, so
  `Il2CppObjectBase`, `TryCast`, `Il2CppType.Of<T>()`, `DelegateSupport`, `Il2CppStructArray<T>`... are real.

`build.sh` lays these out as a fake game folder in `out/game/BepInEx/{core,interop}` and runs
`dotnet build -p:GamePath=out/game -p:DeployToGame=false` on `ASL.slnx`, on every `samples/*` project that
is not in the solution (HelloMod, FunPanel), and on every `samples/*` mod whose manifest has
`"type": "script"` (a throwaway csproj in `out/script/`, mirroring `ModLoader`/`ScriptLoader`: the manifest
`entry` file, else all top-level `*.cs`; references = ASL.API + every interop assembly).

**Templates** (`templates/content/*`): every template folder that ships C# is compile-checked the same way
(a throwaway csproj in `out/template/<name>/` with all its top-level `*.cs`, references = ASL.API + every
interop assembly). Today that is `asl-dll` and `asl-script`; `asl-content` and `asl-ui` have no code and are
skipped. The template's own `MyMod.csproj` is **not** built (it points at a real game path). Result lines:
`OK    template:asl-dll`, `OK    template:asl-script`. Every `templates/content/*/manifest.json`,
`.template.config/template.json` and `ui/*.json` is also parsed as JSON (`python3`): `OK    template-json:<name> (N file(s))`
or `FAIL  template-json:<name>` with the parse error. Templates must only use members that exist in
`src/ASL.API`, so a template compile failure means the template (or the API it uses) needs fixing.

Then a self-test compiles `smoke/UiSurface.cs`, a self-test of the Unity/uGUI/TMP surface the stubs promise
(so UI code can be written against them).

After that it runs:

- **Unit tests**: `dotnet test` on every `tests/*/*.csproj` (currently `ASL.Tests`, `ASL.Data.Tests`); one
  `tests:<name>` summary line each.
- **API compatibility** (`api-compat`): `ASL.API` is the stable contract mods compile against. The script
  extracts `src/ASL.API` from the baseline git ref (`git archive`), builds it against the same stub game dir,
  builds the current `ASL.API`, and compares them with Microsoft's ApiCompat tool
  (`Microsoft.DotNet.ApiCompat.Tool`, installed once into `.cache/tools`; v9 to match the SDK 9 runtime). Result line:
  `OK    api-compat (ASL.API main -> current: no breaking changes)`,
  `FAIL  api-compat (N breaking change(s))` (offending members are printed; log in `out/logs/api-compat.log`), or
  `SKIP  api-compat (baseline ref ... not found)`. The baseline ref is `$ASL_API_BASELINE` (default `main`, falling
  back to `origin/<ref>`), so CI needs `fetch-depth: 0`. `CP0006` (adding a member to an interface) is ignored by
  default because ASL implements the `IAsl*`/`IMod*` interfaces and mods only consume them; override with
  `ASL_API_NOWARN` (e.g. `ASL_API_NOWARN=CP0006,CP0008`). Removing or changing a public member fails.

## Important: this is an approximation

**Passing does NOT guarantee that the real build or the game behaves.** The stubs were written from
knowledge of Unity 2021/2022, TextMeshPro, Mirror and from how this repo *uses* the game's own types
(`Metater.*`, `SettingsUi`, ...). Where a signature, property/field choice, generic constraint or assembly
placement differs from the real interop, the check can pass when the real build fails (or fail when the real
build passes). The build against the game's real `BepInEx\interop` and testing in-game
(`BepInEx\LogOutput.log`) stay authoritative. Game types in `stubs/Assembly-CSharp/Game.cs` are pure guesses
inferred from usage.

## Run

```bash
tools/compile-check/build.sh            # needs .NET SDK 9+ (dotnet on PATH, or $HOME/.dotnet/dotnet), curl, unzip
tools/compile-check/build.sh --clean    # wipe out/ first (the BepInEx download in .cache/ is kept)
ASL_API_BASELINE=v1.0 tools/compile-check/build.sh   # compare ASL.API against another ref/tag/commit
```

Exit code is non-zero if anything fails to compile; per-target logs are in `out/logs/`. The first run
downloads ~33 MB and restores NuGet packages (net6.0 targeting pack, Roslyn); later runs take ~20 s.
CI: `.github/workflows/compile-check.yml`. `.cache/` and `out/` are git-ignored.

## Fixing a missing/wrong stub member

Errors like `CS1061: 'X' does not contain a definition for 'y'` (or a missing type) usually mean the stub is
incomplete, not that the code is wrong. Check what the real game/Unity API looks like (the game's interop
dump, dnSpy/ILSpy on `BepInEx\interop\*.dll`, or Unity docs), then edit the stub:

1. Find the assembly the real type lives in (a `Foo` in `UnityEngine.UI` goes in `stubs/UnityEngine.UI/`).
   The assembly matters: code only compiles if its csproj references that assembly, as in the real build.
2. Add the member with the **real** shape - property vs field, static vs instance, return type, and
   `Il2CppStructArray<T>` / `Il2CppReferenceArray<T>` / `Il2CppStringArray` instead of `T[]` where the
   interop would use them. Bodies are `throw null` (or an auto-property); they are never executed.
3. New type: derive from the right base (`UnityEngine.Object` -> `Il2CppSystem.Object` ->
   `Il2CppObjectBase`), add `public Foo(IntPtr pointer) : base(pointer) { }` (plus a public parameterless
   ctor only if code does `new Foo()` and the real type has one). Non-blittable structs (those holding
   references) are classes deriving `Il2CppSystem.ValueType` in the interop output; blittable ones stay structs.
4. New assembly: add `stubs/<Name>/<Name>.csproj` (copy a sibling) and an entry in `stubs/AllStubs.csproj`.
5. Run `build.sh`. Add anything new UI/Unity code needs to `smoke/UiSurface.cs` so it keeps working.

Layout: `stubs/` (one csproj per assembly + `Directory.Build.props`), `smoke/` (stub-surface self-test),
`build.sh`, `.cache/` (download), `out/` (fake game folder, generated csprojs, logs).

## Known stub guesses worth double-checking against the real interop

- `System.Action` -> `UnityAction` implicit conversions (`AddListener(new Action(...))`) are assumed to exist.
- `Il2CppSystem.Object.GetIl2CppType()` name; `Il2CppSystem.Collections.Generic.*` implementing `IList<T>` /
  `ICollection<T>`; `Dictionary.GetEnumerator()` returning a class-typed `Enumerator`.
- `KeyCode` in `UnityEngine.CoreModule`; `TextAnchor`/`FontStyle`/`Font` in `TextRenderingModule` (needs a csproj
  reference that ASL.csproj does not have today); `RectOffset` in `IMGUIModule`; `RenderMode` in `UIModule`.
- `ImageConversion.LoadImage` is deliberately absent (span-based, unmarshalable on this build).
- Everything in `stubs/Assembly-CSharp/Game.cs` (e.g. `MetaPlayer` being a Mirror `NetworkBehaviour`,
  `MetaCursor.CursorUsers` being a `HashSet<MonoBehaviour>`, `LanguageManager.CurrentLanguage` being a static string).
