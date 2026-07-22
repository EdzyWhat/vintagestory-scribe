## Context

The lectern's row controls should read as hand-inked glyphs matching VS's handmade fiction.
The rendering path for a custom SVG icon in Vintage Story is undocumented and has two traps
that were discovered the hard way during a spike this session (proven in-game with a
temporary diagnostic that rendered a custom pin on both views):

1. **Asset category.** VS only scans assets under its 16 hardcoded `AssetCategory` codes
   (`blocktypes, config, dialog, entities, itemtypes, lang, patches, recipes, shaders,
   shaderincludes, shapes, sounds, textures, music, worldgen, worldproperties`). There is no
   `icons` category — a file under `assets/scribe/icons/` is never loaded, so `TryGet` returns
   null and the icon silently draws nothing. Vanilla stores SVG icons at `textures/icons/`.
2. **Asset lifetime.** `capi.Gui.Icons.SvgIconSource(IAsset asset)` captures the asset object
   and re-reads `asset.Data` at draw time. But `AssetManager.UnloadAssets()` runs after startup
   and sets `Data = null` on every non-patched asset. So an icon registered at `StartClientSide`
   has real bytes then, but by the first compose (seconds later) `.Data` is null and
   `SvgLoader.rasterizeSvg` throws `ArgumentNullException`, hard-crashing the client mid-compose.

Both findings are decompile-confirmed and recorded in `VSAPI-NOTES.md` "Icon-button glyphs".

The consuming UI (per-row pin/delete/drag buttons) does not currently exist: `ScribeBlockRowCell.Compose`
that once built them is dead code since the S2 merge (`466a1a4`); the live row is `ScribeRowElement`
(checkbox + text + ruling only). Re-adding those controls is owned by other changes.

## Goals / Non-Goals

**Goals:**
- A reusable, crash-safe helper to register a custom SVG icon by code string.
- Ship the four hand-inked SVG assets in the correct, VS-scannable location.
- Register the four codes (`scribepin`, `scribegrip`, `scribeclose`, `scribeedit`) at client init.
- Correct the art spec (`scribe-icon-svgs.md`) that documents the wrong path + crashing pattern.

**Non-Goals:**
- Repointing or creating any row-control button (owned by `lectern-gui-quick-edit-affordances`
  and `lectern-drag-reorder-feedback`; wiring dead code now is explicitly out of scope).
- Any change to row interactivity, networking, persistence, or `Core`.
- Judging final art quality at small sizes — a separate visual review (the feather/`edit`
  glyph legibility at ~15px is the standing concern).

## Decisions

**Decision: Register a re-resolving delegate, not `SvgIconSource(asset)`.**
Register `CustomIcons[code]` as a lambda that calls `capi.Assets.TryGet(loc, loadAsset: true)`
every draw, then delegates to `capi.Gui.Icons.SvgIconSource(asset)(...)` for the actual draw.
`TryGet` reloads an unloaded asset on demand (`if (!value.IsLoaded() && loadAsset)
value.Origin.TryLoadAsset(value)`), so this self-heals across the engine's unload. If
`asset?.Data is null`, draw nothing and log — never throw.
- *Alternative considered:* mark the asset patched / pre-bake to a `LoadedTexture` at init.
  Rejected — more moving parts, and the re-resolve is cheap because compose is infrequent
  (open/recompose, not per-frame).

**Decision: Assets under `assets/scribe/textures/icons/`, codes without a colon.**
Path resolves as `new AssetLocation("scribe", "textures/icons/<name>.svg")`. Code strings
(`scribepin`, etc.) omit the colon to stay clearly distinct from built-in glyph names and from
`AssetLocation` strings.

**Decision: Author single-flat-color silhouettes.** `DrawSvg` flood-recolors the whole SVG to
one caller-supplied color, so the SVG carries shape only; ink color comes from the drawing code.
This also gives per-state hover recolor for free once buttons exist.

**Decision: Ship + register all four now, wire none.** The mechanism and assets are the
reusable artifact; making the codes available (and validated) de-risks the affordance changes
that follow, without wiring anything to dead code.

## Risks / Trade-offs

- **[Registered-but-unused codes look like dead ends to a future reader]** → the spec's
  "decoupled from buttons" requirement + proposal sequencing note make the hand-off explicit;
  the consuming changes are named.
- **[Re-resolving each draw is wasted work if VS never actually unloads these]** → negligible:
  compose is infrequent and `TryGet` on an already-loaded asset is a dictionary hit; correctness
  (no crash) outweighs the micro-cost.
- **[Art may not read at small size]** → out of scope here; flagged for the visual review that
  the consuming affordance change will do when the icons actually appear on buttons.
- **[VSAPI-NOTES and scribe-icon-svgs.md drift out of sync]** → tasks include correcting the art
  spec; the notes entry is the canonical mechanism record.

## Migration Plan

No runtime migration — additive client-side registration + new assets. Rollback is removing the
registration call and assets; nothing depends on the codes yet. Lands after S2 completes to
avoid touching `src/Mod` while S2 is in flight in a shared working tree.

## Open Questions

- Final code-string spelling is proposed (`scribepin`/`scribegrip`/`scribeclose`/`scribeedit`);
  the consuming affordance changes must reference whatever this change registers.
