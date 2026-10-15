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

# Unit tests: every tests/*/*.csproj (pure logic under src/; tests/* are deliberately NOT in ASL.slnx).
for tproj in "$REPO"/tests/*/*.csproj; do
  [ -f "$tproj" ] || continue
  tname="$(basename "$(dirname "$tproj")")"
  tlog="$LOGS/tests_$tname.log"
  say "tests: tests/$tname"
  if "$DOTNET" test "$tproj" -c Release -v:minimal -p:GamePath="$GAME" -p:DeployToGame=false >"$tlog" 2>&1; then
    SUMMARY+=("OK    tests:$tname  ($(grep -E 'Passed!|Passed:' "$tlog" | tail -1 | sed -E 's/^[[:space:]]+//'))")
  else
    FAILED+=("tests:$tname")
    SUMMARY+=("FAIL  tests:$tname")
    grep -E ' error |\[FAIL\]|Failed!|Assert\.' "$tlog" | sort -u | head -60
    echo "(full log: $tlog)"
  fi
done

# API compatibility: ASL.API is the stable contract for mods. Build ASL.API as it is on the baseline git ref
# (env ASL_API_BASELINE, default "main"; falls back to "origin/<ref>" as in a CI checkout) against the same stub
# game dir and diff it against the current ASL.API.dll with Microsoft's ApiCompat. Any breaking change fails.
# CP0006 ("cannot add interface member") is ignored by default (env ASL_API_NOWARN, comma/semicolon list of
# rule ids): the IAsl*/IMod* interfaces are implemented by ASL and only consumed by mods, so adding members is
# additive for modders. Removing/changing members (CP0002, CP0005, CP0008, ...) still fails.
BASE_REF="${ASL_API_BASELINE:-main}"
BASE_LABEL="ASL.API $BASE_REF -> current"
say "api-compat: $BASE_LABEL"
base_commit=""
for cand in "$BASE_REF" "origin/$BASE_REF"; do
  if base_commit="$(git -C "$REPO" rev-parse --verify --quiet "$cand^{commit}")" && [ -n "$base_commit" ]; then break; fi
  base_commit=""
done
if [ -z "$base_commit" ]; then
  SUMMARY+=("SKIP  api-compat (baseline ref '$BASE_REF' not found; in CI use fetch-depth: 0)")
else
  alog="$LOGS/api-compat.log"; : >"$alog"
  bdir="$OUT/baseline"; rm -rf "$bdir" "$OUT/api-current"; mkdir -p "$bdir"
  apicompat_ok=1
  # Install the tool once into .cache (v9 matches the SDK 9 runtime; the v10 tool needs a .NET 10 runtime).
  APICOMPAT="$CACHE/tools/apicompat"
  if [ ! -x "$APICOMPAT" ]; then
    "$DOTNET" tool install Microsoft.DotNet.ApiCompat.Tool --version "${APICOMPAT_VERSION:-9.0.304}" --tool-path "$CACHE/tools" >>"$alog" 2>&1 \
      || apicompat_ok=0
  fi
  if [ $apicompat_ok = 1 ] \
     && git -C "$REPO" archive "$base_commit" src/ASL.API Directory.Build.props | tar -x -C "$bdir" \
     && "$DOTNET" build "$bdir/src/ASL.API/ASL.API.csproj" -c Release -v:minimal -p:GamePath="$GAME" -p:DeployToGame=false \
          -o "$bdir/bin" >>"$alog" 2>&1 \
     && "$DOTNET" build "$REPO/src/ASL.API/ASL.API.csproj" -c Release -v:minimal -p:GamePath="$GAME" -p:DeployToGame=false \
          -o "$OUT/api-current" >>"$alog" 2>&1; then
    # Reference assemblies for the framework (System.Runtime etc.): the net6.0 targeting pack restored by the build.
    fwref=""
    for d in "${NUGET_PACKAGES:-$HOME/.nuget/packages}"/microsoft.netcore.app.ref/6.*/ref/net6.0 \
             "$(dirname "$(readlink -f "$DOTNET")")"/packs/Microsoft.NETCore.App.Ref/*/ref/net*; do
      [ -d "$d" ] && fwref="$d"
    done
    refs="$CORE,$INTEROP${fwref:+,$fwref}"
    echo "== apicompat (baseline ${base_commit:0:9}) ==" >>"$alog"
    DOTNET_ROOT="$(dirname "$(readlink -f "$DOTNET")")" "$APICOMPAT" \
      -l "$bdir/bin/ASL.API.dll" -r "$OUT/api-current/ASL.API.dll" \
      --noWarn "${ASL_API_NOWARN:-CP0006}" \
      --lref "$refs" --rref "$refs" >>"$alog" 2>&1
    rc=$?
    n=$(grep -cE '(^|[[:space:]])CP[0-9]{4}:' "$alog" || true)
    if [ "$rc" -eq 0 ] && [ "$n" -eq 0 ]; then
      SUMMARY+=("OK    api-compat ($BASE_LABEL: no breaking changes)")
    else
      [ "$n" -eq 0 ] && n="?"   # non-zero exit without a parsable rule id: still a failure
      FAILED+=("api-compat")
      SUMMARY+=("FAIL  api-compat ($n breaking change(s))")
      grep -E 'CP[0-9]{4}:' "$alog" | sort -u | head -60
      [ "$n" = "?" ] && tail -20 "$alog"
      echo "(full log: $alog)"
    fi
  else
    FAILED+=("api-compat")
    SUMMARY+=("FAIL  api-compat (could not install ApiCompat or build the baseline/current ASL.API)")
    grep -E ' error |rror:' "$alog" | sort -u | head -30
    echo "(full log: $alog)"
  fi
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
