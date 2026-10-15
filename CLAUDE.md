# ASL — Airport Security Lib (project instructions)

Modding framework for *Airport Security Sucks!* on **BepInEx 6 (IL2CPP)**. Mods live in the game's
`mods/` folder; three tiers (content / script / dll) share one stable API (`ASL.API.dll`).

## Standing rule: keep documentation current — ALWAYS

Whenever you change a feature, public API, manifest field, or runtime behavior, **update the docs in
the same change**. This is non-negotiable for this project (it is a public framework — stale docs
mislead modders). Specifically:

- `README.md` — the "What works today" status table and examples.
- `docs/mod-types.md`, `docs/manifest.md`, `docs/api-reference.md`, `docs/building.md`,
  `docs/troubleshooting.md` — whichever the change touches.
- Only document a capability as "working" after verifying it **in-game** (read `BepInEx\LogOutput.log`).

Treat "docs updated" as part of the definition of done for every ASL change.

## Build / deploy

- `dotnet build ASL.slnx -c Release` (SDK 9/10 `.slnx` format). Target framework `net6.0`.
- `Directory.Build.props` sets `GamePath` (game install) and `DeployToGame` (copies output into
  `<GamePath>\BepInEx\plugins\` + the sample into `<GamePath>\mods\`).
- The game **locks `plugins\ASL.dll`** while running — close it before a deploying build, or use
  `/p:DeployToGame=false` to compile-check only.
- BepInEx/interop references use `<Private>false</Private>` (provided at runtime). Roslyn (for script
  mods) ships via `<CopyLocalLockFileAssemblies>true</...>`.

## IL2CPP gotchas (hard-won)

- HarmonyX matches injected patch parameters **by name** — use exact names from the dump, or `__0`/
  `__instance`. A broken patch by another plugin poisons the same method for everyone; `IModHooks`
  isolates this.
- Keep per-frame work tiny (only the alloc-free `Update` event runs every frame; scene/player polling
  is throttled). Heavy per-frame work / allocations stutter the game (GC).
- Never read other objects' meshes (`Mesh.vertices`/`bindposes`) — native AccessViolation, not
  catchable.
- `ImageConversion.LoadImage` is span-based here and unmarshalable; texture swaps decode PNGs in
  managed code (`PngDecoder`) and write via `LoadRawTextureData` / reference reassignment instead.

## 2.0 architecture (pointer)

- **Per-mod services**: `IModContext` members (`Config`, `Storage`, `Timers`, `Mods`, `Commands`, `Assets`, `Loc`, `Ui`, `Events`)
  are built per mod by `ModServices.Create` (`src/ASL/Services/`) and released in one place (`ModServices.Release`) on unload /
  hot reload. Anything a service registers globally must add its undo to the mod's resources.
- **Pure logic** (versions, dependency resolver, scheduler, console tokenizer, layout parser, WAV, language, storage names) lives
  under `src/ASL/Core/` with **no Unity/BepInEx types** so `tests/` can compile it directly. UI toolkit: `src/ASL/Ui/`
  (one driver, `UiRuntime.Tick`, polls open windows only). Dev console: `src/ASL/Console/`.
- **Backward compatibility is a hard rule**: only *add* to `src/ASL.API`; the 0.1 samples (HelloMod, FunPanel, PropHunt,
  ExampleScriptMod, ExampleContentMod) are regression tests and are not edited by feature work.
- **`tools/compile-check/build.sh`** compiles ASL, the samples and script mods on Linux/CI against the real BepInEx core DLLs plus
  *stub* Unity/game interop, runs `tests/*`, and checks `ASL.API` for breaking changes vs `main` (ApiCompat). **Its limits:** the stubs
  are guesses at the real interop, so a pass is *not* proof the real build or the game works, and it can disagree with the real build in either
  direction. The build against `BepInEx\interop` and `BepInEx\LogOutput.log` stay authoritative; docs call 2.0 features
  "🧪 Implemented, not yet verified in-game" until then. Details: `tools/compile-check/README.md`, `docs/building.md`.
- Docs for 2.0: `docs/ui.md`, `config-and-storage.md`, `timers-and-events.md`, `console.md`, `mod-manager.md`,
  `assets-and-localization.md`, `upgrading-to-2.0.md`; keep them current with every change (standing rule above).
