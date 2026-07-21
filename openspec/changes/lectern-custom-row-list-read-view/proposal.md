## Why

The lectern's row list mixes static-baked chrome (checkbox outlines, dividers, text labels)
with interactive elements, so the engine can only *cull* overflowing rows, not *clip* them —
producing the scroll-boundary "pop" the `skeuomorphic-lectern-gui` work could never fully
resolve. Vanilla scrolling lists avoid this by rendering 100% of their content in the
interactive pass, where `BeginClip` actually applies. This change is the first stage (S1) of
reworking the row list onto a single custom-drawn row element that renders entirely in the
interactive pass — fixing clipping for real, and along the way replacing the "gamey" base-game
row look with a grounded lined-paper aesthetic. S1 is scoped to the **read view** only, so the
lower-risk view proves the element and its look before the editor (S2) depends on it.

## What Changes

- Introduce a single custom-drawn `ScribeRowElement` (a `GuiElement` rendered wholly in the
  interactive pass) that draws each task/note row itself: a lined-paper hairline ruling, the
  row text, and — for tasks — a checkbox glyph. In S1 this element is wired into the **read
  view only**; it carries a read/edit mode flag whose edit path is stubbed for S2.
- Because rows now render in the interactive pass, the row list is **clipped natively** by the
  existing `BeginClip` region — scrolling becomes smooth with no off-screen cull/pop. This
  finally, fully delivers the existing "scrolls within a clipped region" requirement for the
  read view and supersedes the recompose-on-scroll / mask workarounds for it.
- The row ruling is a **real structural part of the row** (drawn per row, scrolls with the
  rows), with padding above/below that **scales with the text-size preference**, authored so it
  can later be swapped for an image without touching layout logic.
- Replace the read view's `GuiElementSwitch`-based checkbox with a **custom-drawn checkbox
  glyph**, with a clearly-marked seam for the later stamp/erase animation (S4).
- The read-view checkbox is **interactive**: clicking it toggles the task's done state
  (server-authoritative, as today). Nothing else in the read view becomes interactive.
- Establish a shared `RowTextLayout` metric (single source of truth for text x-offset,
  baseline, and font) that the row's text draw reads from now, so S2's floating edit input can
  align to the exact same pixels.
- Seed hardcoded sample rows (mixed tasks + notes, including long lines) so the read view is
  testable before edit mode exists. This is temporary scaffolding, removed/replaced once S2's
  edit-in-place lands.

Out of scope (later stages, deliberately excluded here): edit-in-place / floating text input
and caret conventions (S2); drag-reorder visual feedback (S3, currently on hold); checkbox
stamp/erase animation (S4).

## Capabilities

### New Capabilities
<!-- none — this builds on the existing lectern GUI capability -->

### Modified Capabilities
- `lectern-gui-shell`: the read view's row list gains custom-drawn rows rendered in the
  interactive pass (delivering real clipping), a structural lined-paper ruling that scales its
  padding with text size, a custom-drawn checkbox glyph, and an interactive read-view checkbox
  that toggles done. The existing checkbox-scaling requirement is unaffected in intent (the
  custom glyph still scales with text size); the new behaviors are added requirements.

## Impact

- **Code (`src/Mod/` only):** new `ScribeRowElement` custom `GuiElement`; `GuiDialogScribeLectern.cs`
  read-view composition switches from the per-row static-element loop to adding `ScribeRowElement`
  instances inside the existing `BeginClip` region (scroll via `fixedY` shift, retiring the
  read-view scroll workarounds); new shared `RowTextLayout` helper; config knobs for ruling
  color/alpha/thickness and its text-scaling padding; temporary sample-content seeding.
- **No `src/Core/` changes:** Core's document/task model is untouched; toggling done from the
  read view reuses the existing server-authoritative mutation + sync path. No new dependencies.
- **No persistence/network changes:** no codec version bump, no new packets — this is a
  client-side rendering + existing-mutation change, covered by manual Mac playtest
  (`build/restage.sh`).
- **Follows staging:** S1 is independently shippable; S2–S4 build on the element it lands.
