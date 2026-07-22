## Why

In both read and editor view, a long task/note wraps onto multiple lines and its row grows to
fit — this is correct, driven by `ScribeRowElement.RowHeightFixed` →
`capi.Gui.Text.GetMultilineTextHeight`. But when the player clicks into a long row **to edit
it**, the floating input collapses to a single line: the text runs off the left/right edges and
most of it is invisible, and typing past the row's width scrolls horizontally instead of
wrapping. The static label and the focused input therefore disagree about how the same text is
laid out — a jarring, unusable inconsistency the tester flagged.

**Root cause (decompile-confirmed, `VintagestoryAPI.dll`):** the editor's in-place input is
`ScribeRowTextInput : GuiElementTextInput`. `GuiElementTextInput` sets `multilineMode = false`
on its shared base `GuiElementEditableTextBase`, so it is inherently single-line and
horizontally-scrolling. VS's multi-line editable is a *sibling* class, `GuiElementTextArea`, on
the same base: it sets `multilineMode = true` **and** ships an `Autoheight` mechanism whose
`TextChanged()` recomputes `Bounds.fixedHeight` from `GetMultilineTextHeight` on every keystroke
— exactly the wrap-and-grow behavior the row already uses for its static label.

## What Changes

- The editor's floating edit input wraps its text onto multiple lines (matching the static
  label) instead of scrolling a single line horizontally, by rebasing `ScribeRowTextInput` on
  the engine's multi-line editable (`GuiElementTextArea`) rather than the single-line
  `GuiElementTextInput`. All existing behaviors are preserved: the Mac caret conventions
  (Cmd/Alt/Shift+arrows), cross-row Enter/Shift+Tab navigation and Esc panic-close, blur-commit,
  the borderless look, and the two clip corrections (off-screen render skip + scissor re-assert).
- As the player types text that overflows onto a new line (or deletes back), the **focused
  row's height grows/shrinks dynamically and naturally**, the rows below it shift, and the
  scroll region updates — so the focused row behaves exactly like a static wrapped row, live.
- The focused row stays in view as it grows (reusing the existing `scrollFocusedRowIntoView`
  one-shot), and the caret/focus is preserved across the height-driven recompose (reusing the
  existing `RecomposeEditorViewPreservingFocus` path).

Because Enter is repurposed by S2 as commit-and-advance (not newline-insert), a multi-line
input here means **wrapped** lines, not player-inserted hard newlines — a single logical line of
text that happens to wrap. This keeps `ScribeBlock.Text` single-line (no embedded `\n`), matching
today's model and the read view's wrapping.

## Capabilities

### New Capabilities

(none — this corrects the behavior of the existing editor in-place input in `lectern-gui-shell`)

### Modified Capabilities

- `lectern-gui-shell`: the "Editor view edits in place with a single floating input" requirement
  gains that the floating input SHALL wrap long text onto multiple lines (aligned with the static
  label's wrapping) and SHALL grow/shrink the row height dynamically as typed text wraps/unwraps,
  shifting subsequent rows and the scroll region — rather than presenting a single horizontally-
  scrolling line.

## Impact

- `src/Mod/ScribeRowTextInput.cs`: change the base class `GuiElementTextInput` →
  `GuiElementTextArea`; reconcile the `ComposeTextElements` border-strip override against the
  TextArea's own compose (which already builds the highlight texture we hand-rolled); hook the
  per-keystroke height change so the dialog re-measures and recomposes the row list.
- `src/Mod/GuiDialogScribeLectern.cs`: on the input's height change, re-measure the focused
  row (and thus `rowHeights`/content height) and recompose preserving focus + scroll-into-view.
  Reuses the existing `RecomposeEditorViewPreservingFocus` and `scrollFocusedRowIntoView`.
- Wrap-parity risk: the static label wraps via `AutobreakAndDrawMultilineTextAt` and the input
  wraps via `GuiElementTextArea`'s own line-breaking. If they break at different points for the
  same width, focus/blur would jump by a line. Design.md addresses reconciling the wrap width so
  the handoff stays jump-free (S2 spent real effort on this via the shared `RowTextLayout`).
- No `Core` changes: `ScribeBlock.Text` stays a single logical string (wrapped, not newline-
  bearing); no codec change, no networking change, no persistence change. Client-render only,
  covered by manual playtest.
- Sequencing: lands BEFORE `restore-row-affordance-columns` — both touch the editor's
  `ScribeRowElement` / row-measure / recompose path, and settling row-height behavior first means
  the affordance-column work builds on a stable layout rather than rebasing over it.
