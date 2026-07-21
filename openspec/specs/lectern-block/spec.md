# lectern-block

## Purpose

TBD - created via spec sync from change `skeuomorphic-lectern-gui`. The base lectern-block
requirements are owned by the not-yet-synced `add-lectern-block` change; this file currently
holds only the requirements added by `skeuomorphic-lectern-gui`.

## Requirements

### Requirement: Pin a task from the GUI
The lectern's GUI SHALL let the player toggle a task's pinned flag. This control SHALL
NOT be available for text-section blocks.

#### Scenario: Pin a task in the editor
- **WHEN** the player activates a task row's pin-toggle control in the editor view
- **THEN** the lectern's document records that task as pinned, and the control's visual
  state reflects it

#### Scenario: Unpin a task in the editor
- **WHEN** the player activates a pinned task row's pin-toggle control again
- **THEN** the lectern's document records that task as no longer pinned

#### Scenario: Pinned state persists across reload
- **WHEN** a task is pinned, then the world is saved and reloaded
- **THEN** reopening that lectern shows the task still pinned
