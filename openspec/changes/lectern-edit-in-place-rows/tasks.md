## 1. Caret-convention text input subclass

- [x] 1.1 Add `ScribeRowTextInput : GuiElementTextInput` in `src/Mod/`, overriding `OnKeyDown`.
- [x] 1.2 Route `CommandPressed` + Left/Right onto the inherited line-start/line-end caret logic
      (the same the base runs for Ctrl+Home/End), marking `args.Handled` before `base.OnKeyDown`.
      (Implemented by rewriting Cmd+Left/Right to Home/End before delegating to the base.)
- [x] 1.3 Route `AltPressed` + Left/Right onto the inherited word-skip (`MoveCursor(dir,
      wholeWord: true)`), acting before the base's `AltPressed` early-return can swallow it.
      (Implemented by clearing Alt + setting Ctrl on the arrow event before delegating.)
- [x] 1.4 Ensure `ShiftPressed` combined with any of the above extends selection (inherited
      behavior preserved — the override must not consume the key in a way that drops selection).
      (Shift is copied onto every rewritten event; the base's shift-extend runs unchanged.)
- [x] 1.5 Surface Enter / Shift+Tab / Esc from the subclass to the dialog (callbacks or handled
      flags) for row navigation and revert, deferring all other keys to `base.OnKeyDown`.

## 2. Editor rows on ScribeRowElement (edit mode)

- [x] 2.1 Wire `ScribeRowMode.Edit` in `ScribeRowElement`: draw each row's text as a static
      label via `RowTextLayout` (checkbox + ruling drawn as in read mode).
- [x] 2.2 Add per-frame text-label suppression on the focused row (draw checkbox/ruling, skip
      the text pixels) so the floating input and label never both paint; `block.Text` untouched.
- [x] 2.3 Use the shared `RowHeightFixed`/`RowTextLayout` for edit-mode row height so read and
      edit rows measure identically (no separate editor measure path).

## 3. Editor view composition and single floating input

- [x] 3.1 Rewrite `ComposeEditorView` to add `ScribeRowElement`s (Edit mode) inside `BeginClip`,
      mirroring `ComposeReadView`'s clip/scroll idiom.
- [x] 3.2 Add exactly one `ScribeRowTextInput` and reposition it onto the focused row (aligned to
      the static label via `RowTextLayout`); enforce the single-live-input invariant.
- [x] 3.3 On row focus change, move the input to the new row, resume the previous row's static
      label, and preserve caret/focus per the VSAPI-NOTES recompose-focus pattern.
- [x] 3.4 Delete the editor branch in `OnRowListScroll` so both views use the continuous
      `fixedY` native-clip shift; remove the now-dead cull/recompose/drag-handoff editor code.

## 4. Commit, navigation, and revert

- [x] 4.1 On Enter: commit the focused row's text via the existing lock-gated
      `ScribeEditDocumentMessage` → `ApplyEdit`, then move focus to the next row.
- [x] 4.2 On Shift+Tab: commit the focused row, then move focus to the previous row.
- [x] 4.3 On blur (focus lost without Enter/Shift+Tab): commit the focused row's text.
- [x] 4.4 On Esc: revert the focused row to its stored `block.Text` without committing.

## 5. Unify width, remove seed, tolerate legacy config

- [x] 5.1 Collapse `ReadListWidth`/`EditorListWidth` in `ScribeClientConfig` into one shared
      width knob; update both compose paths and the debug sliders to the single field.
- [x] 5.2 Make config deserialization tolerant of the removed legacy keys (ignore unknown keys;
      fall back to the default width if the new key is absent) so existing on-disk configs load.
- [x] 5.3 Delete `SeedSampleContentIfEmpty` and its constructor call (both `TEMP SAMPLE SEED`
      sites).

## 6. Build, test, and playtest

- [x] 6.1 Build Debug and Release clean; run `dotnet test` (Core.Tests) — all green.
- [ ] 6.2 Manually test in-game (Mac, `build/restage.sh`, full relaunch): Cmd+Arrow jumps to
      line ends, Alt/Option+Arrow skips by word, Shift extends selection during each — and none
      of these insert stray characters.
- [ ] 6.3 Manually test in-game: Enter commits + advances, Shift+Tab commits + retreats, Esc
      reverts, clicking away commits; edits persist across a view switch and reload.
- [ ] 6.4 Manually test in-game: focusing/blurring a row shows no baseline/position/size jump
      between the static label and the floating input.
- [ ] 6.5 Manually test in-game: editor rows clip (not pop) at the scroll boundary and scroll
      continuously; read and editor views are the same row-list width.
- [ ] 6.6 Confirm no regression to the read view (checkbox toggle, ruling, clipping) after the
      shared-width and scroll-path changes.

## 7. Close out

- [ ] 7.1 Update `docs/session-notes/` handoff (or mark S2 complete) and delete the now-stale
      S1 handoff note if fully superseded.
- [ ] 7.2 Note in the change that the `row-list-rework` branch is now mergeable (seed gone,
      views unified) for the user's merge decision.
