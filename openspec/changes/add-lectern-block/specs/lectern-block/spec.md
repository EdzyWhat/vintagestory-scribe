## ADDED Requirements

### Requirement: Craftable, placeable lectern block

The system SHALL provide a lectern block that reuses the vanilla "Aged book lectern"
appearance, can be obtained (via a crafting recipe or creative inventory), placed in the
world, and broken to be recovered.

#### Scenario: Place and break the lectern

- **WHEN** a player places the lectern block and later breaks it
- **THEN** the block appears in the world when placed and is returned to the player's inventory when broken

### Requirement: Open the lectern's editor

The system SHALL let a player open the lectern's editing GUI, both by looking at the
block and pressing a rebindable hotkey, and by right-clicking the block.

#### Scenario: Open by right-click

- **WHEN** a player right-clicks a placed lectern
- **THEN** the Scribe editing GUI opens showing that lectern's tasks and note

#### Scenario: Open by hotkey while looking at it

- **WHEN** a player looks at a placed lectern and presses the (rebindable) open hotkey
- **THEN** the Scribe editing GUI opens showing that lectern's tasks and note

#### Scenario: Hotkey does nothing when not aimed at a lectern

- **WHEN** a player presses the open hotkey while not looking at a lectern
- **THEN** no Scribe GUI opens

### Requirement: Edit tasks and note through the GUI

The system SHALL let the player add, rename, toggle-complete, and delete tasks, and edit
the freeform note, from the lectern's GUI.

#### Scenario: Add and complete a task

- **WHEN** the player adds a task "Build a forge" and then marks it complete in the GUI
- **THEN** the lectern's document contains that task shown as completed

#### Scenario: Edit the note

- **WHEN** the player types a note and confirms/saves in the GUI
- **THEN** the lectern's document stores that note text

### Requirement: Server-authoritative persistence

The system SHALL treat the server as the source of truth for a lectern's document: edits
made in the client GUI are sent to the server, applied there, saved with the world, and
survive a save/reload.

#### Scenario: Edits persist across reload

- **WHEN** a player edits a lectern's tasks and note, then the world is saved and reloaded
- **THEN** reopening that lectern shows the same tasks and note

#### Scenario: Client edits are not trusted directly

- **WHEN** the client GUI changes a task or note
- **THEN** the change is sent to the server and only takes lasting effect after the server applies it (a client that fails to reach the server does not permanently change the stored document)

### Requirement: Multiplayer synchronization

The system SHALL synchronize a lectern's document to players in multiplayer, so a change
made by one player is seen by another who opens the same lectern.

#### Scenario: Two players see the same content

- **WHEN** player A edits a lectern and player B then opens the same lectern
- **THEN** player B sees player A's changes

### Requirement: Each lectern is independent

The system SHALL key each lectern's document to that block's position, so different
lecterns hold different documents.

#### Scenario: Separate lecterns hold separate notes

- **WHEN** two lecterns are placed and each is given different tasks
- **THEN** each lectern shows only its own tasks and note
