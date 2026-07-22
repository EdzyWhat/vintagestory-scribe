## Why

The lectern GUI's row-control affordances (pin, drag-handle grip, delete, edit) currently
rely on built-in glyph-font codes and bare text (`wpCircle`, `eraser`, `"::"`, the word
"Edit") that read poorly and clash with Vintage Story's handmade-with-primitive-tools
fiction. The icon-font audit (`docs/specs/lectern-gui-polish.md` item 8) decided to move all
four onto a single family of custom hand-inked SVGs. Authoring and wiring custom SVG icons
in VS turned out to be non-obvious — the naive registration pattern silently draws nothing
in one case and hard-crashes the client in another — so the reusable **mechanism** for
registering custom SVG icons is worth landing on its own, decoupled from the row-control UI
work that will consume it.

## What Changes

- Add a reusable `ScribeModSystem.RegisterSvgIcon(api, code, assetLocation)` helper that
  registers a custom SVG under a `CustomIcons` code, safe against VS's asset-unload behavior
  (re-resolves the asset each draw rather than capturing it; draws nothing instead of
  crashing if the asset is missing).
- Ship the four hand-inked SVG assets under `src/Mod/assets/scribe/textures/icons/`
  (`pin.svg`, `grip.svg`, `close.svg`, `edit.svg`), authored as single-flat-color silhouettes
  so the button/caller supplies the ink color via flood-recolor.
- Register the four icons under stable code strings (`scribepin`, `scribegrip`,
  `scribeclose`, `scribeedit`) at client init.
- Correct `docs/specs/scribe-icon-svgs.md`, which still documents the wrong asset path
  (`assets/scribe/icons/`) and the crashing naive `SvgIconSource(asset)` registration.
- **Explicitly does NOT repoint any button onto these codes.** The per-row pin/delete/
  drag-handle buttons do not exist in the live UI — `ScribeBlockRowCell.Compose` (which built
  them) has been dead code since the S2 merge (`466a1a4`); the live row is `ScribeRowElement`
  (checkbox + text + ruling only). Re-adding those controls is owned by the existing changes
  `lectern-gui-quick-edit-affordances` (pin/delete/drag columns) and
  `lectern-drag-reorder-feedback` (drag handle). This change registers the icons and hands
  the codes off to those changes rather than wiring dead code.

## Capabilities

### New Capabilities

(none — no new capability domain; this adds a rendering mechanism + assets consumed by the
existing `lectern-gui-shell` capability)

### Modified Capabilities

- `lectern-gui-shell`: adds a requirement that the mod register a documented set of custom
  SVG row-control icons at client init, available by stable code string for GUI elements to
  draw. Does not change any row's current interactive behavior.

## Impact

- `src/Mod/ScribeModSystem.cs`: adds `RegisterSvgIcon` + a `RegisterCustomIcons` call in
  `StartClientSide` registering the four codes. (The working `scribepin` registration +
  helper already exist as uncommitted code from this session's spike; this change formalizes
  and completes them to all four.)
- `src/Mod/assets/scribe/textures/icons/{pin,grip,close,edit}.svg`: four new assets. Path is
  under the `textures` `AssetCategory` (a bare `icons/` folder is never scanned by VS and
  `TryGet` returns null → silent empty icon).
- `docs/specs/scribe-icon-svgs.md`: corrected to the real asset path and the safe registration
  pattern.
- `VSAPI-NOTES.md` "Icon-button glyphs": the two hard-won findings (real `AssetCategory`
  requirement; asset-`.Data`-unload crash and the re-resolve fix) are recorded here — keep in
  sync.
- No `Core` changes — this is a `Mod`-layer client-render/asset change only, no networking,
  no persistence.
- **Sequencing:** lands AFTER S2 (`lectern-edit-in-place-rows`) completes. The button
  repointing that consumes these codes is deferred to the two affordance changes named above.
