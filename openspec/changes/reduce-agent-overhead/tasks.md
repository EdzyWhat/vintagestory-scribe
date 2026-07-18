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

- [ ] 2.1 Confirm `src/Mod/ScribeModSystem.cs` and `src/Mod/GuiDialogScribeLectern.cs` have
      no uncommitted changes in this or any other worktree before proceeding.
- [ ] 2.2 In `ScribeModSystem.cs`, add a private `TryGetLectern(IWorldAccessor world, int x,
      int y, int z)` helper that builds the `BlockPos` and returns
      `world.BlockAccessor.GetBlockEntity<BlockEntityScribeLectern>(pos)` (nullable, no
      out-param — matches the existing call-site pattern-match style per design.md
      decision 3).
- [ ] 2.3 Replace the lookup logic in `OnClientReceivedEditReply`, `OnServerReceivedEdit`,
      `OnServerReceivedReleaseLock`, and `OnServerReceivedRequestAccess` with calls to
      `TryGetLectern`, preserving each handler's existing delegation logic unchanged.
- [ ] 2.4 `dotnet build src/Mod/Mod.csproj` — confirm it still builds clean.
- [ ] 2.5 In `GuiDialogScribeLectern.cs`, examine whether `OnRowDragMouseUp` needs to exist
      separately from the dialog's `OnMouseUp` override (e.g. whether the row's own
      drag-handle element would otherwise also handle the mouse-up event). If it serves no
      purpose `OnMouseUp` doesn't already cover, remove it and inline
      `args.Handled = true` into `OnMouseUp`. If it does serve a real purpose, add the one
      sentence explaining why, per design.md decision 4.
- [ ] 2.6 `dotnet build src/Mod/Mod.csproj` and `dotnet test` (Core.Tests) — confirm both
      still pass/build clean; manually retest drag-reorder in the running game to confirm
      no behavior change.
- [ ] 2.7 Confirm `OnClickAddTask`'s placeholder-seeding (tasks.md 8.9 in
      `add-lectern-block`) remains untouched — out of scope for this change per design.md
      Non-Goals; do not make the `Core.AddTask` semantics change without separate sign-off.
