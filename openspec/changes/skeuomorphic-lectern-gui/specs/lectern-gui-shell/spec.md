## ADDED Requirements

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
