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

- [x] 6.10 **BUG — content bleeds past the clip boundary (fixed).** Screenshots showed a
      newly-added task's floating input rendered near the bottom of the screen, well below the box,
      and row rulings/chrome drawn above the top clip boundary while scrolled. **Root cause
      (decompile-confirmed via `VintagestoryLib.dll`):** `GuiElementTextInput.RenderInteractiveElements`
      ends with `GlScissorFlag(false)`, which is a GLOBAL `GL.Disable(GL_SCISSOR_TEST)` in
      `ClientPlatformWindows` — it does NOT restore the enclosing `BeginClip` scissor on the render
      API's `ScissorStack`, so everything drawn after the input renders unclipped. **Fixes (three,
      after playtest 2026-07-21T20-58-36 showed two residual bleeds):**
      (a) `ScribeRowTextInput.RenderInteractiveElements` calls `base` then
      `PushScissor(InsideClipBounds)`/`PopScissor()` to re-assert the dialog's clip for later
      elements.
      (b) That same override now also SKIPS drawing the input when its row is scrolled fully
      outside the clip window — the base input clips its own text to its own bounds (not the dialog
      window), so a focused input on an off-screen row would otherwise paint unclipped below the
      box; the render skip is focus-safe (input stays typable, reappears when scrolled back).
      (c) Removed `AddRowDivider` entirely (method, call site, `RowDivider*` config + debug sliders,
      and the stale `configlib-patches.json` entries): the `AddInset` dividers drew in the
      always-unclipped static pass (so they bled into the controls area) AND were redundant with
      `ScribeRowElement`'s own baked lined-paper ruling. Recorded in VSAPI-NOTES.md. **Retest via
      6.13.**
- [x] 6.11 **BUG — adding a task while scrolled/overflowing puts the new row off-screen (fixed).**
      Add Task (and Enter/Shift+Tab navigation) could move focus to a row outside the visible
      window with no scroll-to. **Fix:** a one-shot `scrollFocusedRowIntoView` flag, set by
      `OnClickAddTask` and `MoveEditFocusTo` and consumed in `ComposeEditorView`, scrolls the
      focused row fully into view before clamping. One-shot so an ordinary click-to-focus or a live
      resync never overrides the user's own scroll. **Retest via 6.13.**
- [x] 6.13 Manually test in-game (retest of 6.5's clip half + 6.10/6.11): with a list long enough
      to overflow, scroll around and confirm NO ruling/chrome/text-input renders outside the box
      (not above the title, not below over the buttons, not down the screen). Then click Add Task
      with the list scrolled/overflowing and confirm the new empty task scrolls into view inside the
      box. Also Enter/Shift+Tab to a row near the top/bottom edge and confirm it scrolls into view.
      *(Confirmed 2026-07-21T21-37-15 — all three sub-checks pass; 6.5 clip half + 6.10 + 6.11 all
      now confirmed.)*
- [x] 6.14 **POLISH — add margin between the checkbox and the text (fixed).** Playtest
      2026-07-21T21-37-15 (+ screenshot 2026-07-21T21-37-08-general.png): the row text/input sat
      flush against the checkbox. **Fix:** added a `CheckboxTextGap` config knob (default 8,
      text-size-scaled) folded into the shared `RowTextLayout.TextX`, so BOTH the static label
      (`ScribeRowElement`) and the floating edit input pick up the gap in lockstep (design.md
      Decision 5); tasks only (notes have no checkbox). **Retest via 6.15.**
- [ ] 6.15 Manually test in-game: confirm a comfortable gap between the checkbox and the text/input
      on task rows, holding for both the static label and the focused input, at a couple of text
      sizes (the gap should scale with text size, not stay a fixed pixel width).
- [ ] 6.12 **FEATURE — Ctrl+Enter commits and inserts a new task below the current row.** *(Decided
      2026-07-21.)* The tester asked for "add a task below the one I'm editing." The convention
      across task/PM tools (Todoist, Things, Apple Reminders, Notion, Workflowy, any outliner) is
      consistent: **Enter/a keyboard gesture with the cursor in a row inserts the new item directly
      below the current one**, while the **"+" / Add button appends at the bottom**. Overloading the
      Add Task *button* with focus-dependent placement (bottom when unfocused, below-current when
      focused) is the genuinely confusing option — same button, two outcomes from invisible state —
      so we DON'T do that. Instead:
      - **Ctrl+Enter (row focused):** commit the current row, insert a new empty task *directly
        below it*, and move focus into the new task. (Plain Enter keeps its S2 behavior:
        commit + advance to the next existing row.)
      - **Add Task button:** unchanged — always appends a new task at the bottom.
      - Both paths must scroll the newly focused row into view (depends on 6.11).
      Sequence AFTER 6.10/6.11 — insert-below-then-scroll-into-view can't be validated until the
      clip + add-task-scroll behavior is correct. Matches the ROADMAP "UX lessons" note that already
      earmarked Ctrl+Enter commit-and-add-below as a follow-up batch. When picked up, this becomes
      its own small `openspec-propose` (behavior-adding beyond S2's scope).

## 7. Close out

- [ ] 7.1 Update `docs/session-notes/` handoff (or mark S2 complete) and delete the now-stale
      S1 handoff note if fully superseded.
- [ ] 7.2 Note in the change that the `row-list-rework` branch is now mergeable (seed gone,
      views unified) for the user's merge decision.
