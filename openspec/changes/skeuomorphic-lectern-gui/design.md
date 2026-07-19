## Context

The lectern's GUI (`src/Mod/GuiDialogScribeLectern.cs`) currently composes a generic
shaded panel (`AddShadedDialogBG`) in a wide/landscape layout (480–540px), with rows
stacked by absolute Y and no clipped/scrollable region — `add-lectern-block` task 8.15
already flags the latter as an unresolved gap, currently stopgapped by capping the
text-size slider (`MaxTextSizePercent = 150`).

The reference mod "Wanderer's Sketchbook" was inspected directly (its shipped zip
extracted to `/tmp`, not decompiled beyond `strings` on the DLL — consistent with
`VSAPI-NOTES.md`'s check-before-decompiling discipline) to confirm a skeuomorphic backdrop
is achievable without inventing new engine capability:

- `journalbackground.png` (760×512) and `journalframe.png` (876×590) are two separate,
  landscape, two-page-spread images.
- DLL strings confirm `LoadBackgroundImage`/`LoadBookFrameImage` load these into Cairo
  `ImageSurface`s, composited behind/in front of a `GuiElementCustomDraw` canvas — not
  `AddShadedDialogBG`.
- A real `currentPage`/`PageCount` state machine (`OnNextPage`/`OnPrevPage`) backs its
  Prev/Next buttons — genuine pagination, not a scrollbar.

This confirms the *mechanism* transfers (Cairo `ImageSurface` behind/in front of ordinary
composer elements is a primitive Scribe's own `GuiElementDialogBackground` already uses
internally), but the *specific images* (landscape, two-page) don't fit the portrait shape
this change targets, and the *pagination* model is explicitly not what we're building.

## Goals / Non-Goals

**Goals:**

- Reshape the lectern dialog to portrait ("phone held vertically").
- Swap the generic shaded panel for a custom-drawn backdrop, shippable today with a cheap
  placeholder and swappable to real per-tier art later with no code change.
- Add a real scrollable/clipped region for the row list, resolving task 8.15.

**Non-Goals:**

- Pagination in any form — deliberately rejected in favor of continuous scrolling.
- Producing final per-tier backdrop art (clay tablet, notebook, desk, bulletin board) —
  future asset work, only unblocked by this change's architecture.
- Any change to `lectern-block`'s persistence/networking or `task-note-document`'s model —
  presentation-layer only.
- Deciding whether v2's notebook reuses this same GUI shell — separate future decision.

## Decisions

**1. Portrait dialog shape, replacing the current wide layout.**
`DialogBounds()` and `EditorListWidth`/read-view `listWidth` (currently 480–540px wide,
short) invert to a narrower, much taller aspect ratio. *Why:* matches the "phone held
vertically" direction and gives the row list genuine vertical room to justify scrolling
over the current pattern of just growing the whole dialog taller. *Alternative:* keep the
current wide shape and only add a scrollbar — rejected, doesn't deliver the skeuomorphic
goal on its own and leaves the "reads like a generic panel" problem unaddressed.

**2. Custom backdrop via a Cairo `ImageSurface`, drawn behind composer content — same
primitive the reference mod uses, adapted to Scribe's own composer setup.**
*Why:* confirmed working in-engine by the reference mod; avoids inventing a new rendering
approach. *Alternative:* keep `AddShadedDialogBG` and only reshape/add scrolling —
rejected, that's the "fix 8.15" change alone and doesn't address the "looks generic"
motivation that started this exploration.

**3. Placeholder backdrop now: a simple flat parchment-tone image (or a purely procedural
Cairo fill+border, TBD at implementation time) — not commissioned art.**
Architected as a single swappable asset reference (one texture path, or one draw call),
so later replacing it with real per-tier art (clay tablet for v3, notebook for v2, lectern
papers for v1, desk for v4, board for v6) is a one-file/one-call change, not a GUI-code
change.
*Why:* unblocks the layout/scroll work now without waiting on art production, which is out
of this change's scope entirely.
*Alternative:* delay this whole change until real art exists — rejected, decided
explicitly via user input to not block on art.

**4. Scrolling, not pagination — the single most consequential rejection of the reference
mod's approach.**
The row list (tasks + text sections, drag-reorderable) scrolls within a clipped region
instead of being split into fixed-size pages. *Why:* Scribe's document model is already an
ordered, reorderable list with per-row dynamic height (wrapped note text) — pagination
would require deciding how many rows fit per page, what happens when a row's wrapped
height changes near a page boundary, and whether drag-reorder can cross a page boundary.
Scrolling preserves the existing data model and row-composition code (`ScribeBlockRowCell`)
with an added clip/scroll wrapper, rather than a redesign.
*Alternative:* pagination (the reference mod's own approach) — rejected per explicit user
decision; the layout-model cost is real and the reference mod's motivation (a drawing
canvas needs one thing on screen at a time) doesn't apply to Scribe's list-based content.

## Open Questions

- **Does the "move the tool panel into a side rail" parked ROADMAP idea still make sense
  once the dialog is portrait-shaped?** A narrower dialog has less spare width for a side
  rail than the current wide layout did when that idea was written. Needs its own decision
  during implementation or a follow-up design note — not resolved here.
- **Exact portrait aspect ratio and pixel dimensions** — "roughly like a smartphone held
  vertically" is a direction, not a spec; pick concrete `ElementBounds` during
  implementation and confirm it reads well in-game before treating it as final.
- **Placeholder backdrop: static image asset vs. procedural Cairo draw?** Both satisfy
  "swappable later"; the choice affects whether the swap point is an asset file or a
  method — decide during implementation, not here.

## Resolved (carried over from earlier exploration, not re-litigated)

- **"Fold Switch-to-Read/Edit into the collapse toggle" (parked ROADMAP idea) is unaffected
  by this change** — it's an interaction/control-grouping decision orthogonal to backdrop
  and scroll-region work; leave it parked as-is.

## Risks / Trade-offs

- **Portrait reshape changes row width, which changes text-wrap points for existing notes**
  → acceptable; no persisted data changes, only presentation. Existing documents render
  differently but aren't migrated or altered.
- **Merge conflict with `add-lectern-block`'s in-flight `src/Mod/` edits** → mitigated by
  the same sequencing rule as `reduce-agent-overhead`: implementation tasks are blocked
  until those changes land; tasks.md marks this explicitly.
- **Scrollable/clipped region is new territory for this codebase** (mentioned as
  unexplored in `add-lectern-block/design.md`'s own risk notes, pointing at
  `GuiDialogTrader`'s scrollbar as the reference pattern) → check `VSAPI-NOTES.md` and
  `GuiDialogTrader` before inventing an approach; add findings to `VSAPI-NOTES.md` if any
  new gotchas surface, per that file's existing discipline.
