## ADDED Requirements

### Requirement: Document structure

The system SHALL represent a Scribe document as an ordered list of tasks plus a single
freeform note. Each task SHALL have text and a completed flag. The note SHALL be free
text that may be empty. This model MUST NOT depend on the Vintage Story API.

#### Scenario: A new document is empty

- **WHEN** a new document is created
- **THEN** it has zero tasks and an empty note

#### Scenario: Task order is preserved

- **WHEN** several tasks are added in sequence
- **THEN** the document lists them in the order they were added

### Requirement: Add a task

The system SHALL allow adding a task with given text to the end of the task list.

#### Scenario: Add a task to an empty document

- **WHEN** a task with text "Find copper" is added to an empty document
- **THEN** the document contains exactly one task, with text "Find copper" and not completed

#### Scenario: Adding trims and rejects blank text

- **WHEN** a task is added with text that is empty or only whitespace
- **THEN** no task is added and the operation reports that the input was invalid

### Requirement: Rename a task

The system SHALL allow changing the text of an existing task identified by its position.

#### Scenario: Rename an existing task

- **WHEN** the text of the task at position 0 is changed to "Find tin"
- **THEN** the task at position 0 has text "Find tin" and its completed flag is unchanged

#### Scenario: Rename rejects blank text

- **WHEN** a task's text is changed to empty or only whitespace
- **THEN** the task text is unchanged and the operation reports that the input was invalid

### Requirement: Toggle task completion

The system SHALL allow toggling the completed flag of an existing task.

#### Scenario: Toggle an incomplete task

- **WHEN** an incomplete task is toggled
- **THEN** it becomes completed

#### Scenario: Toggle a completed task

- **WHEN** a completed task is toggled
- **THEN** it becomes incomplete

### Requirement: Delete a task

The system SHALL allow removing a task by its position, preserving the order of the rest.

#### Scenario: Delete a task from the middle

- **WHEN** the task at position 1 of three tasks is deleted
- **THEN** the document has two tasks, and they are the former first and third tasks in that order

### Requirement: Edit the note

The system SHALL allow replacing the freeform note with new text, including empty text.

#### Scenario: Set the note

- **WHEN** the note is set to "Copper is south of the ridge"
- **THEN** the document's note equals "Copper is south of the ridge"

#### Scenario: Clear the note

- **WHEN** the note is set to empty text
- **THEN** the document's note is empty

### Requirement: Out-of-range operations are safe

The system SHALL reject task operations that reference a position outside the current
task list without corrupting the document or throwing to the caller.

#### Scenario: Operate on an invalid position

- **WHEN** a rename, toggle, or delete targets a position that does not exist
- **THEN** the document is left unchanged and the operation reports failure

### Requirement: Serialization round-trip

The system SHALL serialize a document to a byte array and deserialize it back to an
equal document, so it can be persisted and sent over the network.

#### Scenario: Round-trip preserves content

- **WHEN** a document with several tasks (mixed completion) and a note is serialized and then deserialized
- **THEN** the resulting document equals the original in tasks, their order, their completed flags, and the note

#### Scenario: Deserializing invalid data fails safely

- **WHEN** deserialization is given empty or malformed bytes
- **THEN** it reports failure rather than throwing, and yields no partial document
