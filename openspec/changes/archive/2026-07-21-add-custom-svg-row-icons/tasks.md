## 0. Preconditions

- [x] 0.1 Confirm S2 (`lectern-edit-in-place-rows`) is complete and merged/settled — this change
  touches `src/Mod` and must not run concurrently with S2 in the shared working tree.
  *(Done 2026-07-21: S2 closed at 36/36, spec synced, archived to
  `openspec/changes/archive/2026-07-21-lectern-edit-in-place-rows`.)*
- [x] 0.2 Revive the parked spike work: the uncommitted `ScribeModSystem.RegisterSvgIcon` helper +
  `scribepin` registration and the four SVGs under `src/Mod/assets/scribe/textures/icons/` were
  stashed at the end of the icon spike — restore them (or re-apply from stash) as the starting point.
  *(Done 2026-07-21: `git stash apply stash@{0}` restored the helper, `scribepin` registration,
  all four SVGs, `scribe-icon-svgs.md`, and the VSAPI-NOTES/ROADMAP/lectern-gui-polish doc edits.)*

## 1. Assets

- [x] 1.1 Confirm the four SVGs exist at `src/Mod/assets/scribe/textures/icons/{pin,grip,close,edit}.svg`,
  each `viewBox="0 0 24 24"`, single flat `#000000`, no `<style>`/`<defs>`/filters, artwork within the
  2..22 safe area. *(Confirmed 2026-07-21: all four present; each is a single `#000000` `<path>` on a
  `0 0 24 24` viewBox, no `<style>`/`<defs>`/filters.)*
- [x] 1.2 Verify the `assets/**` glob in `Mod.csproj` copies them into build output (they should already
  be picked up; confirm after a build). *(Confirmed 2026-07-21: `<None Include="assets/**">` at
  Mod.csproj:82 copied all four into `bin/Debug/net10.0/assets/scribe/textures/icons/`.)*

## 2. Registration mechanism

- [x] 2.1 Implement `ScribeModSystem.RegisterSvgIcon(ICoreClientAPI api, string code, AssetLocation loc)`:
  register `CustomIcons[code]` as a delegate that re-resolves via `api.Assets.TryGet(loc, loadAsset: true)`
  each draw, delegates to `api.Gui.Icons.SvgIconSource(asset)(...)`, and draws nothing + logs a warning
  when `asset?.Data is null` (never throws). *(Done — from the spike; verified present after stash apply.)*
- [x] 2.2 Call `RegisterCustomIcons(api)` from `StartClientSide`, registering all four codes:
  `scribepin` → `textures/icons/pin.svg`, `scribegrip` → grip, `scribeclose` → close, `scribeedit` → edit.
  *(Done 2026-07-21: extended `RegisterCustomIcons` from spike's single `scribepin` to all four codes.)*
- [x] 2.3 Build `src/Mod/Mod.csproj` (Debug) locally against the game DLL — 0 warnings / 0 errors.
  *(Confirmed 2026-07-21: `dotnet build Mod.csproj -c Debug` → Build succeeded, 0 Warning(s), 0 Error(s).)*

## 3. In-game verification

- [x] 3.1 Restage (`./build/restage.sh Debug`) and fully relaunch the client. *(Done 2026-07-21.)*
- [x] 3.2 Manually verify each of the four codes renders (a throwaway diagnostic draw on the read row,
  as used in the spike, is acceptable) — confirm no crash after the engine unloads asset data (i.e. the
  icon still draws seconds after open, not just at first paint). *(Confirmed 2026-07-21 via
  screenshots/debug/2026-07-21_22-19-03_custom-svg-row-icons-diagnostic-strip.png: all four icons
  — pushpin, six-dot grip, close X, pen/nib — draw as clean silhouettes in the read-view diagnostic
  strip and remain drawn after the dialog has been open past the asset-unload point. No crash.)*
- [x] 3.3 Remove any diagnostic draw code once confirmed; leave only the registration. *(Done
  2026-07-21: removed the temp four-icon diagnostic strip from ComposeReadView; Debug build clean 0/0;
  grep confirms no diagnostic code remains.)*

## 4. Documentation

- [x] 4.1 Correct `docs/specs/scribe-icon-svgs.md`: fix the asset path from `assets/scribe/icons/` to
  `assets/scribe/textures/icons/`, and replace the crashing `SvgIconSource(asset)` registration example
  with the re-resolving `RegisterSvgIcon` pattern. *(Done 2026-07-21: status banner now says mechanism +
  assets landed; the registration example, asset-path section, and "Asset location" block all corrected
  to `textures/icons/` + the re-resolve delegate.)*
- [x] 4.2 Confirm `VSAPI-NOTES.md` "Icon-button glyphs" matches the shipped mechanism (category rule +
  asset-unload crash + re-resolve fix) — it was updated during the spike; keep it as the canonical record.
  *(Confirmed 2026-07-21: the spike's VSAPI-NOTES edit records both gotchas + the re-resolve fix and
  matches the shipped `RegisterSvgIcon`.)*

## 5. Hand-off (no wiring here)

- [x] 5.1 Do NOT repoint any button. In `lectern-gui-quick-edit-affordances` and
  `lectern-drag-reorder-feedback`, note that the codes `scribepin`/`scribegrip`/`scribeclose`/`scribeedit`
  are now available for their row-control buttons to draw. *(Done 2026-07-21: added a hand-off note at
  the top of both changes' tasks.md, also flagging that both were drafted against the now-dead
  `ScribeBlockRowCell` and that re-adding the pin/delete columns is owned by the new
  `restore-row-affordance-columns` change.)*
- [x] 5.2 Update `ROADMAP.md` Open decision #2 / `lectern-gui-polish.md` item 8 to reflect that the icon
  assets + registration mechanism are landed and the remaining work is button-repointing in the affordance
  changes. *(Done 2026-07-21: ROADMAP #2 now says assets + mechanism landed, remaining work is
  repointing, and names `restore-row-affordance-columns` as the owner of the column restoration.)*
