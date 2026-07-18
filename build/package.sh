#!/usr/bin/env bash
# Package the Scribe mod into a distributable zip: Releases/scribe_<version>.zip
#
# Run this LOCALLY (it needs the Vintage Story install to compile the Mod project).
# It reads the version from src/Mod/modinfo.json, builds the Mod in Release, and zips
# the compiled DLL together with modinfo.json and the assets folder.
#
# Usage: ./build/package.sh
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

MODINFO="src/Mod/modinfo.json"
if [[ ! -f "$MODINFO" ]]; then
  echo "error: $MODINFO not found (has the Mod been scaffolded yet?)" >&2
  exit 1
fi

# Extract modid and version from modinfo.json (grep/sed to avoid a jq dependency).
MODID=$(grep -iE '"modid"' "$MODINFO" | head -1 | sed -E 's/.*"modid" *: *"([^"]+)".*/\1/')
VERSION=$(grep -iE '"version"' "$MODINFO" | head -1 | sed -E 's/.*"version" *: *"([^"]+)".*/\1/')
MODID="${MODID:-scribe}"
echo "Packaging ${MODID} v${VERSION}"

# Build the mod in Release.
dotnet build src/Mod/Mod.csproj --configuration Release

# Stage the mod contents.
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
cp "$MODINFO" "$STAGE/"
cp src/Mod/bin/Release/net10.0/Scribe.dll "$STAGE/"
if [[ -d src/Mod/assets ]]; then
  cp -R src/Mod/assets "$STAGE/assets"
fi

# Zip it up.
mkdir -p Releases
OUT="$REPO_ROOT/Releases/${MODID}_${VERSION}.zip"
rm -f "$OUT"
( cd "$STAGE" && zip -r -q "$OUT" . )
echo "Wrote $OUT"
