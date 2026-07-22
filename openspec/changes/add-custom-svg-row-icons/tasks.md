## 0. Preconditions

- [ ] 0.1 Confirm S2 (`lectern-edit-in-place-rows`) is complete and merged/settled — this change
  touches `src/Mod` and must not run concurrently with S2 in the shared working tree.
- [ ] 0.2 Revive the parked spike work: the uncommitted `ScribeModSystem.RegisterSvgIcon` helper +
  `scribepin` registration and the four SVGs under `src/Mod/assets/scribe/textures/icons/` were
  stashed at the end of the icon spike — restore them (or re-apply from stash) as the starting point.

## 1. Assets

- [ ] 1.1 Confirm the four SVGs exist at `src/Mod/assets/scribe/textures/icons/{pin,grip,close,edit}.svg`,
  each `viewBox="0 0 24 24"`, single flat `#000000`, no `<style>`/`<defs>`/filters, artwork within the
  2..22 safe area.
- [ ] 1.2 Verify the `assets/**` glob in `Mod.csproj` copies them into build output (they should already
  be picked up; confirm after a build).

## 2. Registration mechanism

- [ ] 2.1 Implement `ScribeModSystem.RegisterSvgIcon(ICoreClientAPI api, string code, AssetLocation loc)`:
  register `CustomIcons[code]` as a delegate that re-resolves via `api.Assets.TryGet(loc, loadAsset: true)`
  each draw, delegates to `api.Gui.Icons.SvgIconSource(asset)(...)`, and draws nothing + logs a warning
  when `asset?.Data is null` (never throws).
- [ ] 2.2 Call `RegisterCustomIcons(api)` from `StartClientSide`, registering all four codes:
  `scribepin` → `textures/icons/pin.svg`, `scribegrip` → grip, `scribeclose` → close, `scribeedit` → edit.
- [ ] 2.3 Build `src/Mod/Mod.csproj` (Debug) locally against the game DLL — 0 warnings / 0 errors.

## 3. In-game verification

- [ ] 3.1 Restage (`./build/restage.sh Debug`) and fully relaunch the client.
- [ ] 3.2 Manually verify each of the four codes renders (a throwaway diagnostic draw on the read row,
  as used in the spike, is acceptable) — confirm no crash after the engine unloads asset data (i.e. the
  icon still draws seconds after open, not just at first paint).
- [ ] 3.3 Remove any diagnostic draw code once confirmed; leave only the registration.

## 4. Documentation

- [ ] 4.1 Correct `docs/specs/scribe-icon-svgs.md`: fix the asset path from `assets/scribe/icons/` to
  `assets/scribe/textures/icons/`, and replace the crashing `SvgIconSource(asset)` registration example
  with the re-resolving `RegisterSvgIcon` pattern.
- [ ] 4.2 Confirm `VSAPI-NOTES.md` "Icon-button glyphs" matches the shipped mechanism (category rule +
  asset-unload crash + re-resolve fix) — it was updated during the spike; keep it as the canonical record.

## 5. Hand-off (no wiring here)

- [ ] 5.1 Do NOT repoint any button. In `lectern-gui-quick-edit-affordances` and
  `lectern-drag-reorder-feedback`, note that the codes `scribepin`/`scribegrip`/`scribeclose`/`scribeedit`
  are now available for their row-control buttons to draw.
- [ ] 5.2 Update `ROADMAP.md` Open decision #2 / `lectern-gui-polish.md` item 8 to reflect that the icon
  assets + registration mechanism are landed and the remaining work is button-repointing in the affordance
  changes.
