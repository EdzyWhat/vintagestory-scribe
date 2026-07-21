## ADDED Requirements

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

### Requirement: Read-view checkbox toggles task done state
The read view's task checkbox SHALL be interactive: clicking it toggles that task's done state
through the existing server-authoritative mutation and sync path. No other part of a read-view
row SHALL be interactive — the read view exposes no text editing, drag, or per-row icon controls.

#### Scenario: Clicking a read-view checkbox toggles done
- **WHEN** the player clicks a task row's checkbox in the read view
- **THEN** that task's done state flips, the change is applied server-authoritatively and synced
  back, and the checkbox glyph updates to reflect the new state

#### Scenario: The rest of a read-view row is inert
- **WHEN** the player clicks or hovers a read-view row anywhere other than its checkbox
- **THEN** no edit field opens, no row reorder begins, and no per-row icon control activates
