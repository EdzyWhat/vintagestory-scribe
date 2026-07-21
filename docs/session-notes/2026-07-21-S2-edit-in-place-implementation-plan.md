# S2 (lectern-edit-in-place-rows) — implementation plan & progress

Working record for implementing Groups 2–5 of `openspec/changes/lectern-edit-in-place-rows`.
Delete when S2 is committed and playtested. Branch `row-list-rework`, base `b360b86`.
Group 1 (ScribeRowTextInput caret subclass) is DONE + committed (`c7035f9`).
Groups 6 (in-game playtest) and 7 (closeout) are RESERVED for the human — do NOT do them.

## Confirmed design decisions (locked, faithful to tasks.md + design.md + RowTextLayout intent)

1. **Editor rows become `ScribeRowElement` in `ScribeRowMode.Edit`, drawn exactly like read
   rows: checkbox (task) + wrapped text label + ruling.** They use the SAME `RowHeightFixed`
   and `RowTextLayout` as read view (task 2.1/2.3). `RowTextLayout`'s own doc comment says it
   deliberately EXCLUDES the drag-handle/pin/delete gutters "so the read and editor views read
   as the same list." => **The per-row delete / pin / drag-reorder-handle affordances are
   intentionally removed from the editor in S2.** This is a spec-intended consequence, not an
   oversight. Reorder returns in S3 (`lectern-drag-reorder-feedback`, builds on the new rows);
   delete/pin are deferred (flag for the human in Group 7 — losing in-GUI delete is a real
   functional gap to acknowledge). "Add Task" toolbar button STAYS.

2. **One shared floating `ScribeRowTextInput` (single-line) for BOTH tasks and notes.** Design
   Open Q2 resolved to the uniform single-input model. A note's DISPLAY still wraps (static
   label via `AutobreakAndDrawMultilineTextAt`), but EDITING a note happens in the single-line
   input (horizontal scroll, no newline — Enter is row-nav). Acceptable v1; flag for playtest.
   => `composedNoteRowHeights` and `OnRowTextChanged`'s wrap-recompose branch are DELETED.

3. **Native-clip scroll for BOTH views (task 3.4).** After S2, `OnRowListScroll` for BOTH views
   is just `rowListContentBounds.fixedY = -value; CalcWorldBounds()`. No recompose-on-scroll
   ever. => DEAD after this: the editor branch of `OnRowListScroll`, `RowListCullBuffer`,
   `rowListComposedRangeTop/Bottom`, `rowListComposedFirstIndex/LastIndex`, `rowListDragHandoff`
   (+ its capture in OnRowListScroll, restore in SetupRowListScrollbar, clear in OnMouseUp),
   and the row-reorder handlers (`draggedBlockIndex`/`hoverTargetIndex`/`OnRowDragMouseDown`/
   `OnRowDragMouseUp`/`HitTestRowIndex`/OnMouseMove+OnMouseUp reorder blocks) — unreachable once
   the drag handle is gone. Remove them. Keep `isComposingRowList` (still suppresses the
   spurious SetHeights→OnRowListScroll(0)). Keep `textSizePendingRecompose` path in OnMouseUp.

4. **Focus model (tasks 3.2/3.3, 4.x).** Dialog holds `int? focusedEditIndex` and
   `string focusedEditOriginalText` (captured at focus, for Esc revert).
   - ComposeEditorView adds ALL rows as ScribeRowElement(Edit) at ABSOLUTE contentY inside
     BeginClip (mirror ComposeReadView). Row i passes `suppressText: i == focusedEditIndex`.
   - If focusedEditIndex in range, add exactly ONE `ScribeRowTextInput` as a child of
     contentBounds at the focused row's text column (x=layout.TextX, y=rowY, w=layout.TextWidth,
     h=rowHeight). Seed value + FocusElement + caret-to-end AFTER `.Compose()` (VSAPI-NOTES:
     never SetValue before Compose).
   - ScribeRowElement(Edit).OnMouseUpOnElement: checkbox-hit => onToggleClicked(i) (edit toggle
     mutates scratch+dirty); else => onRequestEdit(i).
   - onRequestEdit(i): commit current focused row (FlushIfDirty), set focusedEditIndex=i, capture
     original text, RequestRecompose (deferred — mid-dispatch hazard), focus happens in compose.
   - Enter => onCommitAndAdvance: FlushIfDirty, focusedEditIndex = min(i+1, count-1), recompose.
   - Shift+Tab => onCommitAndRetreat: FlushIfDirty, focusedEditIndex = max(i-1, 0), recompose.
   - Esc => onRevert: scratch.SetBlockText(i, originalText); input.SetValue(originalText);
     isDirty=true (push the revert); stay focused; no recompose needed.
   - Blur => new `onBlur` hook on ScribeRowTextInput (override OnFocusLost) => FlushIfDirty.
     Guarded by an `isTearingDown`/recompose flag so a recompose-driven blur doesn't double-fire.
     Autosave (1s) + close-flush already back-stop this, so blur-commit is belt-and-suspenders.

5. **Input rendering caveat (KNOWN, for playtest):** the base `GuiElementTextInput` bakes its
   border/highlight in the STATIC pass (ComposeTextElements) — static content does NOT scroll and
   is NOT clipped. So a focused row scrolled to straddle the viewport edge may bleed its input
   border. Normal case (focused row in view) is fine. Recompose on focus change re-bakes at the
   right spot. Chose default rendering over a borderless override to minimize GL crash risk
   (cannot playtest GL on this Mac). Flag for the human; a borderless ComposeTextElements override
   is the follow-up if the border reads badly.

## Group 5 (config + seed) — do FIRST (lowest risk)
- 5.1 Collapse `ReadListWidth`(300)/`EditorListWidth`(500) => single `RowListWidth`. Pick 500
  (editor needed the room; read text just gets wider — fine). Update ComposeReadView,
  ComposeEditorView, OnRowTextChanged (deleted anyway), and the TWO debug sliders => ONE.
- 5.2 Newtonsoft (LoadModConfig) ignores unknown JSON keys by default and leaves absent keys at
  their C# default initializer — so removing the legacy fields is already load-tolerant. Add a
  brief comment on the new field noting this. Verify LoadModConfig has no MissingMemberHandling.Error.
- 5.3 Delete `SeedSampleContentIfEmpty` + its ctor call + the `TEMP SAMPLE SEED` comment block.

## Files to touch (all src/Mod/, NO Core)
- ScribeClientConfig.cs (5.1/5.2)
- GuiDialogScribeLectern.cs (bulk: 2.x compose, 3.x, 4.x, 5.1, 5.3)
- ScribeRowElement.cs (Edit mode: suppressText, onRequestEdit, edit-mode mouse handling)
- ScribeRowTextInput.cs (add onBlur hook; already has advance/retreat/revert)

## Build/verify gate (task 6.1 — ALLOWED, it's a build gate not a playtest)
`dotnet build src/Mod/Mod.csproj -c Debug` AND `-c Release`, then
`dotnet test tests/Core.Tests/Core.Tests.csproj`. Game DLLs present at /Applications/Vintage Story.app.

## Commit
Only the files changed for this task. Do NOT commit untracked `docs/specs/glyph_forge_feasibility.md`.
(This plan file IS ok to commit — it's a session note.) End message with the Co-Authored-By line.

## PROGRESS — Groups 2–5 + 6.1 DONE (2026-07-21)
- [x] Group 5 (config/seed): RowListWidth=500 replaces ReadListWidth/EditorListWidth; seed gone.
- [x] Group 2 (ScribeRowElement Edit mode): suppressText + onRequestEdit + edit-mode checkbox/text hit.
- [x] Group 3 (compose + single input + scroll unify): ComposeEditorView rewritten; OnRowListScroll
      now one native-clip path for both views; cull/drag-handoff/reorder code removed.
- [x] Group 4 (commit/nav/revert): Enter/Shift+Tab/Esc/blur wired via ScribeRowTextInput callbacks.
- [x] Build Debug + Release clean; Core.Tests 35/35 pass.
- [x] tasks.md checkboxes 2.1–5.3 + 6.1 flipped.
- [ ] Commit (in progress)

## SCOPE DECISIONS MADE (not user-approved — flag for Group 7 review)
- **Editor lost its per-row delete / pin / drag-reorder-handle affordances.** Moving the editor
  onto ScribeRowElement (which by RowTextLayout's design has NO icon gutters, to match read view)
  means those go away. Reorder is explicitly S3. Delete + pin have NO replacement right now — this
  is a real functional regression the human must weigh (can still Add Task; can toggle done + edit
  text). If unacceptable, the fix is a follow-up (e.g. a read-view-style hit-zone on the row for
  delete, or a toolbar action) — did NOT invent one unprompted.
- **Notes edit single-line.** Note DISPLAY still wraps; note EDITING uses the single-line input
  (design Open Q2 → uniform single-input). Multi-line note editing deferred.
- **Input border may bleed if the focused row straddles the clip edge** (base input bakes border in
  the static/unclipped pass). Normal in-view case fine. Flagged; borderless override is the fallback.
- **ScribeBlockRowCell.Compose/ApplyValues + ScribeDragHandleElement + ScribeHoverIconButton are now
  DEAD CODE** (nothing calls them). Left in place: S3 (drag-reorder) is expected to reuse that
  pattern, and deleting is out of this change's scope. Compiles clean, no warnings.
</content>
</invoke>
