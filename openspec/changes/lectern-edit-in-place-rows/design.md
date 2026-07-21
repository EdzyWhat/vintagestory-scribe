## Context

S1 landed the read view on a single custom-drawn `ScribeRowElement` (interactive-pass draw,
native clipping, shared `RowTextLayout`, lock-free checkbox). The editor view was left on the
pre-rework path: `ComposeEditorView` hand-stacks `GuiElementTextInput`/`GuiElementTextArea`
rows mixed with static chrome, and `OnRowListScroll` culls-and-recomposes (an editor branch)
instead of the read view's continuous `fixedY` shift. That path is the source of the
scroll-boundary "pop", the read/edit visual divergence, and the divergent `ReadListWidth` vs
`EditorListWidth`.

S2 finishes the rework: editor rows become `ScribeRowElement` in `ScribeRowMode.Edit`, with a
single floating input for the row being edited. The spike (in `VSAPI-NOTES.md`) established
that the engine's editable-text base already provides selection, word-skip, clipboard, and
line-jump — but is Windows-keyed, so a thin subclass supplies the Mac caret routing.

Constraints unchanged: `src/Core/` never references the VS API (all this is `src/Mod/`); no new
mod dependencies; edits stay server-authoritative through the existing lock-gated
`ScribeEditDocumentMessage` → `ApplyEdit`; persistence follows the Sign pattern.

## Goals / Non-Goals

**Goals:**
- Editor rows render as `ScribeRowElement` in the interactive pass, clipped natively, scrolled
  by the same continuous `fixedY` shift the read view uses.
- Exactly one live `GuiElementTextInput` at a time, repositioned onto the focused row, aligned
  to the static label via `RowTextLayout` so focus handoff is visually seamless.
- Mac caret conventions (Cmd+Arrow line-ends, Alt/Option+Arrow word-skip, Shift-extend-select)
  and row navigation (Enter advance, Shift+Tab retreat, Esc commits-and-closes the dialog, blur
  commit). *(Esc was originally specced as revert-in-place; reversed to close-the-dialog after
  the 2026-07-21 playtest — see tasks.md 4.4.)*
- One unified row-list width across both views; the temp sample seed removed; the old editor
  scroll path deleted.

**Non-Goals:**
- Drag-reorder lift-ghost / insertion feedback — that is S3 (`lectern-drag-reorder-feedback`).
- Checkbox stamp/erase animation — S4 (fills the existing `// S4 HOOK`).
- Any change to the read-view lock-free toggle or to `src/Core/` mutation rules.
- Multi-line rich editing beyond what the note/task model already stores.

## Decisions

### 1. One floating input, static labels elsewhere (edit-in-place)
Draw every editor row's text as a static label via `RowTextLayout`; keep a single real
`GuiElementTextInput` that is repositioned onto the focused row. The focused row **suppresses
its own text label for that frame** (draws checkbox + ruling, skips text pixels) so text is
never double-drawn. `block.Text` is untouched — "suppress" means skip painting, not clear data.

- **Why one input:** `GuiElementTextInput.RenderInteractiveElements` calls
  `GlScissorFlag(false)`, which clobbers the clip stack for anything drawn after it that frame
  (VSAPI-NOTES). One live input at a time contains that blast radius to the single focused row,
  and the row's own draw already sits inside `BeginClip`.
- **Alternative rejected — one input per row:** re-introduces the multi-input scissor clobber
  and the exact mixed static+interactive bleed S1 was built to kill.
- **Alternative rejected — TextArea for tasks:** tasks are single-line by design; `TextInput`
  matches. Notes may still use the wrapping path, but the floating-input model is uniform.

### 2. Caret conventions via a `GuiElementTextInput` subclass, not a rewrite
Add `ScribeRowTextInput : GuiElementTextInput` overriding `OnKeyDown`. Before delegating to
`base.OnKeyDown` (which early-returns on `AltPressed` and ignores `CommandPressed` for
navigation), the override:
- On `CommandPressed` + Left/Right → call the inherited Home/End caret logic (line ends).
- On `AltPressed` + Left/Right → call the inherited `MoveCursor(dir, wholeWord: true)`.
- Preserve `ShiftPressed` semantics so selection extends (the base already extends selection on
  Shift; we just must not let it swallow the Alt/Cmd combos first).
- Intercept Enter / Shift+Tab for row navigation (below); let Esc fall through to the base so it
  closes the dialog (2026-07-21 decision), else defer to base.

- **Why subclass:** the spike confirmed all the hard machinery (selection model, `MoveCursor`
  word scan, clipboard, Home/End) already exists in `GuiElementEditableTextBase`; only the
  modifier routing is Windows-keyed. Subclassing reuses it; reimplementing a caret/selection
  engine would be large and bug-prone.
- **Alternative rejected — patch at the dialog `OnKeyDown`:** the dialog can't cleanly reach
  the element's protected caret state; the override is the right seam.

### 3. Row navigation and commit live at the dialog/focus level
Enter and Shift+Tab are cross-element (they move focus between rows), so the subclass surfaces
them to the dialog, which commits the current row (through `ScribeEditDocumentMessage`) and
moves focus to the sibling `ScribeRowElement`'s input position. Esc closes the dialog (the base
`GuiDialog` Esc-close; the subclass no longer intercepts it) — blur-commit fires on the way out,
so it's a commit-and-close, not a discard. *(Originally specced as revert-in-place; reversed
2026-07-21 per playtest — tasks.md 4.4.)* Losing focus (blur) commits — matching the existing
edit path so no edit is silently dropped.

- **Why keep commits on the existing lock-gated path:** the editor already holds the
  single-editor lock; `ApplyEdit` is server-authoritative and synced. S2 changes *where the
  text comes from* (a floating input), not *how it persists*. No new network message.

### 4. Unify list width and delete the old scroll path
Collapse `ReadListWidth`/`EditorListWidth` into one width knob so both views compose at the
same width. Delete the editor branch in `OnRowListScroll` (and the cull/recompose/drag-handoff
machinery it drove) so both views run the single native-clip `fixedY` path. Remove
`SeedSampleContentIfEmpty` and its ctor call.

- **Why now:** unification and seed-removal are exactly what makes the `row-list-rework` branch
  mergeable to `main`; leaving either in place perpetuates the two reasons the branch is held.
- **Migration note:** the config-field collapse is a local rename; existing
  `scribe-client-config.json` files with the old keys should fall back to the default width
  rather than error (keep the JSON tolerant of the removed keys).

## Risks / Trade-offs

- **[The Alt early-return may sit above where our override can intercept]** → The subclass
  overrides `OnKeyDown` (the public entry the engine calls) and acts *before* `base.OnKeyDown`,
  so the Alt combo is handled and marked `args.Handled` before the base's early-return can eat
  it. Verify by playtest that Option+Arrow moves by word and does not also insert a character.
- **[Focus handoff jump if label and input diverge]** → Both read from the same `RowTextLayout`
  (x-offset, baseline, font). Any future change to row metrics must stay funneled through that
  struct; a playtest check on focus/blur watches for a baseline jump.
- **[Scissor clobber if a second input ever goes live]** → Enforce single-live-input invariant:
  repositioning is a move, not an add; only the focused row hosts the input. Assert/guard that
  no more than one input element exists in the composed tree.
- **[Commit-on-blur races the autosave / lock]** → Commits ride the existing lock-gated path
  unchanged, so S2 introduces no new concurrency surface beyond what the editor already had. The
  read-view lock-free toggle (S1) is untouched.
- **[Config key removal breaks an existing on-disk config]** → Tolerant deserialization: unknown
  legacy keys are ignored, missing new key falls back to default. No hard failure on load.

## Migration Plan

1. Land the `ScribeRowTextInput` subclass with the caret overrides (unit-reasoned, playtested).
2. Rewrite `ComposeEditorView` onto `ScribeRowElement` (Edit mode) + single floating input;
   remove the `OnRowListScroll` editor branch.
3. Unify the width config field (tolerant of the old keys); delete the sample seed.
4. Mac-playtest (`build/restage.sh`, full relaunch): caret conventions, row nav, seamless
   handoff, unified width, boundary clipping, no regression to the read view.
5. Once verified, the `row-list-rework` branch is mergeable to `main` (seed gone, views unified).

Rollback: revert the S2 commits; the read view and S1 archive are independent and stay intact.

## Open Questions

- Should Tab (without Shift) do anything, or only Shift+Tab retreat + Enter advance? Current
  plan: Tab unbound in-field (retreat is Shift+Tab, advance is Enter) — confirm in playtest.
- Notes (multi-line) vs tasks (single-line) in the floating-input model: does a note need the
  wrapping `GuiElementTextArea` variant, or does a single-line input suffice for v1 note edit?
  Resolve during implementation against the current note-editing behavior.
