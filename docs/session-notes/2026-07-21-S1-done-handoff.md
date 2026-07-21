# Handoff — 2026-07-21 (S1 shipped; row-list-rework continues)

For the next agent picking this up WITHOUT the prior chat transcript. Read this, then
`docs/explorations/lectern-row-list-rework.md` (the S1–S4 arc), then `git log` on the
`row-list-rework` branch. This file is a transient working record; delete once its content is
stale.

## TL;DR of current state

- **On branch `row-list-rework`** (NOT main). Do not merge to main yet — see "Why hold" below.
- **S1 is DONE, playtested, committed, and archived.** The lectern **read view** is fully
  reworked onto a custom-drawn row list. The **editor view is untouched** and still uses the old
  cull-don't-clip path — that's expected; S2 reworks it.
- Working tree: `TESTING.md` and `.claude/skills/what-to-test/SKILL.md` may show as modified —
  those were NOT part of this work; leave them for the user/what-to-test skill to own.

## What the row-list rework is (the through-line)

Replace the lectern's mixed static+interactive row list with a single custom-drawn row element
(`ScribeRowElement`) rendered entirely in the **interactive pass**, so the engine's `BeginClip`
scissor clips rows natively — fixing the scroll-boundary "pop", unifying read+edit onto one
renderer, enabling a lined-paper aesthetic + custom checkbox, and folding in smooth drag-reorder.
Staged S1→S4 (see the exploration doc for the full rationale and the research behind it):

- **S1 — read view (DONE 2026-07-21).** Custom rows, real clipping, lined-paper ruling, custom
  checkbox glyph, lock-free interactive checkbox. Archived at
  `openspec/changes/archive/2026-07-21-lectern-custom-row-list-read-view/`.
- **S2 — edit-in-place (NEXT).** Move the editor view onto `ScribeRowElement` too, with ONE real
  `GuiElementTextInput`/`TextArea` that floats onto the focused row (spreadsheet-style), aligned
  to the row's static label via the shared `RowTextLayout` so there's no baseline jump. Retire the
  editor's old cull/recompose/drag-handoff scroll path. **Removes the temporary sample-content
  seed** (see below).
- **S3 — drag-reorder feedback.** Unhold `lectern-drag-reorder-feedback` (currently ON HOLD, still
  in active changes) and build the lift-ghost/insertion-indicator on the landed custom rows.
- **S4 — checkbox stamp/erase animation.** Fills the seam already marked in
  `ScribeRowElement.DrawCheckboxGlyph` (search `// S4 HOOK`).

## Key files S1 added/changed (all in src/Mod/, no Core changes)

- **`ScribeRowElement.cs`** (NEW) — the custom row. Bakes its visuals (checkbox glyph + text +
  lined-paper ruling) into its OWN private `LoadedTexture` in `ComposeElements`, blits it in
  `RenderInteractiveElements` where the clip applies. Has a `ScribeRowMode` flag (Read wired; Edit
  stubbed for S2). `RowHeightFixed` is the SINGLE source of row height — it handles the
  scaled-vs-fixed unit conversion (see its doc comment; a naive measure clipped the last wrapped
  line, found+fixed in playtest).
- **`RowTextLayout.cs`** (NEW) — single source of truth for text x-offset / checkbox column / font.
  Read by the row's drawing now and, in S2, by the floating edit field so they pixel-align.
- **`ScribeToggleTaskMessage.cs`** (NEW) — client→server lock-free "toggle this task" packet.
- **`GuiDialogScribeLectern.cs`** — `ComposeReadView` rewritten to add `ScribeRowElement`s inside
  `BeginClip` and scroll by shifting `rowListContentBounds.fixedY` (native-clip path).
  `OnRowListScroll` now BRANCHES: read view = parent-fixedY shift (new); editor view = old
  recompose path (unchanged until S2). New `OnReadViewToggleTask` sends the toggle message. Temp
  `SeedSampleContentIfEmpty` (see below).
- **`BlockEntityScribeLectern.cs`** — `ToggleTaskFromReader(index)`: applies `Document.ToggleTask`
  with NO lock check, `MarkDirty(redrawOnClient:true)`.
- **`ScribeModSystem.cs`** — registers + server-handles `ScribeToggleTaskMessage`.
- **`ScribeClientConfig.cs`** — ruling knobs (`Ruling*`), `ReadCheckboxGlyphFill`,
  `ReadCheckboxHitboxScale`.
- **`Mod.csproj`** — added a `cairo-sharp` reference (the game-runtime 2D lib the custom element
  draws with; NOT a new mod dependency — it ships in the install, referenced like VintagestoryLib).

## Load-bearing facts / gotchas (don't re-derive these)

- **The read view holds NO editor lock and never mutates via the edit path.** The editor's
  `ScribeEditDocumentMessage` → `ApplyEdit` is lock-gated. That's WHY the read-view checkbox needed
  its own lock-free `ScribeToggleTaskMessage`. Decision: "anyone viewing can tick a task off, even
  while another player edits" (user, 2026-07-21). Known accepted caveat: a concurrent editor's
  full-doc autosave could overwrite a reader's toggle — see the archived design.md Risks.
- **Two render passes, two Y coords** — this whole rework exists because static-baked content
  can't be clipped OR scrolled per-frame; only interactive-pass content can. See VSAPI-NOTES.md
  "TWO passes with TWO Y coordinates" and "BeginClip doesn't visually clip". `ScribeRowElement`
  works precisely because it draws in the interactive pass.
- **Row height math is unit-sensitive.** `GetMultilineTextHeight` returns SCALED pixels; must
  divide by `RuntimeEnv.GUIScale` before handing to `ElementBounds.Fixed`. This is centralized in
  `RowHeightFixed` — don't reintroduce a separate measure.
- **TEMP SAMPLE SEED** — `GuiDialogScribeLectern.SeedSampleContentIfEmpty` (called from the ctor)
  injects junk rows into an empty local document so the read view is testable before S2's editor
  exists. Client-only, never persisted/sent. **DELETE it (and its ctor call) in S2** once the
  editor can author rows. Both sites are clearly commented `TEMP SAMPLE SEED (row-list-rework S1)`.

## Why hold the branch (don't merge to main yet)

The sample seed injects junk content, and the read/editor views intentionally look different
during the S1→S2 window (editor is still the old style). Merging S1 alone would ship both of those
to main. Wait until at least S2 removes the seed and reunifies the views. (Archiving the S1 *change*
is separate from merging the *branch* — the change is archived + specs synced; the branch stays open.)

## Testing state

S1 playtested on Mac (`bash build/restage.sh`, then fully relaunch — Mac-first now, VSImGui can't
render on Apple Silicon). Verified: rows render with ruling + custom checkbox; boundary rows clip
(slice, no pop); ruling scrolls with row + padding scales with text size; checkbox toggles done;
rest of row inert; editor view unregressed. Two-client lock-free toggle reasoned, not machine-tested.

## Open roadmap items logged this session (in ROADMAP.md)

- **Lectern placement direction** — always faces a fixed way; should face the placing player
  (vanilla lectern behavior). Small `BlockScribeLectern` placement/orientation change.
- **Writing-desk kanban / completed-task funnel** — Active/Backlog/Completed; "hide completed"
  toggle exposed only in the editor's options. Depends on this rework's shared renderer; likely v4.

## Suggested next action

Start **S2** with an `openspec-propose` for edit-in-place. FIRST spike the open risk flagged in the
exploration doc: whether VS `GuiElementTextInput`/`TextArea` natively supports the caret
conventions the user wants (Cmd/Ctrl+Arrow to line ends, Alt/Option word-skip, Shift-extend-select,
Enter/Shift+Tab row nav) or whether we must subclass — decompile `GuiElementEditableTextBase` if
the wiki/source is thin, and record findings in VSAPI-NOTES.md.
