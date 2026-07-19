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
- **Row-list visual restyle**, moving toward the flatter task-list language shown in a
  Slack reference screenshot (`~/Desktop/SlackTasks.png`, not committed to the repo): a
  circular-reading checkbox, generous row spacing with a subtle divider, icons that are
  hidden until the row is hovered (not always visible as today), and a focus ring scoped
  to the active field rather than the whole row.
- **Fix: the row checkbox does not scale with the text-size slider.** `GuiElementSwitch`
  is composed at a fixed pixel size regardless of `clientConfig.TextSizeScale`, unlike the
  rest of the row's text — the checkbox visually shrinks relative to the text at high
  scale and looms oversized at low scale.
- **New per-task "pin to HUD" flag**, `ScribeBlock.Pinned`, with a toggle affordance in
  the row. v1 only stores and toggles the flag — no on-screen HUD overlay renders it yet;
  that render is reserved for `v5 — Backpack`'s "pin ≤3 tasks to HUD" feature (ROADMAP.md),
  so this change doesn't collapse that later tier's differentiator.
- **New reserved (not yet used) per-task field**, `ScribeBlock.AssignedToUid` (nullable
  string), for the not-yet-designed `v4 — Writing desk` faction-assignment feature
  (ROADMAP.md's "Writing desk as faction task-assignment" idea, whose actual semantics —
  single player UID vs. a shared owner list vs. a real faction-mod dependency — are an
  explicitly open research question, not resolved here). This change only reserves the
  field in the data model; **the lectern's GUI does not expose it at all** — no column, no
  toggle, nothing composed. A future Desk-focused change defines the real assignment
  mutation and UI once that research lands.
- **BREAKING**: `ScribeDocumentCodec.Version` bumps to persist `Pinned` and
  `AssignedToUid`. No migration path — pre-release documents saved under the old version
  fail to deserialize (same fail-safe-not-corrupt behavior as any version mismatch; not a
  new failure mode, just one more version boundary). Accepted explicitly: this is
  pre-release test data, not real player worlds.
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
Rendering pinned tasks anywhere outside the lectern GUI (the actual HUD overlay is v5's
job). Any GUI, mutation method, or resolved semantics for `AssignedToUid` — v1 reserves
the field only; the v4 Desk work defines how it's actually set/used.

## Capabilities

### New Capabilities

- `lectern-gui-shell`: the lectern dialog's visual presentation and content-overflow
  behavior — portrait dialog shape, custom-image backdrop (with a swappable placeholder),
  a scrollable/clipped region for the task/note row list, and the row-list's visual
  language (checkbox scaling, hover-conditional icons, focus ring, pin-toggle affordance).
  Distinct from `lectern-block` (placement/persistence/networking) and
  `task-note-document` (the document model) — this capability governs presentation only.

### Modified Capabilities

- `task-note-document`: adds a `Pinned` flag and a `TogglePinned` mutation to a task
  block, and reserves an unused `AssignedToUid` field on every block (no mutation exposed
  yet). Both persist through the codec's next version.
- `lectern-block`: the lectern's GUI gains a way to toggle a task's pinned flag,
  alongside its existing add/edit/toggle-complete/delete/reorder operations.

## Impact

- **Code:** `src/Mod/GuiDialogScribeLectern.cs` (`DialogBounds`, `ComposeReadView`,
  `ComposeEditorView`, `EditorListWidth` — reshaped to portrait, backdrop swapped, scroll
  region added, row restyle), `src/Mod/ScribeBlockRowCell.cs` (row layout width
  assumptions may need adjusting for the narrower portrait column; checkbox scaling;
  hover-conditional icon visibility; pin-toggle affordance), `src/Core/ScribeBlock.cs`
  (`Pinned`, `AssignedToUid` fields), `src/Core/ScribeDocument.cs` (`TogglePinned`),
  `src/Core/ScribeDocumentCodec.cs` (version bump, new fields serialized).
- **New assets:** a placeholder backdrop texture (or a purely procedural Cairo-drawn
  equivalent — decide in design.md) under `src/Mod/assets/scribe/textures/gui/` or similar.
- **Coordination risk:** none remaining — `add-lectern-block`'s in-flight `src/Mod/`
  changes (playtesting bugfixes, tasks 8.10–8.18) and `reduce-agent-overhead`'s
  code-hygiene tasks have both landed since this proposal was first drafted; this change's
  implementation tasks are no longer blocked on either.
- **Dependencies:** none new — reuses the existing Cairo/`GuiComposer` primitives already
  available via `VintagestoryAPI`, per the mechanism confirmed by inspecting the reference
  mod's shipped assets (no new NuGet/mod dependency).
- **Compatibility: BREAKING for saved documents** (see above) — persistence *format*
  changes (new fields, version bump), though the document *model*'s existing operations
  (add/edit/toggle-complete/delete/reorder) are unaffected; this is additive plus a
  presentation-layer retrofit, not a restructure.
