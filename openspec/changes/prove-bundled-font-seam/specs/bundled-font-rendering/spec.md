## ADDED Requirements

### Requirement: A bundled TTF loads via FreeType-direct, not name-based selection

The mod SHALL render its own GUI text in a bundled `.ttf` typeface by loading that file directly
through `Cairo.Util.FreeTypeFontFace.Create(ttfPath, loadoptions)` (backed by the game's bundled
`freetype6` native library) and applying the returned `FontFace` with
`Cairo.Context.SetContextFontFace(face)`. The mod SHALL NOT attempt to render the bundled face by
passing its family name to `CairoFont`/`SelectFontFace`, because name-based resolution goes through
the OS/fontconfig registry and cannot resolve a bundled file cross-platform.

#### Scenario: The bundled face loads without an OS font install

- **WHEN** the client initializes with the bundled Caudex `.ttf` present in the mod's assets and
  the font not installed at the OS level
- **THEN** `Cairo.Util.FreeTypeFontFace.Create` returns a usable `FontFace` for that file
- **AND** the mod uses that `FontFace` for drawing, not a name passed to `SelectFontFace`

#### Scenario: No global font configuration is touched

- **WHEN** the bundled-font path is active
- **THEN** the mod SHALL NOT modify `clientsettings.json defaultFontName` or require any OS font
  installation
- **AND** the mechanism relies only on the already-vendored `Lib/cairo-sharp.dll` and the bundled
  `freetype6`, adding no new package or mod dependency

### Requirement: The bundled face applies to only the mod's own text

The bundled face SHALL be applied only on Scribe's own baked drawing surface, at the existing
lectern row-text draw seam in `ScribeRowElement.ComposeElements`, so that no other GUI text in the
game is affected. The face override SHALL be applied to the row's private `Context` after
`CairoFont.SetupContext` and before the multiline text draw.

#### Scenario: Only the lectern row text changes typeface

- **WHEN** the lectern dialog is open with the bundled-font path active
- **THEN** the lectern's row text renders in the bundled Caudex face
- **AND** all other in-game GUI text (menus, tooltips, other dialogs) renders in its normal font,
  unchanged

#### Scenario: The face override is applied last on the row's own context

- **WHEN** a row bakes its text in `ScribeRowElement.ComposeElements`
- **THEN** `CairoFont.SetupContext(ctx)` is called first (establishing size/color/matrix)
- **AND** `ctx.SetContextFontFace(cachedFace)` is applied afterward so `SelectFontFace` does not
  clobber the bundled face
- **AND** the override targets the row's own local `ImageSurface`/`Context`, not the shared static
  surface

### Requirement: Font size survives the face override

The rendered row text SHALL appear at the size established by the row's `CairoFont` (the
`RowFont()` size, scaled by the client text-size setting), not at a default or unscaled size, after
the face override is applied. Because `SetContextFontFace` sets the face but not necessarily the
size, the implementation SHALL verify the size at runtime and, if the face swap resets it,
re-apply the scaled font size after the override.

#### Scenario: Text renders at the configured size in the bundled face

- **WHEN** the lectern row text is drawn in the bundled face
- **THEN** the glyphs render at the same size the engine default face rendered at for the current
  text-size setting
- **AND** if the face swap alone does not preserve the size, the scaled font size is re-applied so
  the result matches

### Requirement: The bundled asset resolves to a real filesystem path

Because `FreeTypeFontFace.Create` requires a real filesystem path, the mod SHALL resolve the
bundled `.ttf` to a real file for loading, extracting the asset bytes to a temporary file when the
mod is running from a packed `.zip` where no such path exists. The resolution SHALL work for both
the unpacked dev layout and a released packed `.zip`.

#### Scenario: The face loads from a released packed mod

- **WHEN** the mod runs as a packed `.zip` (no direct filesystem path to the asset)
- **THEN** the mod reads the asset bytes, writes them to a temporary file, and loads the face from
  that temp path
- **AND** the row text renders correctly

#### Scenario: The face loads from an unpacked dev build

- **WHEN** the mod runs unpacked with the asset present as a real file
- **THEN** the face loads (whether directly from the asset path or via the same temp-file path) and
  the row text renders correctly

### Requirement: The loaded face is cached once and disposed at shutdown

The mod SHALL create the `FontFace` exactly once per client session (at client initialization) and
reuse the cached instance for all row draws; it SHALL NOT create the face per row or per frame.
The cached `FontFace` SHALL be disposed when the client shuts down.

#### Scenario: The face is not recreated per draw

- **WHEN** the lectern dialog recomposes its rows repeatedly (multiple `ComposeElements` calls)
- **THEN** the same cached `FontFace` instance is reused for every row draw
- **AND** no new `FreeTypeFontFace.Create` call occurs per row or per frame

#### Scenario: The face is freed on shutdown

- **WHEN** the client shuts down
- **THEN** the cached `FontFace` is disposed, releasing its native FreeType handle

### Requirement: The FreeType-direct path is proven on Apple Silicon

The spike SHALL be validated by running it on the author's Apple Silicon (arm64) macOS machine,
confirming the `freetype6` P/Invoke load and the FreeType face draw render correctly on that
hardware, since this is one of the runtime behaviors a static decompile could not settle.

#### Scenario: The bundled face renders on arm64 macOS

- **WHEN** the spike build runs on the author's Apple Silicon Mac and the lectern is opened
- **THEN** the row text renders in the bundled face without crash, native-interop error, or garbled
  glyphs

### Requirement: The bundled font's license is honored

The mod SHALL ship the bundled font's license file (`OFL.txt`) alongside the `.ttf` and SHALL
credit the font (Caudex, SIL OFL 1.1) in a `CREDITS` file. If the font files were modified they
SHALL NOT be redistributed under the original reserved font name; for this spike the files are
unmodified.

#### Scenario: License artifacts ship with the font

- **WHEN** the mod is packaged with the bundled Caudex `.ttf`
- **THEN** Caudex's `OFL.txt` is included in the package
- **AND** a `CREDITS` file names Caudex and its SIL OFL 1.1 license
