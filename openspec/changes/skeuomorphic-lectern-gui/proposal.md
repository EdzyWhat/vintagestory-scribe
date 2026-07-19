## Why

The lectern's GUI currently looks like a generic engine dialog panel (`AddShadedDialogBG`),
which undersells the vanilla lectern shape it's built on and doesn't read as "a real
notebook/ledger." Separately, `add-lectern-block` task 8.15 flags a real functional gap:
rows are stacked by absolute Y with no scrollable/clipped region, currently stopgapped only
by capping the text-size slider. A reference mod ("Wanderer's Sketchbook",
mods.vintagestory.at/show/mod/42149 — installed and screenshotted, see
`screenshots/progress/2026-07-18_16-5[4,6]-*_skeuomorphic-notebook-mod-reference.png`)
shows both a skeuomorphic backdrop and a real content-overflow answer are achievable in
this engine, and inspecting its shipped assets/DLL strings directly confirmed the
mechanism is a Cairo `ImageSurface` composited behind/in front of ordinary composer
elements — not a new engine capability we'd have to invent.

## What Changes

- Reshape the lectern's dialog from its current wide/landscape layout to a **tall,
  portrait shape** ("roughly like a smartphone held vertically").
- Replace `AddShadedDialogBG` with a **custom-drawn backdrop** (a Cairo `ImageSurface`,
  composited the way the reference mod does it) instead of the generic shaded panel.
  Ship with a **simple placeholder image** (flat parchment tone + border) — not
  commissioned art — architected so a real per-tier texture can later replace it as a
  one-asset swap, not a code change.
- Add a **real scrollable/clipped content region** for the task/note row list, replacing
  the current absolute-Y stacking with no overflow handling. This resolves
  `add-lectern-block` task 8.15 and supersedes its stopgap text-size cap.
- **Explicitly NOT pagination.** The reference mod uses fixed-size pages with Prev/Next
  turning; this change deliberately chooses continuous vertical scrolling instead, to
  avoid redesigning the row/task data model around page boundaries.
- Reconcile three ROADMAP.md "Parked" bullets from earlier exploration:
  - The existing "skeuomorphic open-book UI... turn-page paging" bullet is **partially
    superseded**: its backdrop-swap goal is what this change implements; its
    pagination-over-scrolling framing is explicitly rejected (see above).
  - "Move the tool panel into a side rail" is an **open question** for design.md — a
    portrait dialog has less width to spare for a side rail than the current wide layout.
  - "Fold Switch-to-Read/Edit into the collapse toggle" is believed **unaffected** by this
    change; confirmed in design.md, not silently dropped.

Non-goals: producing final per-tier art (clay tablet, notebook, desk, bulletin board
backdrops) — those are separate future asset-production work, only enabled by this
change's architecture, not delivered by it. Whether/how the v2 notebook reuses this same
scrollable-portrait GUI shell — a separate future decision. Pagination in any form.

## Capabilities

### New Capabilities

- `lectern-gui-shell`: the lectern dialog's visual presentation and content-overflow
  behavior — portrait dialog shape, custom-image backdrop (with a swappable placeholder),
  and a scrollable/clipped region for the task/note row list. Distinct from
  `lectern-block` (placement/persistence/networking, unaffected) and
  `task-note-document` (the document model, unaffected) — this capability governs
  presentation only.

### Modified Capabilities

<!-- None — `lectern-block`'s and `task-note-document`'s existing requirements are
     unaffected; only the GUI's presentation layer changes. -->

## Impact

- **Code:** `src/Mod/GuiDialogScribeLectern.cs` (`DialogBounds`, `ComposeReadView`,
  `ComposeEditorView`, `EditorListWidth` — reshaped to portrait, backdrop swapped, scroll
  region added), `src/Mod/ScribeBlockRowCell.cs` (row layout width assumptions may need
  adjusting for the narrower portrait column).
- **New assets:** a placeholder backdrop texture (or a purely procedural Cairo-drawn
  equivalent — decide in design.md) under `src/Mod/assets/scribe/textures/gui/` or similar.
- **Coordination risk:** `src/Mod/GuiDialogScribeLectern.cs` and
  `src/Mod/ScribeBlockRowCell.cs` both have uncommitted edits in another terminal finishing
  `add-lectern-block` right now. This change's implementation tasks are **blocked** until
  those changes land — same constraint as the in-progress `reduce-agent-overhead` change.
- **Dependencies:** none new — reuses the existing Cairo/`GuiComposer` primitives already
  available via `VintagestoryAPI`, per the mechanism confirmed by inspecting the reference
  mod's shipped assets (no new NuGet/mod dependency).
- **Compatibility:** no change to persistence, networking, or the document model — a pure
  presentation-layer retrofit of the existing lectern GUI.
