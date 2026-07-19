## 1. Pre-flight

`add-lectern-block`'s in-flight changes (playtesting bugfixes, tasks 8.10–8.18) and
`reduce-agent-overhead`'s code-hygiene tasks have both landed since this change was
proposed — no longer blocked. Still worth a fresh look since the files changed:

- [ ] 1.1 Confirm `src/Mod/GuiDialogScribeLectern.cs`, `src/Mod/ScribeBlockRowCell.cs`,
      and `src/Core/ScribeBlock.cs`/`ScribeDocument.cs`/`ScribeDocumentCodec.cs` have no
      uncommitted changes in this or any other worktree before proceeding.
- [ ] 1.2 Re-read the current state of all files above (they changed since this change
      was proposed) before making any edits, so task descriptions below are applied
      against current code, not a stale mental model.

## 2. Core: pin flag, reserved assignment field, codec version bump

- [ ] 2.1 Add `Pinned` (bool, default false) and `AssignedToUid` (nullable string,
      default null) to `ScribeBlock`. Both are per-block fields, not a side table (design.md
      decision 7). Update its constructor and any existing call sites that construct one
      positionally.
- [ ] 2.2 Add `ScribeDocument.TogglePinned(int index)`, mirroring `ToggleTask`'s shape and
      task-only restriction (fails on a text-section block or an out-of-range index,
      never throws). No mutation method for `AssignedToUid` yet — it stays reserved/unset
      until the future Desk work defines real semantics.
- [ ] 2.3 Bump `ScribeDocumentCodec.Version` and serialize/deserialize the two new fields.
      No backward-compat parse path for the old version (design.md decision 8, explicitly
      accepted) — a version mismatch continues to fail safe via the existing
      magic/version check, it just now also rejects the previous version's bytes.
- [ ] 2.4 Add `Core.Tests` coverage: `TogglePinned` (toggle on/off, fails on a text
      section, fails on an out-of-range index) and a codec round-trip test asserting
      `Pinned`/`AssignedToUid` survive serialize→deserialize (including a null
      `AssignedToUid`).
- [ ] 2.5 `dotnet test tests/Core.Tests/Core.Tests.csproj` — confirm all tests pass,
      including the new ones.

## 3. Scrollable/clipped content region (resolves `add-lectern-block` task 8.15)

- [ ] 3.1 Investigate `GuiComposer`'s clipped-region support, starting from
      `GuiDialogTrader`'s scrollbar usage (already referenced in
      `ScribeBlockRowCell`'s class doc comment) and `VSAPI-NOTES.md`. Decompile only if
      the wiki/shipped-mod-source don't answer it, per existing project discipline — add
      any new finding to `VSAPI-NOTES.md`.
- [ ] 3.2 Add a scrollable/clipped region wrapping the row list in both
      `ComposeReadView` and `ComposeEditorView`, replacing the current unclipped
      absolute-Y stacking.
- [ ] 3.3 Confirm drag-reorder (`OnMouseMove`/`OnMouseUp`/`HitTestRowIndex`) still works
      correctly when the row list is scrolled away from its top position — hit-testing
      must account for scroll offset.
- [ ] 3.4 Revisit `MaxTextSizePercent` (currently 150, capped as a stopgap for the missing
      scroll region) now that overflow is handled by scrolling — raise or remove the cap
      per design.md's note that the original constraint no longer applies. Confirm with
      the user before removing it outright if any doubt remains about other reasons for
      the cap.
- [ ] 3.5 Manually test: create a document with enough rows to overflow the visible
      dialog height; confirm every row is reachable by scrolling, in both read and editor
      view.

## 4. Portrait reshape

- [ ] 4.1 Pick concrete portrait `ElementBounds` dimensions (design.md leaves exact pixel
      dimensions as an open question) and update `DialogBounds()`.
- [ ] 4.2 Update `EditorListWidth` and the read view's `listWidth` to the new, narrower
      portrait column width.
- [ ] 4.3 Re-check `ScribeBlockRowCell.TextWidth`/`MeasureWrappedHeight` call sites for any
      layout assumptions tied to the old wide dimensions.
- [ ] 4.4 Manually test in-game: confirm the reshaped dialog reads well at the new
      dimensions, at the vanilla lectern's typical viewing distance, before treating the
      dimensions as final.

## 5. Custom-drawn backdrop

- [ ] 5.1 Decide placeholder backdrop implementation: a static image asset under
      `src/Mod/assets/scribe/textures/gui/` vs. a purely procedural Cairo fill+border draw
      (design.md leaves this open) — pick based on whichever is less code for a first cut.
- [ ] 5.2 Implement the backdrop, composited behind composer content the way the reference
      mod does it (Cairo `ImageSurface`/custom draw behind ordinary `GuiComposer`
      elements) — replacing `AddShadedDialogBG`.
- [ ] 5.3 Confirm the backdrop swap point (one asset path or one draw call) is isolated
      enough that a future real per-tier texture can replace it with no change to
      `GuiDialogScribeLectern.cs`'s layout/composition logic — this is the requirement
      from `specs/lectern-gui-shell/spec.md`'s "Backdrop is swappable" scenario; verify it
      concretely (e.g. swap in a second placeholder image and confirm nothing else needs
      to change) rather than assuming the architecture satisfies it.
- [ ] 5.4 Manually test in-game: confirm the backdrop renders correctly behind both read
      and editor view content, with no rows rendered behind/under an opaque part of the
      backdrop.

## 6. Row-list visual restyle

- [ ] 6.1 Fix checkbox scaling: pass `size: ToggleWidth * textSizeScale` (or equivalent)
      to `AddSwitch` in `ScribeBlockRowCell.Compose`, reusing the same `textSizeScale`
      factor `RowHeight` already applies (design.md decision 5). Manually test: drag the
      text-size slider across its range and confirm the checkbox grows/shrinks with the
      row text, not staying a fixed pixel size.
- [ ] 6.2 Investigate whether `GuiElementSwitch`'s existing rendering can read as
      "circular" via its own constructor/params, or needs a custom element (design.md's
      open question) — decide based on how it actually looks in-game at the new scaling.
- [ ] 6.3 Increase row spacing and add a subtle divider between rows (or between the row
      list and the toolbar), matching the Slack reference's generous, airy spacing rather
      than the current tight stacking.
- [ ] 6.4 Implement hover-conditional icon visibility (delete icon, and the new pin
      toggle from group 7) via a render-time mouse-position check inside each icon
      element's own rendering, not a recompose (design.md decision 6 — mirrors the
      vanilla title bar's own close/menu-icon hover-glow technique). Confirm no existing
      `AddIf`/recompose call sites are used for this.
- [ ] 6.5 Add a focus ring scoped to the actively-focused text field (task input or note
      text area), rather than the whole row, matching the Slack reference's blue focus
      box around only the field being edited. Investigate what focus-indication the base
      `GuiElementEditableTextBase`/composer already draws by default before adding a
      custom one — this may already exist and only need confirming, not building.
- [ ] 6.6 Manually test in-game: confirm hover-icon show/hide does not reset focus or
      caret position while typing (the exact regression this render-time approach is
      meant to avoid — verify it actually holds, don't just assume the mechanism works).

## 7. Pin toggle GUI affordance

- [ ] 7.1 Add a pin-toggle icon button to each task row in `ScribeBlockRowCell.Compose`,
      wired to a new `onTogglePin` callback (mirrors the existing `onToggle`/`onDelete`
      wiring shape). Hover-conditional per group 6.4. Text-section rows get no pin
      affordance (design.md decision 7 — pin is task-only, same as `Done`).
- [ ] 7.2 Wire the callback through `GuiDialogScribeLectern` to
      `scratchDocument.TogglePinned(index)`, following the same `isDirty = true` +
      autosave pattern as `OnRowToggle`.
- [ ] 7.3 Seed the pin toggle's visual on/off state from `block.Pinned` in
      `ScribeBlockRowCell.ApplyValues`, alongside the existing `Done`/text seeding.
- [ ] 7.4 Confirm `AssignedToUid` has zero GUI surface anywhere in this file — no column,
      no toggle, nothing composed or seeded (design.md non-goal; verify by grep, not
      assumption).
- [ ] 7.5 Manually test in-game: pin and unpin a task in editor view; switch to read view
      and back; confirm the pinned state persists across a save/reload (fully quit and
      relaunch, not just close/reopen the dialog).

## 8. Cleanup and verification

- [ ] 8.1 `dotnet build src/Mod/Mod.csproj` — confirm clean build.
- [ ] 8.2 `dotnet test tests/Core.Tests/Core.Tests.csproj` — confirm all pass, including
      group 2's new tests.
- [ ] 8.3 Full manual playtest pass: place a lectern, open read + editor view, add enough
      rows to require scrolling, drag-reorder while scrolled, resize via text-size slider,
      pin/unpin a task, confirm no regression versus `add-lectern-block`'s existing
      playtesting checklist (its tasks.md group 7) for anything not specific to the old
      wide layout or the old fixed checkbox size.
- [ ] 8.4 Confirm old-version saved documents fail to load safely (no crash, no partial/
      corrupt document) rather than silently misreading old bytes as new-format data —
      this is the accepted-breaking-change behavior from design.md decision 8, verify it
      actually fails safe rather than assuming the existing codec guarantee still holds
      after the version bump.
- [ ] 8.5 Update ROADMAP.md: mark the "skeuomorphic open-book UI... turn-page paging"
      parked bullet as done/superseded per this change (backdrop delivered; pagination
      explicitly rejected in favor of scrolling) rather than leaving it duplicated.
