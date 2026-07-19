## Context

The lectern's GUI (`src/Mod/GuiDialogScribeLectern.cs`) currently composes a generic
shaded panel (`AddShadedDialogBG`) in a wide/landscape layout (480–540px), with rows
stacked by absolute Y and no clipped/scrollable region — `add-lectern-block` task 8.15
already flags the latter as an unresolved gap, currently stopgapped by capping the
text-size slider (`MaxTextSizePercent = 150`).

Separately, a Slack task-list screenshot (`~/Desktop/SlackTasks.png`) was used as a
reference for the row list's visual language — restyling toward that look surfaced two
more concrete asks: a checkbox-scaling bug (confirmed in code, see Decision 5) and a new
per-task "pin to HUD" concept, which in turn raised a related but semantically-unresolved
idea already logged in `ROADMAP.md` — per-task assignment for the future faction-aware
Writing Desk (v4). Both new fields share one Core model/persistence change, bundled here
rather than as two separate codec version bumps.

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
- Restyle the row list toward a flatter, less generic look (checkbox scaling fix,
  hover-conditional icons, focus ring, row spacing) informed by a Slack reference.
- Add a per-task pin flag and its toggle affordance; reserve (but don't expose) an
  assignment field for the future Desk work.

**Non-Goals:**

- Pagination in any form — deliberately rejected in favor of continuous scrolling.
- Producing final per-tier backdrop art (clay tablet, notebook, desk, bulletin board) —
  future asset work, only unblocked by this change's architecture.
- Any change to `lectern-block`'s persistence/networking mechanics, or to
  `task-note-document`'s existing operations (add/edit/toggle-complete/delete/reorder) —
  this change adds two new fields/one new mutation, it does not restructure the model.
- Deciding whether v2's notebook reuses this same GUI shell — separate future decision.
- Rendering pinned tasks on an actual HUD — v5's job, not this change's.
- Resolving `AssignedToUid`'s real semantics (single UID vs. shared list vs. faction-mod
  dependency) or exposing any GUI/mutation for it — reserved field only; a future
  Desk-focused change owns this decision, per the still-open ROADMAP.md research question.

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

**5. Checkbox scaling: pass a computed `size` to `AddSwitch`, reusing the same scale
factor `RowHeight` already applies — no new mechanism.**
`GuiElementSwitch`'s constructor already accepts a `size` parameter (default 30,
confirmed via decompile: `/private/tmp/switch_decompile/...GuiElementSwitch.decompiled.cs`)
that the composer helper never varies today — `ScribeBlockRowCell.Compose` calls
`AddSwitch(onToggle, toggleBounds, key)` with no `size` argument, so every checkbox
renders at the same fixed pixel size regardless of `clientConfig.TextSizeScale`. Fix is a
one-line change at that call site: pass `size: ToggleWidth * textSizeScale` (the same
factor `RowHeight(block, textSizeScale)` already multiplies by), so the checkbox grows
and shrinks in lockstep with the row's text and height.
*Alternative:* a custom switch element with its own scaling logic — rejected, the
built-in parameter already does exactly this, no need to reimplement.

**6. Hover-conditional icon visibility via a render-time mouse check, NOT a recompose.**
The composer's existing `AddIf(condition)` pattern only evaluates its condition at
*compose* time — using it for "is the mouse over this row right now" would mean
recomposing on every mouse-move, which resets focus/caret on every relevant element
(the same problem already tracked as the still-open, general-form backlog item 8.5 in
`add-lectern-block`). Instead, each row's icon elements check live mouse position inside
their own `RenderInteractiveElements` override and skip drawing when the mouse isn't over
the row — the same technique the vanilla title bar itself already uses for its
close/menu-icon hover-glow (confirmed via decompile:
`GuiElementDialogTitleBar.RenderInteractiveElements` checks
`closeIconRect.PointInside(mouseX - Bounds.absX, mouseY - Bounds.absY)` every frame, no
recompose). This is new territory for `ScribeBlockRowCell` specifically (today only the
title bar and our own `ScribeDragHandleElement` do any custom per-element rendering), but
reuses an established in-engine pattern, not an invented one.
*Alternative:* `AddIf`-based conditional composition, triggered by a lightweight
mouse-move handler that only recomposes when hover state actually changes (similar to how
`OnRowTextChanged` already deduplicates before recomposing) — rejected as more complex
than necessary; a render-time check needs no recompose at all, so there's nothing to
deduplicate or debounce.

**7. Pin flag and reserved assignment field live on `ScribeBlock` directly, as a bool and
a nullable string respectively — not a separate lookup/side-table.**
Matches the existing model: `Done`, `Depth`, etc. are already per-block fields on
`ScribeBlock` itself, not a side collection keyed by index. `Pinned` is meaningful for
task blocks only (mirrors `Done`'s existing task-only convention); `AssignedToUid` is
reserved on every block for now since the future Desk semantics aren't decided — scoping
it to tasks-only prematurely would bake in an assumption ROADMAP.md explicitly flags as
still open.
*Alternative:* a separate `Dictionary<int, PinState>`-style side table — rejected, adds
indirection for no benefit; every other per-block flag already lives on the block.

**8. One codec version bump for both new fields, accepting the pre-release breaking
change rather than adding backward-compat parsing.**
`ScribeDocumentCodec`'s version-mismatch behavior (reject + fail-safe, no partial/corrupt
read) was designed for the v1→v2 change, which *restructured* the document (flat
tasks+note → ordered blocks) — no lossless migration was possible then, so "reject" was
the only honest option. This v2→v3 change is different: purely additive (every v2 block
maps 1:1 to a v3 block, `Pinned`/`AssignedToUid` defaulting to `false`/`null`), so a
compat parse path is actually feasible here for the first time. Explicitly decided
against building it anyway: this is pre-release test-world data, and the compat-parsing
machinery (a switch on version number, one parser per historical version) is real
ongoing-maintenance cost for a guarantee not otherwise needed yet. Revisit this decision
once the mod has real player worlds to protect.
*Alternative:* add the v2-compatible parse path now, since it's cheap for this
specific additive case — rejected per explicit user decision; deferred until backward
compatibility actually matters.

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
- **Can `GuiElementSwitch`'s existing rounded-rect rendering read as "circular" via its
  own constructor/params, or does the Slack-style circular checkbox need a custom
  element?** Not determined from the decompiled source alone — decide by testing the
  built-in element's visual result in-game before reaching for a custom implementation.

## Resolved (carried over from earlier exploration, not re-litigated)

- **"Fold Switch-to-Read/Edit into the collapse toggle" (parked ROADMAP idea) is unaffected
  by this change** — it's an interaction/control-grouping decision orthogonal to backdrop
  and scroll-region work; leave it parked as-is.

## Risks / Trade-offs

- **Portrait reshape changes row width, which changes text-wrap points for existing notes**
  → acceptable; no persisted data changes, only presentation. Existing documents render
  differently but aren't migrated or altered.
- **Scrollable/clipped region is new territory for this codebase** (mentioned as
  unexplored in `add-lectern-block/design.md`'s own risk notes, pointing at
  `GuiDialogTrader`'s scrollbar as the reference pattern) → check `VSAPI-NOTES.md` and
  `GuiDialogTrader` before inventing an approach; add findings to `VSAPI-NOTES.md` if any
  new gotchas surface, per that file's existing discipline.
- **Codec version bump breaks pre-release saved documents (BREAKING, accepted per
  Decision 8)** → any lectern document saved under the current version fails to
  deserialize after this change ships; not a corruption risk (the codec fails safe), but
  test-world content will need re-creating. Acceptable now; would need the deferred
  compat-parsing path (Decision 8's alternative) before this mod has real player worlds.
- **Render-time hover-visibility (Decision 6) is new territory for `ScribeBlockRowCell`**
  → mitigated by reusing the vanilla title bar's own established pattern rather than
  inventing one; add any new gotcha to `VSAPI-NOTES.md` per existing project discipline.
- **`AssignedToUid` is reserved with no consumer yet** → dead weight in the serialized
  format until the Desk work lands; acceptable since it's one extra nullable-string field
  in an already-bumping codec version, not a separate migration later.
