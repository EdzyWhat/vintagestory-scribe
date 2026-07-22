## Why

The fuller presentation vision (`docs/specs/presentation-and-fonts.md` Item 3) wants Scribe's
own text drawn in bundled, tier-specific typefaces (a rustic script for notebooks, cuneiform
block letters for the clay tablet) *without* the community "global font swap" (an OS font install
plus `clientsettings.json defaultFontName`), which is game-wide and cannot be scoped to one mod.
A decompile of the game DLLs confirmed a mod-scopable mechanism exists — but it hinges on three
runtime behaviors the decompile could **not** settle. Before any tier commits to a font system,
we need a small, throwaway-scale spike that proves the mechanism actually renders a bundled face
on **only** Scribe's own text, on the author's hardware.

## What Changes

- Bundle one license-cleared TTF (Caudex, a humanist serif under the SIL OFL 1.1) as a mod
  asset, purely to prove the loading path.
- Load that face at client init via `Cairo.Util.FreeTypeFontFace.Create(ttfPath, loadoptions)`
  (from the vendored `Lib/cairo-sharp.dll`), which loads a `.ttf` **directly** through the
  bundled `freetype6` native lib — bypassing `SelectFontFace`'s name-based OS/fontconfig
  resolution entirely — and cache the resulting `FontFace` once for the client session.
- Apply that face to **only** the lectern's row text, at the existing draw seam in
  `ScribeRowElement.ComposeElements` (which bakes text onto the row's *own* `ImageSurface`/
  `Context`), by calling `ctx.SetContextFontFace(cachedFace)` immediately after
  `font.SetupContext(ctx)` and before `AutobreakAndDrawMultilineTextAt`. Because the row bakes
  onto its own surface, this is inherently mod-scoped — no other GUI text in the game is
  affected.
- Prove, by running the spike **on the author's Apple Silicon Mac**, the three make-or-break
  runtime unknowns the decompile left open: (1) setup-then-face-override ordering preserves the
  font size, (2) whether a released mod's packed-`.zip` asset must be extracted to a temp file
  for FreeType to read it, and (3) that the `freetype6` P/Invoke path renders on arm64 macOS.
- Dispose the cached `FontFace` on client shutdown.
- Ship the font's `OFL.txt` and credit Caudex in a `CREDITS` file (license gate).
- Correct two stale facts in the design docs discovered during the decompile (documentation-only,
  no code): the type is `Cairo.Util.FreeTypeFontFace` (docs wrote `Cairo.FreeTypeFontFace`,
  missing the `.Util` segment), and `GuiStyle.StandardFontName` resolves to `"sans-serif"` at
  runtime (a stale XML doc-comment claimed "Montserrat").

Explicit **non-goals** (this is a spike, not the font system):

- **No** per-tier faces — no cuneiform tablet face, no handwritten-notebook face. One face, one
  surface, to prove the seam.
- **No** global-swap route (OS install + `defaultFontName`) — that path is game-wide and
  deliberately rejected.
- **No** `src/Core/` changes, no networking, no persistence/sync, no codec bump. This is
  Mod-layer client rendering only.
- Does **not** replace the stroke-glyph path (`docs/specs/glyph-strokes-ingestion.md`), which is
  a *different* approach aimed at the tablet's stamped glyphs; this font path is the serif /
  body-text path and the two coexist.

## Capabilities

### New Capabilities

- `bundled-font-rendering`: A mod-scoped mechanism for rendering Scribe's own baked GUI text in a
  bundled TTF loaded directly through FreeType, proven end-to-end on one surface (the lectern
  row text) with one face (Caudex), including the license-bundling requirement.

### Modified Capabilities

<!-- None. No existing spec's requirements change; the lectern row-text behavior gains a
     rendering detail but no requirement in lectern-gui-shell is altered. -->

## Impact

- **New asset:** one bundled `.ttf` (Caudex) plus its `OFL.txt` under the mod's assets, and a new
  `CREDITS` file at the repo root.
- **Touched code (Mod layer only):** `src/Mod/ScribeRowElement.cs` (the `ComposeElements` draw
  seam) and a small client-init font-cache holder; the `RowFont()` site in
  `src/Mod/GuiDialogScribeLectern.cs` is referenced but its `CairoFont` (size/color) contract is
  unchanged.
- **No** `src/Core/` impact, no network/persistence surface, no new package or mod dependency
  (uses only the already-vendored `Lib/cairo-sharp.dll` + bundled `freetype6`).
- **CI unaffected:** cloud runners build/test `Core` only; this is Mod-layer client render proven
  by manual playtest on the author's Mac.
- **Platform risk:** the `freetype6` interop must be verified on arm64 macOS — a prior
  VSImGui arm64 issue was in a *different* subsystem (the ImGui overlay, not Cairo), so it does
  not predict this path, but it is why the spike must run on the Mac rather than being assumed.
- **Docs corrected:** `docs/specs/presentation-and-fonts.md` and the stale `StandardFontName`
  XML doc-comment.
