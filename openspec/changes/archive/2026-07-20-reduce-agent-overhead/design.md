## Context

Two unrelated but small cleanups bundled into one change: reducing Claude Code tooling
overhead (plugins, redundant slash commands) and resolving three weak-reason methods
found while auditing `src/Mod/` documentation. `src/Mod/` currently has uncommitted changes
in another terminal finishing `add-lectern-block` group 8 (playtesting bugfixes), so the
code-hygiene half must be sequenced around that, not done concurrently.

## Goals / Non-Goals

**Goals:**

- Remove tooling/context overhead that has nothing to do with this project's stack.
- Give `ScribeModSystem`'s four network handlers one shared lookup instead of four copies.
- Make `OnRowDragMouseUp`'s reason for existing explicit (either by removing it or
  documenting it).

**Non-Goals:**

- Changing `Core.AddTask`'s blank-text-rejection semantics to support a real GUI
  placeholder (task 8.9 in `add-lectern-block/tasks.md`) — needs explicit sign-off first,
  tracked separately.
- Disabling `swift-lsp` globally in `~/.claude/settings.json` — out of this repo's scope;
  recorded as a recommendation only.
- Any behavior change to the lectern's persistence, networking, or GUI — this is a pure
  refactor of existing logic, not a new capability.

## Decisions

**1. Bundle tooling cleanup and code-hygiene fixes in one change, sequenced, not two.**
Both are small, low-risk, and came out of the same review pass. Splitting them into two
changes would double the OpenSpec ceremony for work this size.
*Alternative:* two separate changes — rejected as overhead disproportionate to the work.

**2. Wait for `add-lectern-block`'s in-flight `src/Mod/` changes to land before starting
the code-hygiene tasks; the tooling tasks (`.claude/` files) have no such dependency and
can start immediately.**
*Why:* `src/Mod/ScribeModSystem.cs` and `src/Mod/GuiDialogScribeLectern.cs` both have
uncommitted edits in another terminal right now (playtesting bugfixes). Touching them
concurrently risks a merge conflict on files mid-edit elsewhere.
*Alternative:* work in a git worktree in parallel — rejected as unnecessary complexity for
a same-day, low-conflict-surface refactor; simpler to just wait.

**3. `TryGetLectern` returns the block entity directly (nullable), not a bool + out
param.**
Matches the existing call-site pattern (`if (... is { } lectern)`) already used in all four
handlers being consolidated, so the refactor is a pure extraction with no new idiom
introduced.
*Alternative:* `TryGetX(..., out var lectern)` bool-returning form — more conventional for
a "Try" prefix, but every existing call site already uses the pattern-match-on-nullable
style; matching it minimizes the diff and keeps one style in the file.

**4. Resolve `OnRowDragMouseUp` by removing it and inlining `args.Handled = true` into
`OnMouseUp`, rather than keeping it with an explanatory comment — pending confirmation
during implementation that no drag-specific behavior depends on the per-row callback
existing separately.**
*Why:* nothing found during the doc audit indicates the per-row split serves a purpose the
dialog-level `OnMouseUp` doesn't already cover; the simplest fix is removing the apparently
-dead indirection. If implementation turns up a real reason it must stay separate (e.g.
stopping the row's own drag-handle element from also handling the event), document that
reason instead of removing it — this is a judgment call to make with the code in front of
you, not a foregone conclusion from the audit alone.

## Risks / Trade-offs

- **Merge conflict with the in-progress `add-lectern-block` work** → mitigated by decision
  2 (sequencing); tasks.md marks the code-hygiene group as blocked-until-clear.
- **Removing `.claude/commands/opsx/` could surprise a user who has a muscle-memory habit
  of typing `/opsx:propose` etc. rather than natural-language skill triggers** → low risk;
  the scoped `openspec-*` skills trigger on the same natural-language cues, and the CLI
  commands them wrap are unchanged.
