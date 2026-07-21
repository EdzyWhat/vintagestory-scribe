# lectern-gui-shell

## Purpose

TBD - created via spec sync from change `skeuomorphic-lectern-gui`. This is a new capability
covering the skeuomorphic layout and interaction shell of the lectern's GUI dialog.

## Requirements

### Requirement: Lectern dialog uses a portrait, custom-drawn backdrop
The lectern's GUI dialog (both read view and editor view) SHALL be laid out in a portrait
(taller-than-wide) aspect ratio and SHALL render a custom-drawn backdrop image in place of
the engine's default shaded dialog panel.

#### Scenario: Opening the lectern shows a portrait, skinned dialog
- **WHEN** a player right-clicks or shift+right-clicks a placed lectern
- **THEN** the opened dialog is taller than it is wide, and its background is the custom
  backdrop image rather than the default `AddShadedDialogBG` panel

#### Scenario: Backdrop is swappable without a code change
- **WHEN** the placeholder backdrop asset is replaced with a different image (or draw
  routine) of matching dimensions
- **THEN** the dialog renders the new backdrop with no changes required to
  `GuiDialogScribeLectern.cs`'s layout or composition logic

### Requirement: Task/note row list scrolls within a clipped region
The lectern GUI's task/note row list SHALL render inside a scrollable, clipped content
region, so a document with more rows than fit the visible dialog height remains fully
reachable by scrolling, with no content rendered off-screen and unreachable.

#### Scenario: A long document remains fully reachable
- **WHEN** a lectern's document has enough tasks and/or note sections that their combined
  height exceeds the dialog's visible content area
- **THEN** a scrollbar or equivalent scroll interaction appears, and every row remains
  reachable by scrolling — no row is rendered permanently off-screen

#### Scenario: The existing text-size cap stopgap is superseded
- **WHEN** the scrollable region is in place
- **THEN** the text-size slider's upper bound is no longer constrained by the absence of a
  scrollable region (the `MaxTextSizePercent` cap introduced as a stopgap for task 8.15 may
  be revisited, since the original overflow problem it guarded against is now handled by
  scrolling instead)

### Requirement: Row list scrolls continuously; no pagination
The lectern GUI SHALL present the task/note row list as a single continuously scrollable
list. It SHALL NOT split content into discrete fixed-size pages with page-turn navigation.

#### Scenario: No page-turn controls are present
- **WHEN** the lectern's editor or read view is composed
- **THEN** no "Prev"/"Next" page-turn controls or page-count indicator are present in the
  dialog — only the continuous scroll region from the requirement above

### Requirement: Row checkbox scales with the text-size preference
A task row's checkbox SHALL render at a size proportional to the current text-size
preference, in the same proportion as the row's text and height, rather than a fixed
pixel size.

#### Scenario: Checkbox grows and shrinks with text size
- **WHEN** the player changes the text-size preference to a larger or smaller value
- **THEN** every task row's checkbox visibly grows or shrinks along with the row's text,
  rather than staying a constant size while the text around it changes

### Requirement: Row icons are hover-conditional
A row's per-row icon controls (at minimum the delete icon and the pin-toggle icon) SHALL
be visually hidden unless the mouse is currently positioned over that row, rather than
always rendered.

#### Scenario: An icon appears only while hovering its row
- **WHEN** the mouse moves over a task or note row
- **THEN** that row's icon controls become visible, and become hidden again once the
  mouse moves off that row

#### Scenario: Hovering does not disturb active typing
- **WHEN** the mouse moves over a row while the player is actively typing in a different
  row's text field
- **THEN** the typing field's focus and caret position are unaffected by the hover-driven
  visibility change

### Requirement: Focus ring is scoped to the active field
When a text field (a task's text input or a note's text area) has input focus, the GUI
SHALL visually indicate focus on that field specifically, not on the row as a whole.

#### Scenario: Only the focused field is highlighted
- **WHEN** the player clicks into a row's text field to edit it
- **THEN** a focus indicator appears around that field, and no other part of the row
  (its checkbox, icons, or drag handle) is highlighted as focused

### Requirement: Task rows expose a pin-toggle affordance
Each task row in the editor view SHALL provide a control that toggles the task's pinned
flag. Text-section rows SHALL NOT expose this control.

#### Scenario: Toggling pin from the GUI
- **WHEN** the player activates a task row's pin-toggle control
- **THEN** the task's pinned flag flips, and the control's visual state reflects the new
  value

#### Scenario: Text sections have no pin control
- **WHEN** a text-section row is composed
- **THEN** no pin-toggle control is present for that row

### Requirement: No assignment UI in the lectern
The lectern GUI SHALL NOT expose any column, toggle, or other control for a block's
assignment field. The field exists in the underlying document model but has no consumer
in this capability.

#### Scenario: Assignment is not visible or editable from the lectern
- **WHEN** the lectern's editor or read view is composed
- **THEN** no assignment-related column, label, or control appears anywhere in the dialog
