# Presentation & fonts — animation, sound, custom typefaces

> Exploration/design spec (2026-07-21). NOT an OpenSpec change, NOT implemented code. When a
> piece of this is picked up, this file becomes the input to a real `openspec-propose`.
> See `docs/specs/README.md` for the shared structure and guardrails.

## Summary

This spec merges the **presentational-polish** cluster from `ROADMAP.md` — everything that
changes how Scribe *looks, moves, and sounds* without touching the data model:

1. **Custom checkbox with stamp/erase animation + sound** — replace the plain check glyph with
   a satisfying stamp-on-check / eraser-on-uncheck animation plus randomized sound variations.
   This is **S4 of the row-list rework**: S1 already shipped the custom-drawn glyph and left an
   explicit `// S4 HOOK` seam in `ScribeRowElement.DrawCheckboxGlyph` (quoted below) for exactly
   this work.
2. **Smooth drag-reorder animation** — animate the other rows spreading/shifting to preview
   where a dragged row will land, instead of only reordering on drop. This is **S3 of the
   row-list rework** and overlaps the on-hold `lectern-drag-reorder-feedback` change (which
   scoped the *lift-ghost / insertion-indicator / drop-settle* half of the same feature).
3. **Custom fonts per tier** — cuneiform-style block letters for the clay tablet's stamped
   text (v3); a rustic/hand-written script for books/notebooks (v2). A render-time font swap,
   gated on a license-terms check before any specific face is chosen.
4. **Future/light:** handwriting-neatening-with-practice (skill curve) and item aging/wear
   visuals — spec'd lightly as needs-investigation.

**No Core / data-model changes anywhere in this cluster.** Every item is client-side render
(and, for sound, a fire-and-forget audio call). The unifying constraint (see VS API hooks) is
that all of it must live in the **interactive render pass**, because VS bakes static content
into a texture once at compose time and cannot move or re-tint it per frame. Several items
**gate on art/audio/font assets**, not just code — called out explicitly below.

---

## VS API hooks

All confirmed against `src/Mod` code already in the repo, `VSAPI-NOTES.md`, and decompiles of
`VintagestoryAPI.dll` / `VintagestoryLib.dll` / `Lib/cairo-sharp.dll` (2026-07-21).

### Per-frame custom drawing + animation

- **`GuiElement.RenderInteractiveElements(float deltaTime)`** — the per-frame draw hook, already
  overridden by `ScribeRowElement` (blits its baked texture at `Bounds.renderX/renderY`) and
  `ScribeBlockRowCell`'s custom sub-elements. `deltaTime` is the frame delta — the animation
  clock. **This is the only pass that can move/redraw per frame.** Confirmed by the two-render-pass
  entry in `VSAPI-NOTES.md`: the *static* pass (`ComposeElements`) bakes to a cached texture at
  `drawY` (no scroll/animation term); the *interactive* pass draws at `renderY` every frame.
- **`GuiDialog.OnRenderGUI(float deltaTime)`** — dialog-level per-frame hook, already overridden in
  `GuiDialogScribeLectern` (drains `pendingRecomposeAction`). The natural home for a
  dialog-scoped animation clock (drag preview, drop-settle tween) that must coordinate across
  rows. Confirmed present at `GuiDialogScribeLectern.cs:340`. No new `RegisterGameTickListener`
  needed (and it would tick at a fixed rate, not frame rate — worse for smoothness).
- **Cairo drawing** — the glyph is drawn with `Cairo.Context` calls in
  `ScribeRowElement.ComposeElements` today (`RoundedRect`, `Stroke`, check-mark path). Animation
  either (a) re-bakes the row texture on state change and draws an *overlay* per frame in the
  interactive pass, or (b) draws the animated glyph entirely per-frame in the interactive pass.
  See Implementation for the recommended split.
- **`api.Render.Render2DTexturePremultipliedAlpha(...)`** — already used by `ScribeRowElement` to
  blit its texture; the mechanism for compositing an animated overlay/ghost at an arbitrary
  per-frame position and alpha.

### Playing a sound

- **`ICoreClientAPI.Gui.PlaySound(AssetLocation soundname, bool randomizePitch = false, float
  volume = 1f)`** — client-side, non-positional UI sound. Confirmed in decompiled `IGuiAPI`. This
  is the right call for a checkbox click: it's a UI event, not a world event, so it shouldn't be
  positional or audible to other players.
  - There is also `PlaySound(string soundname, ...)` and `PlaySound(SoundAttributes sound)`.
- **`IWorldAccessor.PlaySoundAt(AssetLocation location, double x,y,z, ..., bool randomizePitch =
  true, float range, float volume)`** — positional, world-audible. Confirmed in decompiled
  `IWorldAccessor`. **Not** what we want for a private lectern-UI checkbox (would leak the sound
  to nearby players and attenuate with distance); noted only because it's the more familiar call
  and we should deliberately *not* use it here.
- **Shipping a custom sound asset:** place `.ogg` files under
  `src/Mod/assets/scribe/sounds/…` and reference them as `AssetLocation("scribe:sounds/…")`
  (mod domain is `scribe`, confirmed in `modinfo.json`). The mod ships no `sounds/` dir today —
  this directory and its assets are new work. **Randomized variation** = ship N variant `.ogg`
  files (e.g. `stamp1.ogg`…`stamp3.ogg`) and pick one at random per event, plus
  `randomizePitch: true` for finer variation on top.

### Loading a custom font face (the key research finding)

The engine ships its own typefaces as **`.ttf` files** under `assets/game/fonts/` (confirmed:
`Lora-*.ttf`, `Almendra-*.ttf`, `Montserrat-*.ttf`). `CairoFont.SetupContext(ctx)` selects a
face by **name** via `ctx.SelectFontFace(Fontname, Slant, FontWeight)` (confirmed in decompiled
`CairoFont`) — i.e. name-based OS/registry resolution, which a mod cannot reliably extend with a
bundled file cross-platform.

**But there is a direct, name-independent path a mod CAN use, because Scribe already draws its
own Cairo surface.** `Lib/cairo-sharp.dll` exposes:

```
// Cairo.FreeTypeFontFace : FontFace
public static FreeTypeFontFace Create(string filename, int loadoptions)   // loads a .ttf via FreeType
// Cairo.Context
public void SetContextFontFace(FontFace value)
```

`FreeTypeFontFace.Create(path, loadoptions)` loads a TTF file directly through FreeType and wraps
it as a Cairo `FontFace`; `Context.SetContextFontFace(face)` then makes the current Cairo context
draw with it — **completely bypassing `SelectFontFace`'s name resolution.** Because `ScribeRowElement`
(and the clay-tablet UI to come) bakes text onto its *own* `ImageSurface`/`Context`, the mod can
call `ctx.SetContextFontFace(ourLoadedFace)` right before its `AutobreakAndDrawMultilineTextAt`
call and get the bundled typeface with no OS install and no dependency on the engine's font map.

**Caveats / open items on this path** (flagged as SUGGESTED VSAPI-NOTES ADDITION below):
- `SetContextFontFace` sets the raw Cairo font face but **not** the size/weight the way
  `CairoFont.SetupContext` does. The draw code would set the face, then still set font
  size/matrix (either via `CairoFont`'s size handling or `ctx.SetFontSize(scaled(size))`). The
  exact interaction of a manually-set FT face with `CairoFont.SetupContext` (which calls
  `SelectFontFace` and would clobber our face) needs a live test — likely we call
  `SetupContext` first (for size/color) then `SetContextFontFace` last to override the face.
- The face must be resolved from the mod's asset path to a real filesystem path (mod assets can
  live inside a packed `.zip`); if `FreeTypeFontFace.Create` needs a real file, we may have to
  read the asset bytes and write a temp file, or confirm FreeType can load from the unpacked dev
  path. Needs a decompile/test pass when picked up.
- Load the face **once** (cache it), not per-row-per-frame — FreeType face creation is not free.

### Existing seams this cluster builds on

- **`// S4 HOOK` in `ScribeRowElement.DrawCheckboxGlyph`** (lines ~160-198). Verbatim:

  > `S4 HOOK (stamp/erase animation): this is the single seam where the checkbox visual is
  > produced. The later stamp-on-check / erase-on-uncheck animation + sound (see ROADMAP) should
  > replace/augment this draw only -- hit-testing (OnMouseUpOnElement) and layout (RowTextLayout)
  > are intentionally independent of it and should not need to change.`

  The animation work lives here and in a new per-frame overlay draw; hit-testing and layout stay
  untouched, exactly as the seam promises.
- **`ScribeRowElement.OnMouseUpOnElement`** already reconstructs the glyph hit-rect and fires
  `onToggleClicked(blockIndex)`. That callback is where the check/uncheck sound + animation
  trigger originates.
- **`RowTextLayout`** — the single source of the checkbox column X/size; the animation reads it
  (never re-derives glyph position) so it scales with text size for free.
- **Drag reorder:** `GuiDialogScribeLectern` already tracks `draggedBlockIndex` + `hoverTargetIndex`
  (updated every `OnMouseMove` via `HitTestRowIndex`), fires `MoveBlock(from,to)` on `OnMouseUp`,
  and reads live `Bounds.absY` so hit-testing is scroll-aware. **All of that stays** — S3 only
  adds visual feedback on top. See `lectern-drag-reorder-feedback/design.md` for the detailed
  drag-lifecycle map.

---

## C# data structures

**No `src/Core/` changes.** Nothing here is game-agnostic model state; it is all render/audio in
`src/Mod/`. This is the load-bearing guardrail for the cluster.

### Animation state (in `ScribeRowElement`, per-row)

Transient, not persisted, not synced:

```csharp
// Checkbox stamp/erase animation (S4). All client-side, reset on dispose.
private float checkAnimT;          // 0..1 progress of the active animation, advanced by deltaTime
private bool  checkAnimActive;     // whether an animation is currently playing
private bool  checkAnimIsStamp;    // true = stamping (uncheck->check), false = erasing
private int   checkAnimVariant;    // which randomized visual variant is in play this run
```

An animation is armed when the row's `done` state flips (detected in the toggle path), advanced
in `RenderInteractiveElements(deltaTime)`, and cleared when `checkAnimT >= 1`.

### Drag-preview state (in `GuiDialogScribeLectern`, dialog-scoped)

Builds on the *already-present* `draggedBlockIndex` / `hoverTargetIndex`. New transient fields
(mirroring the on-hold change's proposed knobs):

```csharp
// S3 drag-reorder preview. Reset alongside the existing drag-state resets (EnterMode/OnGuiClosed).
private readonly Dictionary<int,double> rowPreviewOffsetY = new();  // per-row animated Y offset toward its previewed slot
private float dropSettleT;          // 0..1 drop-settle tween progress (from lectern-drag-reorder-feedback)
```

### Font selection concept (per-tier, render-time)

No new schema — a *client-side selection* of which loaded `FontFace` a given tier's UI draws
with. A small `ScribeFontRegistry` (Mod-side, client-only) loads and caches the bundled faces
once and hands the right one to the row/tablet draw code:

```csharp
// src/Mod, client-only. Loads bundled TTFs via Cairo.FreeTypeFontFace.Create once, caches them.
sealed class ScribeFontRegistry {
    FontFace Body { get; }        // rustic/handwritten script — notebooks/books (v2)
    FontFace Cuneiform { get; }   // block-letter stamped face — clay tablet (v3)
    // Tier -> face lookup; falls back to the engine default face if a bundle is missing.
}
```

Config-side, tier→font is a presentation knob, not model state; it can live as a
`ScribeClientConfig` toggle (e.g. "use themed fonts") consistent with the existing layout knobs.

---

## Implementation spec

### Item 1 — S4: custom checkbox stamp/erase animation + sound

**At the `// S4 HOOK` seam only.** Two-part draw:

1. **Static baked glyph (existing `DrawCheckboxGlyph`)** keeps drawing the *rest state* — empty
   box when undone, filled check when done — into the row's baked texture. This is what shows
   when no animation is playing (the common case) and costs nothing per frame.
2. **Per-frame animation overlay** in `RenderInteractiveElements`: while `checkAnimActive`, draw
   the animating glyph on top of the blitted row texture, at the glyph's on-screen rect (from
   `RowTextLayout` + `Bounds.renderX/renderY`, same math as the hit-test), interpolated by
   `checkAnimT`.
   - **Stamp (check):** the check mark scales/drops in with a slight overshoot (ease-out-back), a
     brief ink-spread/opacity ramp, maybe a 1-frame "impact" scale on the box. Reads as a stamp
     hitting paper.
   - **Erase (uncheck):** the check fades/smears out (a short "rubbed away" alpha+jitter), leaving
     the empty box.
   - **Randomized variation:** `checkAnimVariant` picks among a few slight variations (rotation
     jitter of the stamp, ink-blot offset) so repeats don't feel mechanical — mirrors the
     randomized-sound intent.
3. **Sound:** on toggle (in the `onToggleClicked` path / when the animation arms),
   `capi.Gui.PlaySound(new AssetLocation("scribe:sounds/stamp{1..N}"), randomizePitch: true)` for
   check, `…/erase{1..N}` for uncheck. Pick the variant `.ogg` at random.
4. **Wiring the trigger:** the read-view toggle round-trips through the server
   (`onToggleClicked` → packet → `done` flips → recompose). The animation should feel immediate,
   so **arm it optimistically on click** (client already knows the intended new state) rather than
   waiting for the server echo — consistent with how the checkbox already reads as responsive.
   Confirm this doesn't double-fire when the server echo recomposes the row (a recompose builds a
   fresh `ScribeRowElement`; carry a "just animated this index" guard at the dialog level, like
   the existing focus/scroll handoff state, or seed the new element's rest state without
   re-arming). **Open question flagged below.**

**Scales with text size for free:** the overlay reads glyph size from `RowTextLayout` /
`CheckboxSize` (already `ToggleWidth * TextSizeScale`), so the animation tracks the shipped
checkbox-scaling with no extra math.

**Assets required:** the stamp/erase `.ogg` variants (new `assets/scribe/sounds/`), and — if the
stamp uses art rather than pure Cairo vector drawing — a small stamp/ink texture. Pure-Cairo is
possible (cheaper, no art gate); a textured stamp looks better but adds an art dependency.

### Item 2 — S3: smooth drag-reorder preview

Two complementary halves, which should be **designed together** even if implemented in stages:

- **This spec's half (the roadmap "spreading rows" item):** the non-dragged rows animate to open
  a gap where the dragged row will land. Because rows are composed at a fixed viewport-relative Y
  per frame (see `VSAPI-NOTES.md` scroll entry — each row bakes at `rowY - scrollValue`), a live
  preview means: as `hoverTargetIndex` changes, shift the *composed* Y of rows between the source
  and target slot by one row-height (down if dragging up, up if dragging down), and recompose so
  the gap opens at the hover target. To animate (not snap), interpolate each row's offset toward
  its target via `rowPreviewOffsetY` advanced in `OnRenderGUI(deltaTime)`, then recompose at the
  interpolated Y each frame while a drag is active.
  - **Cost/consequence:** this means recomposing every frame during a drag (rows can only move via
    recompose, per the scroll research). That is the same tradeoff the scroll-thumb-drag fix
    already accepted (recompose per frame + hand the gesture to the new element). Reuse that
    discipline: carry the drag across recompose exactly as `OnRowListScroll` carries the scrollbar
    drag. **This is the non-trivial part.**
- **The on-hold `lectern-drag-reorder-feedback` half:** a **lift-ghost** (semi-transparent copy of
  the dragged row following the cursor), a **live insertion indicator**, and an **eased
  drop-settle** tween. That change deliberately chose the *"just the row lifts, others don't
  shift"* model as a tighter scope. **This spec supersedes that non-goal:** the roadmap item
  explicitly wants the other rows to spread. So the merged S3 = ghost + indicator (from the
  on-hold change) **plus** the spreading-rows animation (this spec). When S3 is proposed, fold the
  on-hold change into it rather than shipping them separately (the on-hold change's own header
  already says it was folded into the row-list-rework exploration).

**Shared constraints:** everything moving is drawn in the interactive pass; read live `Bounds.absY`
every frame (never cache composed Y) so it stays correct while scrolled; reset all preview state
alongside the existing drag-state resets in `EnterMode`/`OnGuiClosed`.

**Assets required:** none (pure layout animation + Cairo). Optional: a nicer insertion-indicator
glyph.

### Item 3 — custom fonts per tier

**Mechanism (see VS API hooks for the decompile detail):**

1. Bundle the chosen TTFs under `src/Mod/assets/scribe/fonts/…`.
2. On client start, `ScribeFontRegistry` loads each via `Cairo.FreeTypeFontFace.Create(path,
   loadoptions)` **once** and caches the `FontFace`.
3. The row/tablet draw code, after `font.SetupContext(ctx)` (which sets size/color but selects the
   default face by name), calls `ctx.SetContextFontFace(registry.Body /* or .Cuneiform */)` to
   override the face just before `AutobreakAndDrawMultilineTextAt`. Verify ordering live (the
   `SetupContext`-then-override sequence — flagged as an open item above).
4. **Tier mapping:** notebook/book (v2) → rustic script; clay tablet (v3) → cuneiform block
   letters. The lectern (v1, plain wood) can stay on the engine default or adopt the rustic script
   — a presentation decision. Selection is a client knob, not synced.

**License gate (hard prerequisite, per ROADMAP):** *before any specific face is chosen*, confirm
the font's license permits redistribution/bundling inside a mod `.zip` (SIL OFL and most
Google-Fonts Apache-2.0 faces are fine; many "free for personal use" faces are **not**). The
engine's own faces (Lora/Almendra/Montserrat) are shipped precedents but are the game's assets,
not ours to re-ship. Record the chosen face + license in `CREDITS` alongside the JeanPierre credit
already planned. **This item cannot start until a license-cleared face is picked for each tier.**

**Assets required:** two license-cleared TTFs (cuneiform-style; rustic script). This is the
primary gate on the whole item.

### Item 4 — light / future

- **Handwriting neatening with practice (skill curve):** *needs investigation.* Concept: text
  starts rougher (more jitter/irregularity in the Cairo draw or a rougher face) and "neatens" as
  the player writes more, as a soft progression reward. Open design questions: what counts as
  "practice" (character count? entries?), where that counter lives (this would be the *one* place
  the cluster risks needing persisted state — a per-player skill value — which must be weighed
  against the "no data-model changes" framing), and whether the visual is font-swap, per-glyph
  jitter, or a stroke-construction effect. Park until the font mechanism (Item 3) exists, since it
  builds on the same Cairo face-swap surface.
- **Item aging/wear visuals:** *needs investigation.* Clay tablets/paper showing wear over time.
  Likely a shader/texture-variant or overlay on the block/item model rather than GUI work, so it's
  a different render surface than the rest of this cluster. Ties to the roadmap's fragility
  mechanics (water-fragile clay, fire-fragile paper) and the fired-tablet "permanent archive"
  idea — spec alongside those when v3 fragility is scoped, not here.

---

## Asset requirements (gating summary)

These items need **art/audio/font assets, not just code** — the work cannot ship without them:

| Item | Asset | Blocks |
|------|-------|--------|
| S4 checkbox | `stamp{1..N}.ogg`, `erase{1..N}.ogg` (randomized variants) | the sound half |
| S4 checkbox (optional) | stamp/ink texture, if not pure-Cairo | the textured-stamp look only |
| Fonts | 2× license-cleared TTF (cuneiform, rustic script) + license verification | the entire font item |
| Item aging (future) | wear texture variants / overlay | that future item |

S3 drag preview and the pure-Cairo S4 visual need **no** assets — they can proceed on code alone.

---

## Dependencies & sequencing

- **S4 (checkbox)** and **S3 (drag preview)** are the two remaining stages of the **row-list
  rework** (S1 = read view, shipped/archived; S2 = edit-in-place). They should be proposed as
  those stages, on top of the row-list-rework infrastructure — not as standalone changes. S4 has a
  ready seam (`// S4 HOOK`); S3 needs the per-frame-recompose-during-drag machinery, which reuses
  the scroll-thumb-drag handoff discipline already in the code.
- **S3 fold-in:** the on-hold `lectern-drag-reorder-feedback` change is the ghost/indicator/settle
  half of S3; fold it into the S3 proposal rather than reviving it standalone (its own header says
  it was folded into this exploration).
- **Fonts** tie to the **tier rollout**: the rustic script lands with **v2 (notebook)**, the
  cuneiform face with **v3 (clay tablet)**. The `FreeTypeFontFace` loading mechanism can be built
  and proven on the v1 lectern first (swap its body text to a bundled face) to de-risk the API
  path before a tier depends on it. Gated on license clearance.
- **Handwriting/aging** are later/needs-investigation; sequence after fonts (handwriting) and
  after v3 fragility (aging).
- **No dependencies added.** Vanilla `VintagestoryAPI` + the already-vendored Cairo only. No Core
  changes, no persistence/sync, no codec bump, no Atlas surface (all client-visual → manual
  playtest coverage).

---

## Open questions

1. **S4 scope boundary — this cluster vs. row-list-rework staging.** S4 (checkbox animation) is
   both "a row-list-rework stage" and "a presentational-polish item." Should S4 be proposed as
   part of the row-list-rework change series (natural home, has the seam), with only the *fonts*
   and *future* items living as a separate "presentation" change? Or should the whole cluster be
   one polish change? (Leaning: S4/S3 stay in row-list-rework; fonts + future are their own.)
2. **Optimistic vs. server-confirmed animation trigger** for the checkbox. Arm on click (feels
   instant, risk of double-fire on the server echo recompose) vs. arm on the state actually
   flipping (correct, but a network round-trip of latency before the stamp plays). Needs a live
   feel test.
3. **Specific fonts.** Is there a face already in mind for either tier, or should sourcing start
   from SIL-OFL / Apache-2.0 catalogs (Google Fonts) with a shortlist for review? Cuneiform-style
   *Latin* faces (block/wedge letters that spell normal words) are rarer than true cuneiform
   Unicode faces — confirm we want a *stylized Latin* look, not actual cuneiform glyph substitution.
4. **Pure-Cairo vs. textured stamp** for the S4 checkbox — worth an art asset, or is a vector-drawn
   stamp animation good enough for the payoff?
5. **Asset-sourcing plan** generally: who makes/sources the `.ogg` stamp/erase sounds and any stamp
   texture — recorded, synthesized, or sourced from a CC0 library (and credited)?
</content>
</invoke>
