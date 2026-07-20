# agent-tooling-footprint

## Purpose

Keep the agent tooling that loads into every Claude Code session in this repo scoped to
Scribe's C#/.NET/Vintage Story stack, so unrelated skill and command descriptions do not
consume session context.

## Requirements

### Requirement: Only stack-relevant plugins are enabled for this repo
This repo's `.claude/settings.json` SHALL disable any Claude Code plugin whose skills are
unrelated to Scribe's C#/.NET/Vintage Story stack, so unrelated skill descriptions are not
loaded into every session's context.

#### Scenario: Salesforce-stack plugins are disabled
- **WHEN** a Claude Code session starts in this repo
- **THEN** `salesforce-claude-support`, `salesforce-trust-foundations`, `swift-lsp`, and
  `google-workspace` are disabled per `.claude/settings.json`'s `enabledPlugins` map

### Requirement: No duplicate slash commands for an already-installed skill
This repo SHALL NOT carry a `.claude/commands/` entry whose triggers and instructions
duplicate an already-installed `.claude/skills/` entry, so the skill listing shown to the
agent has no redundant entries.

#### Scenario: OpenSpec slash commands are removed in favor of the scoped skills
- **WHEN** the repo's `.claude/` directory is inspected
- **THEN** `.claude/commands/opsx/` does not exist, and the six OpenSpec workflows
  (propose, apply, explore, update, sync, archive) remain reachable only via the scoped
  `vintagestory-scribe:openspec-*` skills
