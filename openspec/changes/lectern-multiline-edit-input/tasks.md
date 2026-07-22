## 1. Rebase the input on the multi-line editable

- [ ] 1.1 Confirm working tree is clean and on `row-list-rework`. Re-read `ScribeRowTextInput.cs`
      and the editor compose/measure/recompose path in `GuiDialogScribeLectern.cs`
      (`ComposeEditorView`, `OnEditInputTextChanged`, `RecomposeEditorViewPreservingFocus`,
      `MoveEditFocusTo`, the `scrollFocusedRowIntoView` one-shot) so the change builds on the
      real S2 code.
- [ ] 1.2 Change `ScribeRowTextInput`'s base class from `GuiElementTextInput` to
      `GuiElementTextArea`; update the constructor's `base(...)` call to the TextArea signature.
      Keep all existing fields/callbacks (`onCommitAndAdvance`/`onCommitAndRetreat`/`onBlur`) and
      the Mac caret-translation logic unchanged.
- [ ] 1.3 Reconcile the `ComposeTextElements` override against `GuiElementTextArea`'s compose:
      strip TextArea's emboss + dark-fill box, keep its focus highlight, and confirm the
      highlight field/texture names the override references still resolve on the new base
      (`highlightTexture`/`highlightBounds`). Preserve the borderless look S2 established.
- [ ] 1.4 Verify the two clip corrections in `RenderInteractiveElements` (off-screen render skip;
      `PushScissor`/`PopScissor` re-assert) still apply on the TextArea base, and that the
      off-screen skip uses the input's live (grown) height, not a fixed one.

## 2. Enter commits, Shift+Enter newlines, Tab safe (multiline gotcha)

- [ ] 2.1 In `OnKeyDown`, split Enter by Shift. **Plain Enter / keypad Enter (no Shift):** ALWAYS
      consume (`args.Handled = true; return`) via `onCommitAndAdvance()` whether or not the
      callback succeeds — it must never fall through to the base's `OnKeyEnter()` newline.
      **Shift+Enter:** do NOT intercept; let it delegate to `base.OnKeyDown` so the base inserts a
      `\n` (multilineMode routes 49/82 → `OnKeyEnter()`). Confirm `TranslateMacCaretModifiers`
      passes a non-arrow Shift+Enter through unchanged.
- [ ] 2.2 Confirm Shift+Tab is fully consumed by the retreat branch and cannot fall through to a
      Tab character insert (the base treats Tab as insertable in multiline mode). Plain Tab: lean
      toward consuming it (no tab glyph inserted into a task line) unless focus traversal needs
      it; decide and document in the code.
- [ ] 2.3 Add commit-time normalization at the `Mod`-layer commit site (where `FlushIfDirty` /
      `OnEditInputTextChanged` finalize the row, NOT per-keystroke): trim trailing blank lines and
      trailing whitespace, preserve interior newlines (e.g. `"a\n\nb\n"` → `"a\n\nb"`). A
      `TrimEnd()`-style normalization; no leading trim, no interior collapse. Confirm this is the
      only place `Text` is normalized so all commit paths (Enter, Shift+Tab, blur, Esc) share it.
- [ ] 2.4 Confirm no Core/codec/wire change is needed for embedded `\n`: `ScribeBlock.Text` is
      serialized as a length-prefixed UTF-8 string (round-trips `\n`), and the read view's
      `Lineize` renders `\n` as a hard break. Spot-check `OnEditInputTextChanged` writes the raw
      (still-newline-bearing) string to the scratch doc.

## 3. Wire auto-height back into the row list

- [ ] 3.1 On text change (via `OnEditInputTextChanged` or a TextArea height-change hook), compute
      the focused row's new measured height using the SAME `ScribeRowElement.RowHeightFixed(...)`
      the compose path uses — never a second measurement path.
- [ ] 3.2 Only when that height differs from the current `rowHeights[focusedEditIndex]` (a wrap
      boundary was crossed), set the `scrollFocusedRowIntoView` one-shot and call
      `RecomposeEditorViewPreservingFocus()` so rows below shift, content height + scrollbar
      update, and the focused row scrolls into view. Do NOT recompose on every keystroke.
- [ ] 3.3 Confirm the caret position is preserved across this re-measure recompose (typing
      mid-string must not jump the caret to the end) — `RecomposeEditorViewPreservingFocus`
      already restores caret; verify it holds when the trigger is a height change, not a focus
      move.

## 4. Build, test, and playtest

- [ ] 4.1 Build Debug and Release clean; run `dotnet test` (Core.Tests) — all green. (No Core
      change expected; the run guards against an accidental one.)
- [ ] 4.2 Manually test in-game (Mac, `build/restage.sh Debug`, full relaunch): click into a long
      wrapped task in editor mode — confirm it stays wrapped across multiple lines (not a single
      line running off-screen) and the input aligns with where the static label was (no jump on
      focus or on blur).
- [ ] 4.3 Manually test in-game: type in a focused row until the text wraps onto a new line —
      confirm the row height grows naturally, the rows below shift down, and the scroll region
      updates; then delete back and confirm the row shrinks and rows below shift up.
- [ ] 4.4 Manually test in-game: with a long list scrolled so the focused row is near the bottom,
      type until the row grows — confirm it scrolls into view and the caret stays where you're
      typing. Confirm Enter still commits-and-advances (no newline inserted) and Esc still
      panic-closes with the edit saved.
- [ ] 4.5 Manually test in-game at a couple of text sizes and window widths: confirm the label
      and the input wrap at the SAME word boundary (no one-line reflow when focusing/blurring a
      row that sits exactly at a wrap boundary). If they disagree, reconcile the wrap width per
      design Decision 4 and retest.
- [ ] 4.6 Manually test in-game (Shift+Enter): press Shift+Enter mid-text — confirm a hard line
      break is inserted at the caret and the row grows; the caret stays put; typing continues on
      the new line. Confirm plain Enter still commits-and-advances (does NOT newline).
- [ ] 4.7 Manually test in-game (trailing trim + round-trip): add a trailing Shift+Enter (blank
      last line), commit, and confirm the row does NOT stay tall/empty (trailing trimmed) while a
      newline placed BETWEEN two words survives. Switch to read view and confirm the interior
      newline renders as a hard break; reload the world and confirm it persists.

## 5. Close out

- [ ] 5.1 Update `VSAPI-NOTES.md` if anything new was learned about `GuiElementTextArea`
      auto-height / multiline Enter behavior beyond what's already recorded.
- [ ] 5.2 Note in the change that it is complete and unblocks `restore-row-affordance-columns`
      (row-height behavior now stable).
