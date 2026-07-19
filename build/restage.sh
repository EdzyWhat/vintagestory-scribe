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
# Usage: ./build/restage.sh
set -euo pipefail

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
echo "Restaging ${MODID} -> $STAGE"

dotnet build src/Mod/Mod.csproj --configuration Release

mkdir -p "$STAGE"
cp "$MODINFO" "$STAGE/"
cp src/Mod/bin/Release/net10.0/*.dll "$STAGE/"

rm -rf "$STAGE/assets"
if [[ -d src/Mod/assets ]]; then
  cp -R src/Mod/assets "$STAGE/assets"
fi

echo "Staged: $(find "$STAGE" -type f | wc -l | tr -d ' ') files"
echo "Fully quit and relaunch the game client to pick up the new build (lang/assets load once at boot, not per-world-join)."
