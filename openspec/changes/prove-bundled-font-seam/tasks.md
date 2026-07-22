## 1. Bundle the font asset and clear its license

- [ ] 1.1 Add the Caudex `.ttf` (a humanist serif, SIL OFL 1.1) under the mod's assets (e.g. `src/Mod/assets/scribe/fonts/`), verifying it is a redistributable release intended for bundling.
- [ ] 1.2 Ship Caudex's `OFL.txt` alongside the `.ttf` in the packaged assets (do not rename the font files — they are unmodified for this spike).
- [ ] 1.3 Create a `CREDITS` file at the repo root (or add to it) crediting Caudex and its SIL OFL 1.1 license.

## 2. Load and cache the FreeType face at client init

- [ ] 2.1 Add a minimal client-side font-cache holder (a single cached `FontFace` field — NOT the full `ScribeFontRegistry`) created in `StartClientSide`.
- [ ] 2.2 Resolve the bundled `.ttf` to a real filesystem path: read the asset bytes and write them to a temp file, then call `Cairo.Util.FreeTypeFontFace.Create(tempPath, loadoptions)`; cache the returned `FontFace`.
- [ ] 2.3 Dispose the cached `FontFace` on client shutdown (it implements `Dispose`); ensure no per-row/per-frame `Create` call exists.

## 3. Apply the face at the lectern row-text draw seam

- [ ] 3.1 In `ScribeRowElement.ComposeElements`, after `font.SetupContext(ctx)` and before `api.Gui.Text.AutobreakAndDrawMultilineTextAt(...)`, call `ctx.SetContextFontFace(cachedFace)` on the row's own private `Context`.
- [ ] 3.2 Wire the cached face from the holder through to `ScribeRowElement` without disturbing the `RowFont()` `CairoFont` size/color/layout contract in `GuiDialogScribeLectern.cs`.

## 4. Prove the runtime unknowns on the author's Apple Silicon Mac

- [ ] 4.1 (Unknown #3 — arm64 interop) Build and run the spike ON THE MAC; open the lectern and confirm the row text renders in Caudex with no crash, native-interop error, or garbled glyphs.
- [ ] 4.2 (Unknown #1 — size survival) Verify the row text renders at the configured (text-size-scaled) size after the face override; if the face swap resets the size, re-apply `ctx.SetFontSize(scaled(size))` after the override and record which was needed.
- [ ] 4.3 (Unknown #2 — packed-zip path) Confirm whether the unpacked dev asset can be loaded directly vs. whether the temp-file extraction is required; test against a packed `.zip` build and record the answer.
- [ ] 4.4 Confirm mod-scoping: with the lectern open, other in-game GUI text (menus, tooltips, other dialogs) is unchanged — only the lectern row text uses Caudex.

## 5. Record findings and correct the docs

- [ ] 5.1 Record the three runtime-unknown outcomes (size survival, packed-zip path, arm64 interop) in `VSAPI-NOTES.md` under the existing "Custom TTF fonts in the GUI" note so the parent font work does not re-derive them.
- [ ] 5.2 Correct `docs/specs/presentation-and-fonts.md`: the type is `Cairo.Util.FreeTypeFontFace` (docs wrote `Cairo.FreeTypeFontFace`, missing the `.Util` segment).
- [ ] 5.3 Correct the stale XML doc-comment that claims `GuiStyle.StandardFontName` is "Montserrat"; at runtime it resolves to `"sans-serif"`.
