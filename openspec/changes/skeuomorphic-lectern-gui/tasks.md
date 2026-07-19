## 1. Pre-flight (BLOCKED until `add-lectern-block`'s in-flight changes land)

Do not start this change while another terminal has uncommitted edits to
`src/Mod/GuiDialogScribeLectern.cs` or `src/Mod/ScribeBlockRowCell.cs` — check `git status`
immediately before starting and again before each task group.

- [ ] 1.1 Confirm `src/Mod/GuiDialogScribeLectern.cs` and `src/Mod/ScribeBlockRowCell.cs`
      have no uncommitted changes in this or any other worktree before proceeding.
- [ ] 1.2 Re-read the current state of both files (they will have changed since this
      change was proposed) before making any edits, so task descriptions below are applied
      against current code, not a stale mental model.

## 2. Scrollable/clipped content region (resolves `add-lectern-block` task 8.15)

- [ ] 2.1 Investigate `GuiComposer`'s clipped-region support, starting from
      `GuiDialogTrader`'s scrollbar usage (already referenced in
      `ScribeBlockRowCell`'s class doc comment) and `VSAPI-NOTES.md`. Decompile only if
      the wiki/shipped-mod-source don't answer it, per existing project discipline — add
      any new finding to `VSAPI-NOTES.md`.
- [ ] 2.2 Add a scrollable/clipped region wrapping the row list in both
      `ComposeReadView` and `ComposeEditorView`, replacing the current unclipped
      absolute-Y stacking.
- [ ] 2.3 Confirm drag-reorder (`OnMouseMove`/`OnMouseUp`/`HitTestRowIndex`) still works
      correctly when the row list is scrolled away from its top position — hit-testing
      must account for scroll offset.
- [ ] 2.4 Revisit `MaxTextSizePercent` (currently 150, capped as a stopgap for the missing
      scroll region) now that overflow is handled by scrolling — raise or remove the cap
      per design.md's note that the original constraint no longer applies. Confirm with
      the user before removing it outright if any doubt remains about other reasons for
      the cap.
- [ ] 2.5 Manually test: create a document with enough rows to overflow the visible
      dialog height; confirm every row is reachable by scrolling, in both read and editor
      view.

## 3. Portrait reshape

- [ ] 3.1 Pick concrete portrait `ElementBounds` dimensions (design.md leaves exact pixel
      dimensions as an open question) and update `DialogBounds()`.
- [ ] 3.2 Update `EditorListWidth` and the read view's `listWidth` to the new, narrower
      portrait column width.
- [ ] 3.3 Re-check `ScribeBlockRowCell.TextWidth`/`MeasureWrappedHeight` call sites for any
      layout assumptions tied to the old wide dimensions.
- [ ] 3.4 Manually test in-game: confirm the reshaped dialog reads well at the new
      dimensions, at the vanilla lectern's typical viewing distance, before treating the
      dimensions as final.

## 4. Custom-drawn backdrop

- [ ] 4.1 Decide placeholder backdrop implementation: a static image asset under
      `src/Mod/assets/scribe/textures/gui/` vs. a purely procedural Cairo fill+border draw
      (design.md leaves this open) — pick based on whichever is less code for a first cut.
- [ ] 4.2 Implement the backdrop, composited behind composer content the way the reference
      mod does it (Cairo `ImageSurface`/custom draw behind ordinary `GuiComposer`
      elements) — replacing `AddShadedDialogBG`.
- [ ] 4.3 Confirm the backdrop swap point (one asset path or one draw call) is isolated
      enough that a future real per-tier texture can replace it with no change to
      `GuiDialogScribeLectern.cs`'s layout/composition logic — this is the requirement
      from `specs/lectern-gui-shell/spec.md`'s "Backdrop is swappable" scenario; verify it
      concretely (e.g. swap in a second placeholder image and confirm nothing else needs
      to change) rather than assuming the architecture satisfies it.
- [ ] 4.4 Manually test in-game: confirm the backdrop renders correctly behind both read
      and editor view content, with no rows rendered behind/under an opaque part of the
      backdrop.

## 5. Cleanup and verification

- [ ] 5.1 `dotnet build src/Mod/Mod.csproj` — confirm clean build.
- [ ] 5.2 `dotnet test` (Core.Tests) — confirm unaffected (this change touches
      presentation only, no `Core` changes expected).
- [ ] 5.3 Full manual playtest pass: place a lectern, open read + editor view, add enough
      rows to require scrolling, drag-reorder while scrolled, resize via text-size slider,
      confirm no regression versus `add-lectern-block`'s existing playtesting checklist
      (tasks.md group 7) for anything not specific to the old wide layout.
- [ ] 5.4 Update ROADMAP.md: mark the "skeuomorphic open-book UI... turn-page paging"
      parked bullet as done/superseded per this change (backdrop delivered; pagination
      explicitly rejected in favor of scrolling) rather than leaving it duplicated.
