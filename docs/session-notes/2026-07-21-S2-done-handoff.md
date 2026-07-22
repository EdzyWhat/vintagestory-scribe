# Handoff — 2026-07-21 (S2 shipped; delete/pin restoration next)

For the next agent picking this up WITHOUT the prior chat transcript. Read this, then
`docs/explorations/lectern-row-list-rework.md` (the S1–S4 arc), then `git log` on the
`row-list-rework` branch. This file is a transient working record; delete once its content is
stale.

## TL;DR of current state

- **On branch `row-list-rework`** (NOT main). Mergeable now — see "Merge readiness" below.
- **S1 and S2 are both DONE, playtested, and confirmed.** Both the lectern **read view** and
  **editor view** are now on the single custom-drawn `ScribeRowElement` row list, rendered in
  the interactive pass so the engine's `BeginClip` clips them natively (no scroll-pop). The
  editor's edit-in-place floating input, caret conventions, commit/navigate, Esc-to-close,
  clip-bleed fix, add-task-scroll-into-view, and checkbox↔text margin are all confirmed
  (`lectern-edit-in-place-rows` tasks through 6.15).
- **Working tree:** clean except `openspec/changes/prove-bundled-font-seam/` (an uncommitted
  font-spike proposal awaiting user review — not part of the row-list work).

## What's carved out of S2 (NOT bugs — deferred by design)

- **6.12 Ctrl+Enter commit-and-insert-below** — a behavior-adding feature beyond S2's scope.
  The design decision is recorded in S2's tasks.md 6.12; when picked up it becomes its own
  `openspec-propose`. Depends on the (now-shipped) 6.11 add-task-scroll behavior.

## Merge readiness (task 7.2)

The `row-list-rework` branch is mergeable for the user's decision: the temp sample seed is
gone (`SeedSampleContentIfEmpty` deleted), read/editor views are unified onto one row element
and one shared width (`RowListWidth`), and all S1+S2 manual tests are confirmed. No known
functional regressions. The user makes the merge call.

## The delete/pin/hover restoration arc (what's NEXT)

Playtest surfaced that the S2 merge dropped the per-row **pin / delete / hover** affordances
from the live UI. We're restoring them — **visuals first, then functionality** — in three
sequenced changes:

1. **`add-custom-svg-row-icons`** *(proposed, 14 tasks)* — the icon-registration mechanism +
   4 hand-inked SVGs (`scribepin`/`scribegrip`/`scribeclose`/`scribeedit`). Spike code is in
   `stash@{0}`; task 0.2 revives it. Gated on S2 being settled (0.1) — now satisfied.
2. **`restore-row-affordance-columns`** *(new — to be proposed)* — re-adds the pin/delete
   (+ drag-handle) gutter columns to `ScribeRowElement` + `RowTextLayout`, drawing the SVGs
   from #1, with hover show/hide. **Visuals only** — buttons present, wired to stubs.
   This is genuinely new scope: `lectern-gui-quick-edit-affordances` only *scales* the column
   widths (it was written against the now-dead `ScribeBlockRowCell`), and
   `lectern-drag-reorder-feedback` is on-hold. Neither re-adds the columns.
3. **Wire functionality** — delete → new `ScribeDeleteBlockMessage`; pin → new pin message;
   server-authoritative, following the vanilla Sign pattern. Core ops already exist
   (`ScribeDocument.DeleteBlock` / `TogglePinned`, `ScribeBlock.Pinned`, codec-serialized).
   This is where `lectern-gui-quick-edit-affordances`' lock-vs-lightweight-path design
   decision bites (Read-view toggle is currently lock-free/non-mutating).

### Dead code available for reuse (from the S2 merge, commit `466a1a4`)

`ScribeBlockRowCell.Compose`, `ScribeDragHandleElement`, `ScribeHoverIconButton` are no longer
called but may be salvageable when re-adding columns. `ScribeClientConfig` still holds
`DeleteWidth`/`PinWidth`/`DragHandleWidth` knobs.
