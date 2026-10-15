#!/usr/bin/env bash
# ASL compile-check: type-checks ASL, ASL.API, the samples and script mods on a machine WITHOUT the game.
#
#   tools/compile-check/build.sh            # run everything
#   tools/compile-check/build.sh --clean    # wipe out/ first (keeps the BepInEx download in .cache/)
#
# It downloads the REAL BepInEx 6 IL2CPP core DLLs (cached), builds STUB Unity/game interop assemblies from
# tools/compile-check/stubs, lays them out as a fake game folder, then builds everything with
# -p:GamePath=<fake game>. Exits non-zero if anything fails to compile. See README.md.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
CACHE="$HERE/.cache"
OUT="$HERE/out"
GAME="$OUT/game"
CORE="$GAME/BepInEx/core"
INTEROP="$GAME/BepInEx/interop"
LOGS="$OUT/logs"
BEPINEX_URL="${BEPINEX_URL:-https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip}"
BEPINEX_ZIP="$CACHE/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788.zip"
BEPINEX_DIR="$CACHE/bepinex-be788"

say()  { printf '\n==> %s\n' "$*"; }
die()  { printf 'compile-check: %s\n' "$*" >&2; exit 2; }

[ "${1:-}" = "--clean" ] && { say "cleaning $OUT"; rm -rf "$OUT"; }

# ---------------------------------------------------------------- dotnet
if command -v dotnet >/dev/null 2>&1; then DOTNET="$(command -v dotnet)"
elif [ -x "$HOME/.dotnet/dotnet" ]; then DOTNET="$HOME/.dotnet/dotnet"
else die "dotnet not found on PATH or at \$HOME/.dotnet/dotnet (install .NET SDK 9+)."; fi
export PATH="$(dirname "$DOTNET"):$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBUILDDISABLENODEREUSE=1
"$DOTNET" --list-sdks | awk -F. '{ if ($1+0 >= 9) ok=1 } END { exit ok ? 0 : 1 }' \
  || die "need a .NET SDK >= 9 (ASL.slnx is the new .slnx format). Found: $("$DOTNET" --list-sdks | tr '\n' ' ')"
echo "dotnet: $DOTNET ($("$DOTNET" --version))"

mkdir -p "$CACHE" "$LOGS"

# ---------------------------------------------------------------- (a) real BepInEx core DLLs
if [ ! -f "$BEPINEX_DIR/BepInEx/core/BepInEx.Core.dll" ]; then
  say "downloading BepInEx 6 IL2CPP (be.788) into .cache/"
  if [ ! -s "$BEPINEX_ZIP" ]; then
    curl -fL --retry 3 --retry-delay 2 -o "$BEPINEX_ZIP.part" "$BEPINEX_URL" || die "download failed: $BEPINEX_URL"
    mv "$BEPINEX_ZIP.part" "$BEPINEX_ZIP"
  fi
  rm -rf "$BEPINEX_DIR"; mkdir -p "$BEPINEX_DIR"
  if command -v unzip >/dev/null 2>&1; then unzip -q -o "$BEPINEX_ZIP" -d "$BEPINEX_DIR" || die "unzip failed"
  else python3 -c 'import sys,zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])' "$BEPINEX_ZIP" "$BEPINEX_DIR" || die "unzip failed"; fi
  [ -f "$BEPINEX_DIR/BepInEx/core/BepInEx.Core.dll" ] || die "BepInEx/core/BepInEx.Core.dll missing from the archive"
fi

# ---------------------------------------------------------------- (c, part 1) fake game dir: core = REAL dlls
say "assembling fake game folder: $GAME"
mkdir -p "$CORE" "$INTEROP"
cp -f "$BEPINEX_DIR"/BepInEx/core/*.dll "$CORE"/

# ---------------------------------------------------------------- (b) stub interop assemblies
say "building stub interop assemblies"
rm -f "$INTEROP"/*.dll
"$DOTNET" build "$HERE/stubs/AllStubs.csproj" -c Release -v:minimal \
  -p:StubCoreDir="$CORE" -p:StubOutDir="$INTEROP" >"$LOGS/stubs.log" 2>&1 \
  || { grep -E ' error ' "$LOGS/stubs.log" | sort -u | head -40; die "stub assemblies failed to build (see $LOGS/stubs.log)"; }
echo "stubs: $(ls "$INTEROP"/*.dll | wc -l) assemblies -> $INTEROP"

# ---------------------------------------------------------------- (d) compile-check the real code
FAILED=()
SUMMARY=()

# build <name> <dotnet build args...>
build() {
  local name="$1"; shift
  local log="$LOGS/${name//[:\/]/_}.log"
  say "compile-check: $name"
  if "$DOTNET" build "$@" -c Release -v:minimal -p:GamePath="$GAME" -p:DeployToGame=false -p:DeploySamples=false >"$log" 2>&1; then
    local w; w=$(grep -cE ' warning ' "$log" || true)
    SUMMARY+=("OK    $name  (0 errors, $(grep -E ' warning ' "$log" | sort -u | wc -l | tr -d ' ') distinct warnings)")
  else
    FAILED+=("$name")
    SUMMARY+=("FAIL  $name")
    grep -E ' error ' "$log" | sed -E 's/ \[[^]]*\.csproj\]$//' | sort -u | head -60
    echo "(full log: $log)"
  fi
}

build "ASL.slnx" "$REPO/ASL.slnx"

# Samples that are not part of the solution (the solution only holds PropHunt).
for proj in "$REPO"/samples/*/*.csproj; do
  [ -f "$proj" ] || continue
  d="$(basename "$(dirname "$proj")")"
  if grep -Eq "<Project[[:space:]]+Path=\"samples/$d/" "$REPO/ASL.slnx"; then continue; fi
  build "sample:$d" "$proj"
done

# Script mods: every samples/*/ whose manifest says "type": "script". Mirrors ModLoader.LoadScriptMod: the
# manifest "entry" file if present, else every top-level *.cs; references = ASL.API + every interop assembly.
for manifest in "$REPO"/samples/*/manifest.json; do
  [ -f "$manifest" ] || continue
  grep -Eq '"type"[[:space:]]*:[[:space:]]*"script"' "$manifest" || continue
  dir="$(dirname "$manifest")"; d="$(basename "$dir")"
  entry="$(sed -nE 's/.*"entry"[[:space:]]*:[[:space:]]*"([^"]*)".*/\1/p' "$manifest" | head -1)"
  wd="$OUT/script/$d"; rm -rf "$wd"; mkdir -p "$wd"
  compile_items=""
  if [ -n "$entry" ] && [ -f "$dir/$entry" ]; then compile_items="    <Compile Include=\"$dir/$entry\" />"
  else for f in "$dir"/*.cs; do [ -f "$f" ] && compile_items="$compile_items
    <Compile Include=\"$f\" />"; done; fi
  [ -n "$compile_items" ] || { FAILED+=("script:$d"); SUMMARY+=("FAIL  script:$d (no .cs files)"); continue; }
  cat >"$wd/script.csproj" <<CSPROJ
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Generated by tools/compile-check/build.sh: compile-check of script mod '$d'. -->
  <PropertyGroup>
    <AssemblyName>ScriptMod_$d</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <OutDir>$wd/bin/</OutDir>
  </PropertyGroup>
  <ItemGroup>
$compile_items
    <ProjectReference Include="$REPO/src/ASL.API/ASL.API.csproj" Private="false" />
    <Reference Include="$INTEROP/*.dll"><Private>false</Private></Reference>
    <Reference Include="$CORE/BepInEx.Core.dll"><Private>false</Private></Reference>
    <Reference Include="$CORE/BepInEx.Unity.IL2CPP.dll"><Private>false</Private></Reference>
    <Reference Include="$CORE/Il2CppInterop.Runtime.dll"><Private>false</Private></Reference>
    <Reference Include="$CORE/Il2CppInterop.Common.dll"><Private>false</Private></Reference>
    <Reference Include="$CORE/0Harmony.dll"><Private>false</Private></Reference>
  </ItemGroup>
</Project>
CSPROJ
  build "script:$d" "$wd/script.csproj"
done

# Stub-surface self-test: makes sure the stubs still provide the Unity/uGUI/TMP surface new UI code relies on.
smoke="$HERE/smoke"
if [ -d "$smoke" ]; then
  wd="$OUT/smoke"; rm -rf "$wd"; mkdir -p "$wd"
  cat >"$wd/smoke.csproj" <<CSPROJ
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Generated by tools/compile-check/build.sh: stub-surface self-test. -->
  <PropertyGroup>
    <AssemblyName>CompileCheckSmoke</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <OutDir>$wd/bin/</OutDir>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$smoke/*.cs" />
    <Reference Include="$INTEROP/*.dll"><Private>false</Private></Reference>
    <Reference Include="$CORE/Il2CppInterop.Runtime.dll"><Private>false</Private></Reference>
    <Reference Include="$CORE/Il2CppInterop.Common.dll"><Private>false</Private></Reference>
  </ItemGroup>
</Project>
CSPROJ
  build "smoke:stub-surface" "$wd/smoke.csproj"
fi

# ---------------------------------------------------------------- result
say "summary"
printf '%s\n' "${SUMMARY[@]}"
if [ ${#FAILED[@]} -gt 0 ]; then
  printf '\nCOMPILE-CHECK FAILED: %s\n' "${FAILED[*]}"
  echo "(stubs are an approximation: if the error is a missing stub member, see tools/compile-check/README.md)"
  exit 1
fi
printf '\nCOMPILE-CHECK PASSED (against stubs - the real build against the game interop stays authoritative).\n'
