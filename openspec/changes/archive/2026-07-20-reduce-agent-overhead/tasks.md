## 1. Tooling overhead (no dependency — safe to start immediately)

- [x] 1.1 Commit the existing `.claude/settings.json` (currently untracked), which disables
      `salesforce-claude-support`, `salesforce-trust-foundations`, `swift-lsp`, and
      `google-workspace` for this project.
- [x] 1.2 Delete `.claude/commands/opsx/` (`propose.md`, `apply.md`, `explore.md`,
      `update.md`, `sync.md`, `archive.md`) — redundant with the already-installed
      `.claude/skills/openspec-{propose,apply-change,explore,update-change,sync-specs,
      archive-change}` skills.
- [x] 1.3 Confirm the scoped `openspec-*` skills still trigger correctly on natural-language
      requests (e.g. "propose a change", "archive this change") with the `opsx` commands
      removed — no regression in how this change itself would be proposed/archived.
- [x] 1.4 Commit the deletion and the settings file together (or as two small commits);
      note in the commit message that `swift-lsp` should also be disabled globally in
      `~/.claude/settings.json` (0 usage across all projects) — a follow-up for the user
      to action outside this repo, not part of this commit.

## 2. Code hygiene in `src/Mod/` (BLOCKED until `add-lectern-block`'s in-flight changes land)

Do not start this group while another terminal has uncommitted edits to
`src/Mod/ScribeModSystem.cs` or `src/Mod/GuiDialogScribeLectern.cs` (per design.md
decision 2) — check `git status` immediately before starting and again before each task.

- [x] 2.1 Confirm `src/Mod/ScribeModSystem.cs` and `src/Mod/GuiDialogScribeLectern.cs` have
      no uncommitted changes in this or any other worktree before proceeding.
      -> confirmed clean after this session's lectern bugfix work (add-lectern-block tasks
      8.10-8.18) was committed across 4 commits.
- [x] 2.2 In `ScribeModSystem.cs`, add a private `TryGetLectern(IWorldAccessor world, int x,
      int y, int z)` helper that builds the `BlockPos` and returns
      `world.BlockAccessor.GetBlockEntity<BlockEntityScribeLectern>(pos)` (nullable, no
      out-param — matches the existing call-site pattern-match style per design.md
      decision 3).
- [x] 2.3 Replace the lookup logic in `OnClientReceivedEditReply`, `OnServerReceivedEdit`,
      `OnServerReceivedReleaseLock`, and `OnServerReceivedRequestAccess` with calls to
      `TryGetLectern`, preserving each handler's existing delegation logic unchanged.
- [x] 2.4 `dotnet build src/Mod/Mod.csproj` — confirm it still builds clean.
      -> confirmed: 0 warnings, 0 errors.
- [x] 2.5 In `GuiDialogScribeLectern.cs`, examine whether `OnRowDragMouseUp` needs to exist
      separately from the dialog's `OnMouseUp` override (e.g. whether the row's own
      drag-handle element would otherwise also handle the mouse-up event). If it serves no
      purpose `OnMouseUp` doesn't already cover, remove it and inline
      `args.Handled = true` into `OnMouseUp`. If it does serve a real purpose, add the one
      sentence explaining why, per design.md decision 4.
      -> kept, with a doc comment explaining why: `OnRowDragMouseUp` is wired to
      `ScribeDragHandleElement.OnDragMouseUp`, which only fires when the release lands
      within THAT row's own drag-handle bounds (the element's own `IsPositionInside` check).
      The dialog-level `OnMouseUp` has no equivalent per-row hit-test -- it only tracks
      `draggedBlockIndex`/`hoverTargetIndex` state -- so folding this in would mean
      duplicating that per-row bounds check. `ScribeDragHandleElement` is a minimal custom
      element (base `GuiElementStaticText`), not a real button/switch widget, so unlike
      those it doesn't mark `Handled` on its own; without this callback a release over the
      drag handle would go unhandled and risk a click-through to world interaction.
- [x] 2.6 `dotnet build src/Mod/Mod.csproj` and `dotnet test` (Core.Tests) — confirm both
      still pass/build clean; manually retest drag-reorder in the running game to confirm
      no behavior change.
      -> build clean, Core.Tests 27/27 (unaffected, no Core changes). Manual in-game
      drag-reorder retest pending user confirmation after restage.
- [x] 2.7 Confirm `OnClickAddTask`'s placeholder-seeding (tasks.md 8.9 in
      `add-lectern-block`) remains untouched — out of scope for this change per design.md
      Non-Goals; do not make the `Core.AddTask` semantics change without separate sign-off.
      -> confirmed untouched; no `Core.AddTask` semantics change made.
