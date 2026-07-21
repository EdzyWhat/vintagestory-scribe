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
- [x] 4.4 On Esc: **close the dialog** (do NOT revert in place). *(Decision reversed 2026-07-21
      after playtest 2026-07-21T13-03-17: the tester wants Esc to be a fast panic-close — "bears
      are a killer, we need to leave windows fast" — and reported the built revert-in-place felt
      wrong.)* Blur-commit already fires on close, so the focused row's pending edit is saved on
      the way out — Esc is a commit-and-close, not a discard. Remove the `onRevert` interception
      from `ScribeRowTextInput.OnKeyDown` so Esc bubbles to the base dialog close; drop `OnEditRevert`
      / `focusedEditOriginalText` revert plumbing. In-place revert is dropped (no key rebinding).

## 5. Unify width, remove seed, tolerate legacy config

- [x] 5.1 Collapse `ReadListWidth`/`EditorListWidth` in `ScribeClientConfig` into one shared
      width knob; update both compose paths and the debug sliders to the single field.
- [x] 5.2 Make config deserialization tolerant of the removed legacy keys (ignore unknown keys;
      fall back to the default width if the new key is absent) so existing on-disk configs load.
- [x] 5.3 Delete `SeedSampleContentIfEmpty` and its constructor call (both `TEMP SAMPLE SEED`
      sites).

## 6. Build, test, and playtest

- [x] 6.1 Build Debug and Release clean; run `dotnet test` (Core.Tests) — all green.
- [x] 6.2 Manually test in-game (Mac, `build/restage.sh`, full relaunch): Cmd+Arrow jumps to
      line ends, Alt/Option+Arrow skips by word, Shift extends selection during each — and none
      of these insert stray characters. *(Confirmed 2026-07-21T13-03-17.)*
- [x] 6.3 Manually test in-game: Enter commits + advances, Shift+Tab commits + retreats, Esc
      commits-and-closes the dialog (per the 2026-07-21 decision, NOT revert), clicking away
      commits; edits persist across a view switch and reload. *(Confirmed 2026-07-21T14-19-12.)*
- [x] 6.4 Manually test in-game: focusing/blurring a row shows no baseline/position/size jump
      between the static label and the floating input. *(Confirmed 2026-07-21T14-19-12.)*
- [ ] 6.5 Manually test in-game: editor rows clip (not pop) at the scroll boundary and scroll
      continuously; read and editor views are the same row-list width. *(Widths + continuous
      scroll confirmed 2026-07-21T14-19-12; CLIP is broken — chrome/rulings bleed past the top
      boundary and a new-task input renders below the box. See 6.10/6.11.)*
- [x] 6.6 Confirm no regression to the read view (checkbox toggle, ruling, clipping) after the
      shared-width and scroll-path changes. *(Confirmed 2026-07-21T14-19-12.)*

### Playtest follow-up fixes (from report 2026-07-21T13-03-17)

- [x] 6.7 **Re-click loses focus (fixed).** Clicking the already-focused editor row blurred its
      input and typing died (only a different-row click recovered it). Root cause (decompile-
      confirmed): the overlapping `ScribeRowElement` — added to the composer before the floating
      input — consumed the mouse-down, and `GuiComposer.OnMouseDown` then blurred the still-focused
      input. Fixed in `ScribeRowElement.OnMouseDownOnElement`: the focused row (`suppressText`)
      yields its text-column mouse-down to the input (no `args.Handled`), so the input keeps focus
      AND places the caret at the click. Recorded in VSAPI-NOTES.md. **Retest in-game (6.8).**
- [x] 6.8 Manually test in-game: click into an editor row, then click it AGAIN — the caret stays,
      you can keep typing, and the caret moves to where you clicked (click-to-place). Confirm a
      different-row click still moves focus + commits the prior row. *(Confirmed 2026-07-21T14-19-12.)*
- [x] 6.9 **Input border removed (fixed).** The floating input's baked emboss border read as the
      text "jumping" on focus (tester's side note). `ScribeRowTextInput.ComposeTextElements` now
      skips the emboss + dark fill, keeping only the subtle focused-highlight background. **Retest
      via 6.4** (the no-jump item this was failing). *(Confirmed via 6.4, 2026-07-21T14-19-12.)*

### Playtest follow-up fixes (from report 2026-07-21T14-19-12)

- [ ] 6.10 **BUG — content bleeds past the clip boundary.** Screenshots show (a) a newly-added
      task's floating input rendered near the bottom of the screen, well below the dialog box
      (2026-07-21T14-12-21-general.png), and (b) row rulings/chrome drawn *above* the top clip
      boundary, overlapping the title area, while scrolled (2026-07-21T14-17-01-general.png).
      Something in the editor row/input path is escaping the `BeginClip` scissor. Investigate:
      the floating `ScribeRowTextInput` is a `GuiElementTextInput` whose `RenderInteractiveElements`
      calls `GlScissor`/`GlScissorFlag(true)` then `GlScissorFlag(false)` — VSAPI-NOTES already
      flags that this flag clobber defeats the dialog's clip for a mixed list. That is the prime
      suspect for the input rendering unclipped below the box; confirm and fix (e.g. the input must
      not render when its row is outside the visible range, or the scissor must be restored).
- [ ] 6.11 **BUG — adding a task while scrolled/overflowing puts the new row off-screen.** With
      enough rows to overflow, Add Task appends at the bottom (out of the visible area) and the
      view does not scroll to it, so the new task (and its focused input) appears out of bounds
      rather than being scrolled into view. Fix: after Add Task, scroll the list so the newly
      focused row is visible (and within the clip region). Ties into 6.10 (the out-of-bounds input
      is only visible at all because of the clip bleed).
- [ ] 6.12 **DECISION/FEATURE — insert new task below the one being edited?** Tester asks: when a
      row's input is focused and you click Add Task, insert the new task directly *below the row
      being edited* rather than at the list bottom. Open question flagged by the tester: is that
      discoverable, or confusing? Needs a product decision before speccing (surfaced to the user).

## 7. Close out

- [ ] 7.1 Update `docs/session-notes/` handoff (or mark S2 complete) and delete the now-stale
      S1 handoff note if fully superseded.
- [ ] 7.2 Note in the change that the `row-list-rework` branch is now mergeable (seed gone,
      views unified) for the user's merge decision.
