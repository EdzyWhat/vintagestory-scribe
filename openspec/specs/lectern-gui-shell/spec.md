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

### Requirement: Read-view rows are custom-drawn in the interactive render pass
The lectern read view SHALL render each task/note row as a single custom-drawn element in the
interactive render pass (not as static-baked chrome), so that the row list is clipped natively
by the dialog's scroll clip region. No row content SHALL bleed outside the clipped scroll region
at its top or bottom edge.

#### Scenario: Rows are clipped, not culled, at the scroll boundary
- **WHEN** the read view's document has more rows than fit the visible content area and the
  player scrolls so a row straddles the top or bottom edge of the scroll region
- **THEN** that row is drawn partially — clipped exactly at the region boundary — rather than
  popping fully in or out of existence, and no part of any row paints outside the region

#### Scenario: Scrolling is continuous and sub-row
- **WHEN** the player scrolls the read view by any increment (wheel, thumb drag, or track)
- **THEN** the rows slide continuously by the scrolled amount, including partial-row offsets,
  with no snap-to-row-boundary and no full recompose required per scroll step

### Requirement: Read-view rows render a structural lined-paper ruling
Each read-view row SHALL draw a lined-paper ruling as a structural part of the row (drawn per
row and scrolling with the row), rather than relying on separately-baked divider chrome. The
spacing (padding) between the row text and its ruling SHALL scale with the current text-size
preference. The ruling SHALL be authored so its visual can be replaced (e.g. with an image)
without changing the row's layout logic.

#### Scenario: Ruling scrolls with its row
- **WHEN** the player scrolls the read view
- **THEN** each row's ruling moves together with that row's text and checkbox as one unit,
  staying aligned to the row it belongs to

#### Scenario: Ruling padding scales with text size
- **WHEN** the player changes the text-size preference to a larger or smaller value
- **THEN** the padding between a row's text and its ruling grows or shrinks in proportion,
  rather than staying a fixed pixel gap

### Requirement: Read-view checkbox is a custom-drawn glyph
Task rows in the read view SHALL render their checkbox as a custom-drawn glyph rather than the
engine's default `GuiElementSwitch` control. The glyph SHALL continue to scale with the
text-size preference (consistent with the existing checkbox-scaling requirement).

#### Scenario: Checkbox shows done and not-done states
- **WHEN** a task row is drawn in the read view
- **THEN** its checkbox glyph reflects the task's current done state (a checked vs. unchecked
  appearance), drawn by the mod rather than the default engine switch

### Requirement: Read-view checkbox toggles task done state without the editor lock
The read view's task checkbox SHALL be interactive: clicking it toggles that task's done state.
Because the read view holds no editor lock, toggling done SHALL be an always-allowed server
action that does NOT require acquiring the single-editor lock, applied server-authoritatively
and re-synced to all viewers. A player SHALL be able to toggle a task's done state from the read
view even while another player holds the editor lock. No other part of a read-view row SHALL be
interactive — the read view exposes no text editing, drag, or per-row icon controls.

#### Scenario: Clicking a read-view checkbox toggles done
- **WHEN** the player clicks a task row's checkbox in the read view
- **THEN** that task's done state flips, the change is applied server-authoritatively (without
  requiring the editor lock) and synced back, and the checkbox glyph updates to reflect the new
  state

#### Scenario: Toggling done works while someone else is editing
- **WHEN** a player clicks a read-view task checkbox while a different player holds the lectern's
  editor lock
- **THEN** the toggle is still applied and synced, and is not rejected for lack of the lock

#### Scenario: The rest of a read-view row is inert
- **WHEN** the player clicks or hovers a read-view row anywhere other than its checkbox
- **THEN** no edit field opens, no row reorder begins, and no per-row icon control activates
