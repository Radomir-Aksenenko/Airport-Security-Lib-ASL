# Publishing to Nexus Mods

How to put **ASL** (the framework) and **PropHunt** (the demo mod) on
[Nexus Mods](https://www.nexusmods.com/). Two separate mod pages: the library, and the mod that
depends on it.

> **Before you start — is the game supported?** Nexus only hosts mods for games it lists. Check that
> *Airport Security Sucks!* has a page on nexusmods.com (search for it). If it does **not**, you can
> request the game be added (Nexus → "Can't find your game?"), or publish on **Thunderstore** instead
> (the usual home for BepInEx/IL2CPP mods — it auto-installs BepInEx as a dependency) and/or attach the
> bundle to a **GitHub Release**. The rest of this guide assumes the Nexus game page exists.

## What you upload

The build produces two shippable shapes (see [building.md](building.md) and the `dist/` bundle):

| Shape | Contents | Who it's for |
|---|---|---|
| **All-in-one bundle** `dist/ASL-v0.1.0-PropHunt-bundle.zip` | BepInEx 6 (IL2CPP) + .NET runtime + ASL + PropHunt, drops into the game folder | players who want one-click "install and play" |
| **Framework only** | `BepInEx\plugins\ASL.dll` + `ASL.API.dll` + Roslyn DLLs (+ BepInEx if you bundle it) | other mod authors / players who only want the loader |
| **Mod only** | `mods\PropHunt\` (`PropHunt.dll` + `manifest.json`) | players who already have ASL |

Recommended split:
- **ASL page → framework.** Ship a clean framework bundle (BepInEx + ASL, **no** mods). Players install
  this once to get modding support.
- **PropHunt page → the mod**, with **ASL listed as a requirement**. Offer the all-in-one bundle there
  as a convenience download for people who don't want to install two things.

To build a **framework-only** bundle, take the all-in-one bundle and delete the `mods\PropHunt` folder
from it (or re-zip `dist/stage-mp` after removing `mods\PropHunt`).

## Account & one-time setup

1. Create/log in to a Nexus account and verify your email.
2. Read the Nexus **author** onboarding once (the "Upload a mod" help). Premium isn't required to upload.
3. Have ready: a **thumbnail image** (Nexus requires at least one image per mod), a short summary, and a
   longer description. Reuse the text from [`dist/stage-mp/ASL-README.txt`](../dist/stage-mp/ASL-README.txt)
   and the repo `README.md`.

## Uploading ASL (the framework)

1. On the game's Nexus page: **Mods → Upload mod** (or your profile → *Upload a mod*).
2. **Category:** *Utilities* / *Modders Resources / Tools* (a framework, not gameplay).
3. **Name:** `ASL - Airport Security Lib`. **Version:** `0.1.0` (matches `AslInfo.Version`).
4. **Summary / Description:** what it is (a BepInEx 6 IL2CPP modding framework — mod loader, in-game
   menu, networking, input/keybinds, player & UI helpers), the requirements, and install steps. Link the
   GitHub repo `https://github.com/Radomir-Aksenenko/Airport-Security-Lib-ASL`.
5. **Requirements** (the Requirements section of the description / the dependency picker):
   - **BepInEx 6 (IL2CPP)** — if you ship the *framework-only* zip without BepInEx, list it as a required
     download and link the BepInEx build. If you ship the **all-in-one bundle**, BepInEx is already
     included, so say "no separate BepInEx needed".
6. **Files tab → Add file:** upload the framework zip, mark it **Main file**. Fill the file's own version
   and a short changelog.
7. **Images:** upload at least the thumbnail.
8. **Permissions:** ASL is **MIT** — set the permissions matrix to permissive (reuse/redistribute/modify
   allowed with credit). Note in the description that the bundled **BepInEx is LGPL** and its license is
   kept in the archive.
9. **Publish.**

## Uploading PropHunt (the mod)

1. **Mods → Upload mod** on the same game page.
2. **Category:** *Gameplay* / *Multiplayer*.
3. **Name:** `PropHunt (ASL)`. **Version:** `1.0.0` (matches `samples/PropHunt/manifest.json`).
4. **Description:** the game mode + controls (props: look at an object and press **B** to disguise,
   **N** to freeze; guard: **left-click** to catch — all rebindable in the F8 Mods menu), that it's
   **multiplayer (2+ players)** with rounds and swapping roles, and that it **requires ASL**.
5. **Requirements → Nexus requirements:** add your **ASL** mod page as a required mod. (Players must
   install ASL first.)
6. **Files tab:**
   - **Main file:** the *mod-only* zip — a `PropHunt\` folder containing `PropHunt.dll` + `manifest.json`,
     so it extracts to `<game>\mods\PropHunt\`.
   - **Optional file:** the all-in-one bundle `ASL-v0.1.0-PropHunt-bundle.zip`, labelled
     "Everything bundled (includes ASL)" for one-click players.
7. **Images / Permissions / Publish** as above (PropHunt is MIT too).

## Install instructions to put in the description

Most players will install **manually** (BepInEx IL2CPP games aren't always Vortex-friendly):

```
1. Close the game.
2. Extract the zip into the game folder (the one with "Airport Security Sucks!.exe"), merging folders.
   Steam: ...\steamapps\common\Airport Security Sucks!\
3. Launch once (first launch builds the interop cache — can take a minute).
4. Main menu shows a "Mods" button; in-game press F8.
```

If the game IS Vortex-supported you can also tell players to "Mod Manager Download", but verify it works
first — BepInEx's loose-file layout sometimes needs manual install.

## Updating later

1. Bump the version: `AslInfo.Version` in `src/ASL/AslPlugin.cs` (and `manifest.json` for PropHunt).
2. Rebuild and re-assemble the bundle (re-sync `dist/stage-mp`, re-zip — see [building.md](building.md)).
3. On Nexus: **Files → Add file** with the new version (don't delete the old one immediately), update the
   mod's version field and changelog. Nexus notifies trackers/endorsers of the update.
