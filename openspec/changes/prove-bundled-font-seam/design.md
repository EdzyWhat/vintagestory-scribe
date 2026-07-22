## Context

`docs/specs/presentation-and-fonts.md` Item 3 ("custom fonts per tier") depends on being able to
render Scribe's own GUI text in a bundled typeface without touching any other GUI in the game.
A decompile of `VintagestoryAPI.dll`, `VintagestoryLib.dll`, and the vendored `Lib/cairo-sharp.dll`
(2026-07-21; see `VSAPI-NOTES.md` "Custom TTF fonts in the GUI") established the mechanism but
also surfaced three runtime behaviors that a static read of the DLLs cannot settle. This spike
exists solely to de-risk those before a real tier commits to a font system.

**What the decompile confirmed (treat as ground truth):**

- `CairoFont.SetupContext(ctx)` resolves fonts by **name only**, via
  `ctx.SelectFontFace(Fontname, Slant, FontWeight)` → Pango/fontconfig → OS/registry. A full grep
  of both game DLLs found **no** managed font-registration API, so a mod cannot bind a bundled
  `.ttf` to a family name and select it by name cross-platform.
- `Cairo.Util.FreeTypeFontFace.Create(string ttfPath, int loadoptions)` (in `Lib/cairo-sharp.dll`)
  loads a `.ttf` **directly** through the bundled `freetype6` native lib via P/Invoke — bypassing
  fontconfig/OS entirely — and returns a Cairo `FontFace`. `Cairo.Context.SetContextFontFace(FontFace)`
  then makes that context draw with it, bypassing `SelectFontFace`'s name resolution.
  - **Note the exact type is `Cairo.Util.FreeTypeFontFace`.** `docs/specs/presentation-and-fonts.md`
    wrote `Cairo.FreeTypeFontFace` (missing the `.Util` segment); this change corrects that doc.
- `ScribeRowElement.ComposeElements` (`src/Mod/ScribeRowElement.cs`) already bakes its text onto
  its **own** `ImageSurface`/`Context` (created locally at lines ~135-136, deliberately *not* the
  shared static surface), then `font.SetupContext(ctx)` and
  `api.Gui.Text.AutobreakAndDrawMultilineTextAt(ctx, font, text, …)` (lines ~154-156). Calling
  `ctx.SetContextFontFace(ourFace)` on that private context therefore affects **only** the mod's
  row text — the mod-scoping is inherent to where the seam sits, not a separate mechanism.
- FreeType-direct is uniform across Win/Linux/Mac because `freetype6` ships with the game, unlike
  the OS-install + `clientsettings.json defaultFontName` route (which is game-wide anyway).

**The chosen face:** Caudex — a humanist serif under the SIL Open Font License 1.1, chosen only
because it is unambiguously redistributable inside a mod `.zip` and legible as body text. The
spike proves the *seam*, not the final aesthetic.

## Goals / Non-Goals

**Goals:**

- Prove a bundled `.ttf` loads via `Cairo.Util.FreeTypeFontFace.Create` and renders through
  `SetContextFontFace` on the lectern row text, and on **no** other game GUI.
- Settle the three runtime unknowns (below) by running the spike on the author's Apple Silicon
  Mac.
- Establish the license-bundling discipline (ship `OFL.txt`, credit in `CREDITS`) that the parent
  font work will reuse.
- Leave the codebase in a clean state: one cached face loaded at client init, disposed at
  shutdown; a single-line override at the draw seam; easily reverted if a finding kills the path.

**Non-Goals:**

- Per-tier faces (cuneiform tablet, handwritten notebook) — the parent Item 3 vision, not this.
- The global-swap route (OS install + `defaultFontName`) — game-wide, not mod-scopable, rejected.
- Any `src/Core/` change, networking, persistence/sync, or codec bump — Mod-layer client render
  only.
- A `ScribeFontRegistry` abstraction, config toggle, or tier→face mapping — over-engineering for a
  one-face proof. A minimal cache holder suffices; the registry is designed in the parent work.
- Replacing the stroke-glyph path (`docs/specs/glyph-strokes-ingestion.md`) — that targets the
  tablet's stamped glyphs and is a different, coexisting approach.

## Decisions

### Decision 1 — Load via FreeType-direct, not name-based selection

Use `Cairo.Util.FreeTypeFontFace.Create(ttfPath, loadoptions)` + `ctx.SetContextFontFace(face)`.

**Why:** The decompile proved name-based `SelectFontFace` cannot resolve a bundled file
cross-platform (no font-registration API exists). FreeType-direct is the only mod-scopable path,
and it is uniform across platforms because `freetype6` is bundled.

**Alternative considered — global font swap** (OS font install + `clientsettings.json
defaultFontName`): rejected because it is game-wide (rewrites *every* GUI's font, not just
Scribe's), requires the player to install a font at the OS level, and is exactly what the community
guides describe and this proposal aims to avoid.

### Decision 2 — Apply the override at the existing `ComposeElements` seam only

Insert `ctx.SetContextFontFace(cachedFace)` in `ScribeRowElement.ComposeElements` immediately
**after** `font.SetupContext(ctx)` and **before** `AutobreakAndDrawMultilineTextAt`, on the row's
own private `Context`.

**Why:** That context is already private to the row (baked to its own surface for clip
correctness). The scoping is free — no other surface is touched. The `RowFont()` `CairoFont` in
`GuiDialogScribeLectern.cs` still owns size/color/layout; only the face is overridden.

### Decision 3 — Ordering: SetupContext first, face override last, verify size survives

`SetupContext` calls `SelectFontFace`, which would clobber a face set earlier — so the face
override must come **last**. The open risk is that `SetContextFontFace` sets the face but **not**
the size, so the size `SetupContext` applied may or may not survive the face swap.

**Approach:** apply `SetupContext` first (size/color/matrix), then `SetContextFontFace` last; then
**visually verify the size is correct**. If the glyphs render at the wrong size, re-apply
`ctx.SetFontSize(scaled(fontSize))` after the face override and record which is needed. This is
runtime unknown #1 and is an explicit spike task — the design does not pre-decide the outcome.

### Decision 4 — Cache one face at client init; dispose at shutdown

Load the face exactly once (a single cached `FontFace` field on a client-side holder created in
`StartClientSide`), never per-row or per-frame. `FreeTypeFontFace` implements `Dispose`; free it on
client shutdown.

**Why:** FreeType face creation is not free; `ComposeElements` runs on every recompose. Per-row
creation would leak native handles and cost measurably. A minimal holder (not the full
`ScribeFontRegistry`) keeps the spike small while modeling the caching discipline the real system
needs.

### Decision 5 — Resolve the asset to a real filesystem path (extract from zip if needed)

`Create` needs a real filesystem path. In dev/unpacked builds the asset is already a real file; a
**released** mod is a `.zip`, so the `.ttf` may need extraction to a temp file first (read asset
bytes → write temp file → `Create(tempPath)`).

**Approach:** implement the read-bytes-to-temp-file path (it works for both packed and unpacked)
**and confirm** during the spike whether the unpacked dev path can be loaded directly. This is
runtime unknown #2 — record which is actually required so the parent work does not re-derive it.

### Decision 6 — License gate is part of "done"

Ship Caudex's `OFL.txt` alongside the `.ttf` and credit Caudex in a `CREDITS` file. SIL OFL 1.1
permits bundling/redistribution inside the mod `.zip`; do not rename the font files if modified
(they are not being modified here). The spike is not complete until the license artifacts are in
place — this bakes the discipline in before the parent font work adds more faces.

## Risks / Trade-offs

- **[arm64 macOS interop fails]** → The whole point of running on the Mac. A prior VSImGui failure
  was arm64-specific but in a *different* subsystem (the ImGui overlay, not Cairo), so it does not
  predict this path — but it is why we verify rather than assume. If `freetype6` P/Invoke or the FT
  face draw fails on arm64, the finding kills (or reshapes) the parent font path; the spike is
  cheap precisely so we learn this early. Mitigation: the change is a single seam + a cached field,
  trivially revertible.
- **[Size clobbered by face swap]** → Decision 3: verify visually, re-apply `SetFontSize` after the
  override if needed, and record the answer.
- **[Packed-zip path can't be read directly]** → Decision 5: implement extract-to-temp, which works
  regardless, and confirm which case applies.
- **[Native handle leak]** → Decision 4: one cached face, disposed at shutdown; never per-row.
- **[Scope creep into the full font system]** → Non-Goals fence it: one face, one surface, no
  registry, no config, no tier mapping.

## Migration Plan

Not applicable — no data model, persistence, or wire-format change. Deploy is a local dev build run
on the author's Mac; "rollback" is reverting the single seam edit and the cached-field holder and
removing the bundled asset. No release ships from this spike unless the findings say the path is
sound.

## Open Questions

The three runtime unknowns below are the spike's reason to exist — they are resolved **by running
it**, not on paper:

1. **Size survival across the face swap** — does the `SetupContext`-then-`SetContextFontFace`
   ordering preserve font size, or is a post-override `SetFontSize(scaled(size))` required?
2. **Packed-zip path** — must the `.ttf` be extracted to a temp file for `Create`, or can the
   unpacked dev asset path be loaded directly?
3. **Apple Silicon interop** — does the `freetype6` P/Invoke + FT face draw actually render on
   arm64 macOS?

Deferred to the parent font work (`docs/specs/presentation-and-fonts.md` Item 3), explicitly **not**
this spike: the `ScribeFontRegistry` shape, per-tier face selection, a client config toggle, and
the sourcing of the actual production faces (rustic script, cuneiform).
