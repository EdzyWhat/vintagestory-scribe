# Design — lectern-multiline-edit-input

## Context

S2 (`lectern-edit-in-place-rows`, archived) built the editor's edit-in-place: one floating
`ScribeRowTextInput` repositioned onto the focused row, aligned to the static label via the
shared `RowTextLayout` metric so the label↔input handoff has no jump. That input subclasses
`GuiElementTextInput`, which is single-line. Rows are measured multi-line
(`ScribeRowElement.RowHeightFixed` → `GetMultilineTextHeight`), so a wrapped row shrinks to one
horizontally-scrolling line the moment it's focused. This change makes the focused input wrap and
grow like the label, keeping every S2 behavior intact.

All facts below are decompile-confirmed from `VintagestoryAPI.dll` (per the DLLs-first guardrail).

## Decision 1 — Rebase on `GuiElementTextArea`, not a hand-rolled multiline flag

`GuiElementTextInput` and `GuiElementTextArea` share the abstract base
`GuiElementEditableTextBase`, which holds `internal bool multilineMode` and all the caret,
selection, word-skip, clipboard, and line-jump logic. `GuiElementTextArea`:

- sets `multilineMode = true` in its constructor,
- ships `public bool Autoheight = true` and overrides `TextChanged()` to do
  `Bounds.fixedHeight = Math.Max(minHeight, textUtil.GetMultilineTextHeight(Font, join(lines),
  Bounds.InnerWidth)); Bounds.CalcWorldBounds();` — i.e. it re-heights itself on every keystroke,
- draws its own faint-white focus highlight (the exact effect S2's `ComposeTextElements` override
  hand-rolled to replace the boxed input border).

So `ScribeRowTextInput : GuiElementTextArea` gets wrapping + auto-height for free, and the border
concern is now *removing* TextArea's emboss+dark-fill (same as before) rather than reproducing an
input highlight. `multilineMode` is `internal`, so subclassing TextArea is the *only* way to get
it set from mod code — we cannot flip it on a `GuiElementTextInput`.

**Rejected:** keep `GuiElementTextInput` and try to force wrapping — impossible without the
`internal` flag, and we'd have to reimplement `Autoheight`. Rebasing is strictly less code.

## Decision 2 — Enter stays commit-and-advance; it must NEVER insert a newline

Critical behavior difference: in `OnKeyDownInternal`, multiline mode routes Enter (KeyCode 49) and
keypad Enter (82) to `OnKeyEnter()`, which **inserts a `\n`**. Single-line mode deferred Enter to
the caller (unhandled). S2's `ScribeRowTextInput.OnKeyDown` already intercepts Enter *before*
delegating to base and returns once `onCommitAndAdvance()` handles it — so in the normal path the
base never sees Enter and no newline is inserted. The one gap: today, if `onCommitAndAdvance()`
returns `false`, the code falls through to `base.OnKeyDown`, which under multiline would now insert
a newline. **Fix:** make the Enter/keypad-Enter branch *always* mark `args.Handled = true` and
`return` in multiline mode regardless of the callback's result — Enter is never a text key here.
Same for Shift+Tab (already returns on handled; confirm it can't fall through to a Tab insert —
line 636 shows multiline treats Tab as handled/insertable, so Shift+Tab must be fully consumed).

This preserves the S2 invariant that `ScribeBlock.Text` holds a single logical line (wrapped for
display, never newline-bearing) — no Core/codec/wire change. `SetValue`/`LoadValue` already
normalize `\r\n`→`\n`, but we simply never let a newline be typed.

## Decision 3 — Wire the input's auto-height back into the row list

`GuiElementTextArea.TextChanged()` re-heights the *input element's own bounds*, but the row list's
`rowHeights[]`, content height, and the rows below are computed once at `ComposeEditorView` time
from `ScribeRowElement.RowHeightFixed`. So the input growing does not, by itself, push rows down.

Flow on each text change (the input already calls `OnTextChanged` → `OnEditInputTextChanged`, which
writes the scratch doc):
1. After the scratch doc's text is updated, compute the focused row's new measured height via the
   same `ScribeRowElement.RowHeightFixed(...)` the compose path uses (single source of truth — do
   not measure a second way, or the label and input disagree).
2. If it differs from the current `rowHeights[focusedEditIndex]` (a wrap boundary was crossed),
   trigger `RecomposeEditorViewPreservingFocus()` — which recomputes all `rowYs`/`rowHeights`,
   shifts rows below, updates content height + scrollbar, and re-seeds the input at the focused row
   with the caret preserved (S2 built this exact path for focus moves).
3. Set the existing `scrollFocusedRowIntoView` one-shot before recompose so a row growing past the
   bottom edge scrolls into view (S2's one-shot; consumed in `ComposeEditorView`).

Only recompose on an actual height *change*, not every keystroke — typing within a line must not
thrash the whole list. The height-changed check is the gate.

**Caret preservation across recompose:** `RecomposeEditorViewPreservingFocus` already restores the
caret position (it was built so focus moves don't lose the caret). Re-measuring mid-word must keep
the caret where the player is typing — verified by playtest (a tasks item), since a recompose that
reset the caret to end-of-text would make typing mid-string impossible.

## Decision 4 — Keep label↔input wrap parity (the real risk)

The static label wraps via `GuiElementScribeRow`'s `AutobreakAndDrawMultilineTextAt(ctx, font,
text, x, y, boxWidth)` at `scaled(layout.TextWidth)`. The input wraps via TextArea's internal
line-breaking at `Bounds.InnerWidth`. If those two widths (or the two break algorithms) disagree,
the text reflows by a line at the focus/blur handoff — the exact "jump" S2 eliminated for the
single-line case.

Mitigation:
- Size the input's inner text width to equal `scaled(layout.TextWidth)` — i.e. account for
  TextArea's own left padding so the *text column* width matches what the label wrapped at. The
  input bounds are already placed at `layout.TextX` with `layout.TextWidth`; verify InnerWidth
  (bounds minus padding) equals the label's wrap width, and adjust the bounds for TextArea's
  padding if needed so the wrap column is identical.
- Both paths ultimately call into the same `TextDrawUtil`/`GetMultilineTextHeight` family with the
  same `CairoFont` (`RowFont()`), so if the wrap *width* is reconciled, the break points should
  match. This is the primary thing the manual playtest (tasks 4.x) must confirm at a couple of
  text sizes and window widths; if a residual off-by-one-word remains, reconcile by feeding the
  label's autobreak the input's effective wrap width (or vice versa) so both consume one number.

## Decision 5 — `ComposeTextElements` override: strip TextArea's box, keep its highlight

S2's override reproduced `GuiElementTextInput`'s highlight while dropping its emboss+dark-fill.
`GuiElementTextArea.ComposeTextElements` does `EmbossRoundRectangleElement(...)` + a `0.20` black
`ElementRoundRectangle` fill + `GenerateHighlight()` + `RecomposeText()`. We want only the
highlight (TextArea's `GenerateHighlight` builds a `0.1` white highlight shown while focused) and
the text, not the box. Override to skip the emboss + dark fill, call the highlight build, and rely
on the dialog's post-compose `SetValue` to trigger `RecomposeText` (as S2 documented) — or, since
TextArea exposes `RenderInteractiveElements` differently, confirm the highlight field/texture
names on TextArea match what the override references (`highlightTexture`, `highlightBounds`) and
adjust. This is a mechanical reconcile against the new base, not a behavior change.

## Decision 6 — Preserve the two clip corrections verbatim

S2's `RenderInteractiveElements` override (off-screen render skip + `PushScissor`/`PopScissor`
re-assert) guards against the base's `GlScissorFlag(false)` global scissor-disable and the base
clipping text to its own bounds. `GuiElementTextArea` renders through the same
`GuiElementEditableTextBase` path and ends the same way, so both corrections are still required and
carry over unchanged — but the height-skip check now compares against a *taller* input
(`Bounds.InnerHeight` grows), which is fine: a partially-visible growing row should still draw.
Re-verify the off-screen skip uses the live grown height.

## Out of scope

- Player-inserted hard newlines / true multi-paragraph blocks (would need a Core model change and
  a different commit gesture; Enter stays commit-and-advance).
- The read view — it already wraps correctly (this is an editor-input-only fix).
- Any pin/delete/drag affordance work (owned by `restore-row-affordance-columns` and the wiring
  change, sequenced after this).
