## Why

S1 reworked the lectern **read view** onto a single custom-drawn `ScribeRowElement` rendered
in the interactive pass, so rows clip natively and share one layout metric. The **editor
view still uses the old pre-rework path** — mixed static + interactive rows that pop at the
scroll boundary, look visibly different from the read view, and are why the branch can't
merge. S2 moves the editor onto the same row element, closing the read/edit visual gap and
letting us delete the temporary sample-content seed S1 added for testing. It also delivers
the edit-in-place typing model (a single floating input on the focused row) with the caret
conventions the user asked for.

## What Changes

- **Editor view renders on `ScribeRowElement` (edit mode).** The editor's rows become the
  same custom-drawn element as the read view, driven by the `ScribeRowMode.Edit` flag that
  S1 stubbed. Rows clip natively at the scroll boundary and scroll by a continuous `fixedY`
  shift — the same native-clip path the read view already uses.
- **One shared floating input, edit-in-place.** A single real `GuiElementTextInput` (a thin
  subclass, see below) is repositioned onto the row the player is editing. Every other row
  draws its text as a static label. The focused row **suppresses drawing its own label for
  that frame** (draws the checkbox/ruling but skips its text pixels) so the input and label
  never double-draw. Label and input align via the shared `RowTextLayout` so focus handoff
  has no baseline jump.
- **Caret conventions via a `GuiElementTextInput` subclass.** The spike (recorded in
  `VSAPI-NOTES.md`) confirmed the engine's `GuiElementEditableTextBase` already does
  selection, word-skip, clipboard, and line-jump — but routes caret navigation off
  `CtrlPressed` only (never `CommandPressed`) and hard-swallows any `AltPressed`. The
  subclass overrides `OnKeyDown` to route **Cmd+Arrow → line start/end** and
  **Alt/Option+Arrow → word-skip** onto the existing `MoveCursor`/Home-End logic before the
  base eats them, with **Shift** extending selection (already inherited).
- **Row-to-row navigation.** **Enter** commits the current row and advances to the next;
  **Shift+Tab** commits and retreats to the previous. Blur also commits; **Esc** reverts the
  row to its stored text. This is wired at the dialog/focus level (inherently cross-element).
- **Retire the old editor scroll path.** Remove the editor's cull/recompose/drag-handoff
  scroll code and the `OnRowListScroll` editor branch so both views share the single
  native-clip path. **BREAKING (internal only):** the old editor-view composition is
  replaced, not preserved.
- **Reunify the two views' width.** Collapse `ReadListWidth`/`EditorListWidth` into one
  shared width so read and edit are pixel-identical. This satisfies the parked
  `lectern-gui-quick-edit-affordances` single-width requirement.
- **Delete the temporary sample seed.** Remove `SeedSampleContentIfEmpty` and its ctor call
  (both tagged `TEMP SAMPLE SEED (row-list-rework S1)`) now that the editor can author rows.

Out of scope (later stages): drag-reorder lift-ghost/insertion feedback (S3), checkbox
stamp/erase animation (S4).

## Capabilities

### New Capabilities
_(none — this extends the existing lectern GUI shell capability)_

### Modified Capabilities
- `lectern-gui-shell`: adds requirements for editor-view rows rendered as custom-drawn
  interactive-pass elements (parallel to the S1 read-view requirements), the single
  floating edit-in-place input with static-label handoff, the caret conventions
  (Cmd/Alt arrow routing + Shift-select) and Enter/Shift+Tab row navigation with
  Esc-revert/blur-commit, and a single unified row-list width across both views.

## Impact

- **Code (all `src/Mod/`, no `src/Core/` changes):**
  - `ScribeRowElement.cs` — light up `ScribeRowMode.Edit`: label-draw + zone-suppression on
    the focused row; edit-mode row height keyed off the same `RowTextLayout`.
  - New `ScribeRowTextInput.cs` (or similar) — the `GuiElementTextInput` subclass overriding
    `OnKeyDown` for the Mac caret routing + row-nav key interception.
  - `GuiDialogScribeLectern.cs` — rewrite `ComposeEditorView` onto `ScribeRowElement`s inside
    `BeginClip`; single floating input repositioned on focus; delete the old editor scroll
    branch in `OnRowListScroll`; delete `SeedSampleContentIfEmpty` + ctor call; unify list
    width.
  - `ScribeClientConfig.cs` — collapse `ReadListWidth`/`EditorListWidth` into one width knob.
- **Mutation/sync:** edits continue to ride the existing lock-gated `ScribeEditDocumentMessage`
  → `ApplyEdit` path (editor holds the single-editor lock). No new network message; the
  read-view lock-free toggle from S1 is unaffected.
- **Testing:** Mac-playtestable via `build/restage.sh`. After S2 removes the seed and unifies
  the views, the `row-list-rework` branch becomes mergeable to `main`.
- **Unblocks:** `lectern-gui-quick-edit-affordances` single-width (5.3) and combined-retest
  (7.4) items, currently Backlogged pending this unification.
