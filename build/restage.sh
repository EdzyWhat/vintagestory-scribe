#!/usr/bin/env bash
# Rebuild the mod and restage it into the local Vintage Story Mods folder for manual
# playtesting. Unlike package.sh (which zips a Release build for distribution), this
# copies straight into ~/Library/Application Support/VintagestoryData/Mods/<modid> so the
# game picks it up on next launch.
#
# Always wipes the staged mod folder's assets/ before recopying (rather than cp -R'ing
# on top of an existing one) - cp -R onto an existing directory nests the source inside
# it instead of overwriting, which silently left a stale duplicate assets/assets/ tree
# and a stale lang file in place during earlier manual re-staging.
#
# Usage: ./build/restage.sh [Debug|Release]
#
# Configuration defaults to Release (the player-like build; VSImGui is excluded from it
# by a Condition in Mod.csproj, so a Release stage has zero ImGui presence). Pass Debug to
# stage a build that includes the VSImGui live-tuning sliders (RegisterDebugSliders) --
# required for the add-imgui-configlib-tuning task 5.1 investigation, since those sliders
# are #if DEBUG-gated AND the reference itself is Debug-only. Note VSImGui's overlay only
# actually renders on a machine with OpenGL >= 4.3 -- it draws nothing on Apple Silicon
# (OpenGL 4.1 over Metal); see VSAPI-NOTES.md "VSImGui debug overlay".
set -euo pipefail

CONFIG="${1:-Release}"
if [[ "$CONFIG" != "Debug" && "$CONFIG" != "Release" ]]; then
  echo "error: configuration must be Debug or Release (got '$CONFIG')" >&2
  exit 1
fi

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

MODINFO="src/Mod/modinfo.json"
if [[ ! -f "$MODINFO" ]]; then
  echo "error: $MODINFO not found (has the Mod been scaffolded yet?)" >&2
  exit 1
fi

MODID=$(grep -iE '"modid"' "$MODINFO" | head -1 | sed -E 's/.*"modid" *: *"([^"]+)".*/\1/')
MODID="${MODID:-scribe}"

STAGE="$HOME/Library/Application Support/VintagestoryData/Mods/$MODID"
echo "Restaging ${MODID} (${CONFIG}) -> $STAGE"

dotnet build src/Mod/Mod.csproj --configuration "$CONFIG"

mkdir -p "$STAGE"
cp "$MODINFO" "$STAGE/"
cp "src/Mod/bin/$CONFIG/net10.0/"*.dll "$STAGE/"

rm -rf "$STAGE/assets"
if [[ -d src/Mod/assets ]]; then
  cp -R src/Mod/assets "$STAGE/assets"
fi

echo "Staged: $(find "$STAGE" -type f | wc -l | tr -d ' ') files"
echo "Fully quit and relaunch the game client to pick up the new build (lang/assets load once at boot, not per-world-join)."
