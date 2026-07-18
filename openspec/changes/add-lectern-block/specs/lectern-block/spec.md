## ADDED Requirements

### Requirement: Craftable, placeable lectern block

The system SHALL provide a lectern block that reuses the vanilla "Aged book lectern"
appearance, can be obtained (via a crafting recipe or creative inventory), placed in the
world, and broken to be recovered.

#### Scenario: Place and break the lectern

- **WHEN** a player places the lectern block and later breaks it
- **THEN** the block appears in the world when placed and is returned to the player's inventory when broken

### Requirement: Open the lectern's editor

The system SHALL let a player open the lectern's editing GUI by right-clicking the block.

#### Scenario: Open by right-click

- **WHEN** a player right-clicks a placed lectern
- **THEN** the Scribe editing GUI opens showing that lectern's tasks and note

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

### Requirement: One editor at a time

The system SHALL allow only one player to have a given lectern's editor open at a time.
While one player has it open, another player who tries to open the same lectern SHALL be
refused with a message such as "Only one person can use the lectern at a time," and the
lock SHALL be released when the first player closes the editor (or disconnects).

#### Scenario: Second player is refused while it's in use

- **WHEN** player A has a lectern's editor open and player B right-clicks the same lectern
- **THEN** player B's editor does not open and player B sees a "one person at a time" message

#### Scenario: Lock releases on close

- **WHEN** player A closes the lectern's editor and player B then right-clicks it
- **THEN** player B's editor opens normally

#### Scenario: Lock releases if the holder disconnects

- **WHEN** player A has the editor open and then disconnects without closing it
- **THEN** the lectern becomes available for another player to open

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
