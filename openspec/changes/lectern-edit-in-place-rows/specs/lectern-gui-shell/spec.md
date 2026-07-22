## ADDED Requirements

### Requirement: Editor-view rows are custom-drawn in the interactive render pass
The lectern editor view SHALL render each task/note row as the same custom-drawn
`ScribeRowElement` used by the read view (in `ScribeRowMode.Edit`), drawn in the interactive
render pass so the row list is clipped natively by the dialog's scroll clip region. No editor
row content SHALL bleed outside the clipped scroll region at its top or bottom edge, and the
editor view SHALL scroll by a continuous offset shift rather than a per-step recompose.

#### Scenario: Editor rows are clipped, not culled, at the scroll boundary
- **WHEN** the editor view has more rows than fit the visible content area and the player
  scrolls so a row straddles the top or bottom edge of the scroll region
- **THEN** that row is drawn partially — clipped exactly at the region boundary — rather than
  popping fully in or out of existence, and no part of any row (text, checkbox, ruling, or the
  active edit field) paints outside the region

#### Scenario: Editor scrolling is continuous and sub-row
- **WHEN** the player scrolls the editor view by any increment (wheel, thumb drag, or track)
- **THEN** the rows slide continuously by the scrolled amount, including partial-row offsets,
  with no snap-to-row-boundary and no full recompose per scroll step

### Requirement: Editor view edits in place with a single floating input
The editor view SHALL edit row text in place using exactly one live text input element that is
repositioned onto the row the player is editing. Every non-focused row SHALL draw its text as a
static label; the focused row SHALL suppress drawing its own text label for that frame (still
drawing its checkbox and ruling) so the input and label never both paint the same text. The
static label and the floating input SHALL align via the shared `RowTextLayout` metric so that
gaining or losing focus produces no visible shift in text position, baseline, or font size.

#### Scenario: Focusing a row hands off from label to input with no jump
- **WHEN** the player clicks into a row to edit it
- **THEN** the live input appears at that row aligned to where the static label was, the row
  stops drawing its own static label, and the text does not visibly shift position or size

#### Scenario: Only one input is live at a time
- **WHEN** the player moves focus from one row to another
- **THEN** the single input is repositioned onto the newly focused row, the previously focused
  row resumes drawing its static label, and at no point are two live inputs present

### Requirement: Editor caret conventions match desktop editing idioms cross-platform
The editor's text input SHALL support caret navigation idioms on macOS as well as Windows.
Cmd+Left / Cmd+Right SHALL move the caret to the start / end of the current line; Alt/Option+
Left / Alt/Option+Right SHALL move the caret by whole words; and holding Shift with any caret-
movement key SHALL extend the selection rather than collapse it. These SHALL be provided by a
`GuiElementTextInput` subclass that routes the macOS modifier combinations onto the engine's
existing caret-movement logic (which otherwise responds only to Ctrl and discards Alt).

#### Scenario: Cmd+Arrow jumps to line ends on macOS
- **WHEN** the player presses Cmd+Right (or Cmd+Left) while editing a row on macOS
- **THEN** the caret moves to the end (or start) of the line, matching the behavior Ctrl+Arrow
  already provides, rather than doing nothing

#### Scenario: Alt/Option+Arrow skips by word
- **WHEN** the player presses Alt/Option+Right (or Left) while editing a row
- **THEN** the caret moves by one whole word in that direction rather than being ignored

#### Scenario: Shift extends selection during caret movement
- **WHEN** the player holds Shift while pressing any caret-movement combination (arrow,
  word-skip, or line-end)
- **THEN** the text selection extends to the new caret position instead of collapsing

### Requirement: Editor rows navigate and commit by keyboard
The editor SHALL let the player move between rows from the keyboard while editing. Pressing
Enter SHALL commit the current row's edit and move focus to the next row; pressing Shift+Tab
SHALL commit and move focus to the previous row. Committing an edit (by Enter, Shift+Tab, or
losing focus) SHALL apply the change through the existing lock-gated server edit path. Pressing
Esc SHALL commit the focused row (via the same blur-commit path) and close the dialog — a fast
panic-close, not an in-place revert. *(Decision reversed 2026-07-21 after playtest: the tester
wanted Esc to be a fast panic-close; see task 4.4.)*

#### Scenario: Enter commits and advances
- **WHEN** the player finishes typing in a row and presses Enter
- **THEN** the row's new text is committed through the server edit path and focus moves to the
  next row

#### Scenario: Shift+Tab commits and retreats
- **WHEN** the player presses Shift+Tab while editing a row
- **THEN** the row's edit is committed and focus moves to the previous row

#### Scenario: Esc commits and closes the dialog
- **WHEN** the player presses Esc while editing a row
- **THEN** the focused row's pending edit is committed (blur-commit fires on close) and the
  dialog closes, rather than reverting the row in place

#### Scenario: Blur commits the edit
- **WHEN** the player clicks away from an actively edited row without pressing Enter
- **THEN** the row's text is committed through the server edit path

### Requirement: Read and editor views share a single row-list width
The lectern's row list SHALL be a single consistent width across both the read view and the
editor view. Switching between views on the same lectern SHALL NOT change the row-list width.

#### Scenario: Row-list width is identical in both views
- **WHEN** the player switches between read and editor view on the same lectern
- **THEN** the row list occupies the same width in both views, with no visible reflow or
  resize of the list column
