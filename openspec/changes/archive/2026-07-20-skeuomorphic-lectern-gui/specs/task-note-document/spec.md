## ADDED Requirements

### Requirement: Pin a task
The system SHALL allow toggling a "pinned" flag on a task block, identified by its
position. Pinning SHALL NOT be allowed on a text-section block.

#### Scenario: Pin an unpinned task
- **WHEN** an unpinned task is toggled
- **THEN** it becomes pinned

#### Scenario: Unpin a pinned task
- **WHEN** a pinned task is toggled
- **THEN** it becomes unpinned

#### Scenario: Pinning a text section fails
- **WHEN** a pin toggle targets a text-section block
- **THEN** the document is unchanged and the operation reports failure

#### Scenario: Pinning an invalid position fails safely
- **WHEN** a pin toggle references a position that does not exist
- **THEN** the document is left unchanged and the operation reports failure

### Requirement: Reserved assignment field
Every block SHALL carry an assignment field (an optional identifier, absent by default)
that is persisted through serialization but has no mutation operation and no consumer in
this document capability. This field exists so a future capability can define its own
semantics (e.g. assigning a task to a player or group) without a further format change.

#### Scenario: A new block has no assignment
- **WHEN** a task or text section is added
- **THEN** its assignment field is absent (unset)

#### Scenario: Assignment survives serialization even though nothing sets it
- **WHEN** a document containing blocks with unset assignment fields is serialized and
  deserialized
- **THEN** the resulting blocks still have their assignment fields absent (unset)

### Requirement: Serialization round-trip includes pin and assignment fields
The system's document serialization SHALL preserve each block's pinned flag and
assignment field, in addition to the fields already preserved (kind, text, completed
flag, depth). A document serialized under an earlier format version SHALL fail to
deserialize rather than silently defaulting or misreading the new fields.

#### Scenario: Round-trip preserves pinned state
- **WHEN** a document with a mix of pinned and unpinned tasks is serialized and then
  deserialized
- **THEN** the resulting document's tasks have the same pinned state, in the same order,
  as the original

#### Scenario: An earlier format version fails to deserialize
- **WHEN** bytes produced by an earlier serialization format version are deserialized
- **THEN** deserialization reports failure rather than producing a document with
  incorrect or default pinned/assignment values
