## ADDED Requirements

### Requirement: Custom row-control icons are registered as SVG assets

The mod SHALL register a documented set of custom SVG glyphs for the lectern's row-control
affordances (pin, drag-handle grip, delete/close, edit) at client initialization, each
available to GUI elements by a stable code string. The glyphs SHALL be shipped as SVG assets
under the `textures` asset category and authored as single-flat-color silhouettes so the
drawing caller supplies the ink color.

#### Scenario: The four icons are available by code after client init

- **WHEN** the client has finished `StartClientSide`
- **THEN** the codes `scribepin`, `scribegrip`, `scribeclose`, and `scribeedit` are each
  registered such that a GUI element drawing that code renders the corresponding custom SVG
- **AND** each renders recolored to the color the caller passes (not a baked-in color)

#### Scenario: Icon assets live under a real asset category

- **WHEN** the SVG files are placed in the mod's assets
- **THEN** they reside under `assets/scribe/textures/icons/` (the `textures` category), not a
  bare `icons/` folder that Vintage Story never scans
- **AND** each icon's `AssetLocation` resolves and loads its data via `TryGet`

### Requirement: Custom SVG icon registration survives asset unload

The custom-icon registration SHALL NOT capture a loaded asset object, because Vintage Story
unloads asset data after startup. The registered renderer SHALL re-resolve its asset on each
draw so that an unloaded asset is reloaded on demand, and SHALL degrade to drawing nothing
(never throwing) when the asset cannot be loaded.

#### Scenario: Icon still draws after the game unloads asset data

- **WHEN** an icon is drawn some time after client init (after the engine has unloaded asset
  data)
- **THEN** the icon renders correctly rather than crashing the client

#### Scenario: A missing or unloadable icon asset does not crash

- **WHEN** a registered icon's asset is missing or cannot be loaded at draw time
- **THEN** the renderer draws nothing and the client continues running
- **AND** the failure is logged for diagnosis

### Requirement: Icon registration is decoupled from row-control buttons

Registering the custom icons SHALL NOT, by itself, add or repoint any per-row control button.
The buttons that consume these codes are owned by separate changes; this capability only makes
the codes available.

#### Scenario: Registration adds no interactive controls

- **WHEN** the icons are registered but no consuming change has run
- **THEN** the live row's interactive behavior is unchanged (checkbox + text + ruling only)
- **AND** no button is wired to a code string that would otherwise be dead code
