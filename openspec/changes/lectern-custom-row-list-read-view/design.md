## Context

The lectern GUI (`GuiDialogScribeLectern.cs`) composes its row list from a mix of static-baked
elements (`AddStaticText`, `AddInset` dividers, `GuiElementSwitch` checkboxes) and interactive
elements. Vintage Story renders a composer in two passes: (1) a single static-texture blit,
baked once at `Compose()` from every element's `ComposeElements()`, drawn first and **always
unclipped**; (2) each interactive element's `RenderInteractiveElements()`, the only stage where
a `BeginClip`/`PushScissor` scissor is active. Because our rows put chrome in pass 1, the engine
can only cull whole overflowing rows, not clip partial ones — the scroll "pop" the
`skeuomorphic-lectern-gui` work chased but never fully fixed. Vanilla scrolling lists
(`GuiElementFlatList`, `GuiElementRichtext`) avoid this by drawing 100% of their content in pass
2, scrolling by a `fixedY` shift. This change moves our rows onto that model, starting with the
read view. Full exploration and research is in `docs/explorations/lectern-row-list-rework.md`;
this is stage S1 of a four-stage rework (S1 read view, S2 edit-in-place, S3 drag feedback, S4
checkbox animation).

## Goals / Non-Goals

**Goals:**
- Render each read-view row as one custom `GuiElement` drawn entirely in pass 2, so the existing
  `BeginClip` region clips rows natively — real partial clipping, continuous sub-row scrolling.
- A lined-paper hairline ruling that is a structural part of each row (scrolls with it), with
  text↔ruling padding that scales with the text-size preference, authored to be swappable for an
  image later.
- A custom-drawn checkbox glyph (replacing `GuiElementSwitch`) with an explicit seam for the S4
  stamp/erase animation; interactive in read view (click toggles done).
- A shared `RowTextLayout` metric that is the single source of truth for text x-offset, baseline,
  and font — so S2's floating edit input can align to the same pixels with no jump.
- Keep the element mode-aware (read/edit flag) from the start, even though only read is wired now.

**Non-Goals:**
- Edit-in-place / the single floating `GuiElementTextInput` and caret conventions (S2).
- Drag-reorder visual feedback (S3, on hold) and checkbox stamp/erase animation (S4).
- Any `src/Core/` change, persistence/codec change, or new network packet.
- Reworking the editor view — it keeps its current composition until S2. The read and editor
  views may look different during the S1→S2 window; that is expected and temporary.

## Decisions

**1. One `ScribeRowElement : GuiElement` with a read/edit mode flag.** Not a base + two
subclasses — subclassing would re-fork the "glued-together / different sizes" drift the rework
exists to kill. The element draws the shared skeleton (ruling, checkbox, text) in
`RenderInteractiveElements`, and lights up interactive zones by mode. In S1 only the read mode is
composed; the edit branch is present but stubbed. Mirrors the existing custom-element pattern in
`ScribeBlockRowCell.cs` (`ScribeDragHandleElement`, `ScribeHoverIconButton`). *Alternative
considered:* keep separate read/edit compositions — rejected as the exact divergence we're removing.

**2. Compose rows inside the existing `BeginClip`, scroll via `fixedY`.** Replace the read-view
per-row static loop with `AddInteractiveElement(new ScribeRowElement(...))` per row inside the
clip region; scrolling shifts the rows' `fixedY` by `-scrollValue` (the vanilla `GuiElementFlatList`
model). This retires the read-view scroll workarounds (recompose-on-scroll, the mask/cover idea,
snap-scrolling) — all were compensations for pass-1 chrome that no longer exists here. *Note:* the
editor view still uses the old path until S2, so those workarounds stay in place for it for now.

**3. Ruling is a structural per-row draw, not baked divider chrome.** Each `ScribeRowElement`
draws its own ruling line as part of its render, so it scrolls with the row for free and can be
replaced by an image draw later behind the same layout. Padding between text and ruling is
computed from the text-size scale (reuse the existing `TextSizeScale` accessor pattern), so it
tracks font size. Color/alpha/thickness become documented `ScribeClientConfig` knobs, following
the existing `Row*` layout-knob style. *Alternative considered:* keep `AddInset` dividers —
rejected: they are pass-1 static (won't clip) and can't scroll per-row.

**4. Custom checkbox glyph with an animation seam.** The read-view checkbox becomes a glyph the
element draws itself (checked/unchecked), replacing `GuiElementSwitch` and its `size`/`toggleable`
workarounds. Draw it through a small, clearly-named routine (e.g. `DrawCheckboxGlyph(done, rect)`)
so S4 can swap in the stamp/erase animation without touching hit-testing or layout. Hit-testing
for the click uses the element's own bounds check against the glyph rect (respecting
`InsideClipBounds` so buffered/clipped rows reject hits automatically).

**5. `RowTextLayout` as the single layout authority.** A small helper computes, from a row's
bounds + the text-size scale, the text x-offset, baseline Y, and font. Both the read-view text
draw and (in S2) the floating input's placement read from it — structural single-source-of-truth
rather than measure-and-match, so there is no baseline jump when S2 swaps a label for the live
input. Establishing it now, in S1, is the cheap insurance that makes S2's alignment tractable.

**6. Interactive read-view checkbox reuses the existing done-toggle mutation.** Clicking the glyph
calls the same server-authoritative toggle path the editor already uses; no new packet, no Core
change. Everything else in a read-view row is inert (no edit, drag, or hover icons) per the spec.

**7. Temporary sample-content seeding.** Seed a few hardcoded rows (mixed tasks + notes, at least
one long line to stress wrapping/clipping) so the read view is testable before S2 exists. Keep it
behind an obvious, easily-removed seam (clearly commented) since it is scaffolding, not a feature.

## Risks / Trade-offs

- **[Read/editor visual divergence during S1→S2]** → Accepted and expected; the two views only
  reunify when S2 lands. Documented as a Non-Goal so it isn't mistaken for a regression.
- **[Text wrapping/measurement in a hand-drawn row]** → Drawing wrapped multi-line text ourselves
  is fiddlier than `AddStaticText`. Mitigation: use the engine's font/text-measurement helpers via
  `RowTextLayout` rather than hand-rolling metrics; the long-line sample row surfaces problems in
  the first playtest.
- **[Checkbox hit-testing across the clip boundary]** → A click near a clipped row edge must not
  toggle a row that's scrolled out. Mitigation: rely on `IsPositionInside` ANDing
  `InsideClipBounds.PointInside` (engine-provided), which already rejects hits outside the clip.
- **[Config-default masking]** → New `ScribeClientConfig` ruling knobs won't appear in an existing
  on-disk config (which is read verbatim). Mitigation: new fields pick up their defaults since
  they're absent from old files; only changing an *existing* default would require a file reset.
- **[Sample seed leaking to release]** → Mitigation: single clearly-commented seam, removed/replaced
  when S2's real edit path lands; it never touches persisted document state.

## Open Questions

- Exact sample-content shape (row count, task/note mix, how long the long line is) — decide when
  writing tasks; low stakes since it's scaffolding.
- Whether the ruling should eventually be *fixed* (preprinted-page style) rather than scrolling —
  deferred; S1 scrolls it with the row, revisit only if the future image-swap wants fixed lines.
