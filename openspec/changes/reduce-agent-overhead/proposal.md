## Why

Every Claude Code session in this repo currently loads context it never uses: four
Salesforce-stack plugins (contributing dozens of unrelated skill descriptions) that have
nothing to do with a C#/.NET game mod, and six `.claude/commands/opsx/*.md` files that
duplicate skills already installed under `.claude/skills/openspec-*`. Separately, a
documentation pass over `src/Mod/` (done while v1's `add-lectern-block` change is still
in progress in another terminal) found three methods whose reason for existing is weak or
undocumented — worth fixing before v2 builds on top of them. Bundling both now keeps
token/tool overhead down and starts v2 on a slightly cleaner `Mod` layer.

## What Changes

- Commit the already-created (currently untracked) `.claude/settings.json`, which disables
  the four Salesforce-stack plugins (`salesforce-claude-support`, `salesforce-trust-foundations`,
  `swift-lsp`, `google-workspace`) for this project. None are relevant to Scribe's stack.
- Remove `.claude/commands/opsx/` (`propose.md`, `apply.md`, `explore.md`, `update.md`,
  `sync.md`, `archive.md`) — thin wrappers with the same triggers and instructions as the
  already-installed `.claude/skills/openspec-{propose,apply-change,explore,update-change,
  sync-specs,archive-change}` skills. Removing them drops six redundant entries from every
  skill listing with no loss of capability.
- Extract a shared `TryGetLectern` lookup helper in `ScribeModSystem` to replace four
  near-identical "rebuild `BlockPos`, look up `BlockEntityScribeLectern`, delegate" blocks.
- Resolve `GuiDialogScribeLectern.OnRowDragMouseUp`: either fold its one line
  (`args.Handled = true`) into `OnMouseUp`, or add the one sentence explaining why it must
  intercept the mouse-up event separately from the dialog-level handler.
- Leave `OnClickAddTask`'s placeholder-seeding as a tracked follow-up, not a fix here — it's
  already flagged in `tasks.md` task 8.9 and blocked on a `Core.AddTask` semantics change
  (currently rejects blank text) that needs explicit sign-off, not a decision made solo.

Non-goal: disabling `swift-lsp` globally in `~/.claude/settings.json` (it shows zero usage
across all projects, not just this one). That's a global setting outside this repo's
control — recorded here as a recommendation for the user to action themselves, not
something this change touches.

## Capabilities

No player-facing, in-game capability is added, removed, or modified by this change — the
code-hygiene tasks are a pure refactor of existing `Mod`-layer logic. The one testable,
spec-worthy behavior this change introduces is process/tooling, not gameplay: what Claude
Code configuration this repo carries.

### New Capabilities

- `agent-tooling-footprint`: the set of Claude Code plugins, slash commands, and skills
  enabled for this repo, and the rule that nothing irrelevant to Scribe's C#/.NET stack
  should be enabled, and nothing should duplicate an existing skill's triggers.

### Modified Capabilities

<!-- None. -->

## Impact

- **Tooling config:** `.claude/settings.json` (commit as-is), `.claude/commands/opsx/`
  (delete).
- **Code:** `src/Mod/ScribeModSystem.cs` (add `TryGetLectern`, use it in all four handlers),
  `src/Mod/GuiDialogScribeLectern.cs` (resolve `OnRowDragMouseUp`).
- **Coordination risk:** `src/Mod/` has uncommitted changes in another terminal finishing
  `add-lectern-block` group 8 (playtesting bugfixes) right now. The code-hygiene tasks in
  this change must not start until that work lands, to avoid merge conflicts on the same
  files.
- **Dependencies:** none.
- **Compatibility:** no behavior change intended; `TryGetLectern` is a pure refactor of
  existing logic.
