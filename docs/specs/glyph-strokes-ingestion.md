# Glyph-strokes ingestion — vendoring & rendering glyph-forge letters

> Exploration/design spec (2026-07-21). NOT an OpenSpec change, NOT implemented code. When a
> piece of this is picked up, this file becomes the input to a real `openspec-propose`.
> See `docs/specs/README.md` for the shared structure and guardrails.
>
> **Companion doc:** `docs/specs/glyph_forge_feasibility.md` (the two-repo feasibility matrix).
> This spec is the *implementation plan* for the path that matrix recommended (Strategy 1:
> Cairo 2D → `LoadedTexture`). Read the matrix first for the format survey and the
> strategy trade-offs; this file assumes them.

## Summary

`glyph-forge` (a sibling repo under `~/claude/`) is a browser tool that constructs display-font
letters from **ordered straight-line stroke primitives** and exports each glyph as a
self-contained JSON file (`glyph-<slug>.json`). This spec covers how Scribe **ingests that data
and renders those glyphs** — the groundwork for the cuneiform-style stamped text on the v3 clay
tablet (and, later, the stamp-by-stamp carving animation), an alternative to the bundled-TTF
font path in `presentation-and-fonts.md` Item 3.

It merges three decisions already settled with the user (2026-07-21):

1. **Ingestion = vendored assets, not a runtime dependency.** A repeatable `tools/` sync script
   copies the *authoritative per-glyph* files from glyph-forge into `assets/scribe/…`. glyph-forge
   stays the single source of truth and the only place glyphs are edited; the vendored copies are
   regenerable, never hand-edited. The localhost save server is a dev aid, never called at runtime.
2. **Layout computed in the mod.** Scribe reads each glyph's metrics (`leftWidth`/`rightWidth`/
   padding/`kerning`) and lays out arbitrary in-game text itself, rather than consuming
   glyph-forge's pre-composed `composed-line.json` (which only covers strings composed *in*
   glyph-forge — no good for user-typed tablet text).
3. **Built against a hardened contract.** A parallel glyph-forge change (`harden-export-contract`)
   adds a `formatVersion` field and a canonical slug source, so Scribe's importer branches on an
   explicit version instead of sniffing for `leftWidth`, and mirrors one authoritative slug table.

**Core/Mod split is the load-bearing structure here.** Everything that is pure data — parse,
stroke→quad geometry, line layout — lives in `src/Core/` (no VS API, fully unit-testable without
the game). Only the Cairo draw + `LoadedTexture` lifecycle lives in `src/Mod/`. This keeps the
risky, hard-to-test surface tiny and mirrors glyph-forge's own "`render.js` is the single source
of truth for stroke rendering" guardrail with one equivalent C# geometry function.

**This spec adds a new Core surface but does NOT touch the shared Core conventions** in
`README.md` (§1–6): no `ScribeDocumentCodec.Version` bump, no access-policy/timestamp/`docId`
changes. Glyph geometry is orthogonal to the document model — a tablet still stores its text as
today; this spec only changes how that text is *drawn*.

---

## VS API hooks

Confirmed against `src/Mod` code already in the repo, `VSAPI-NOTES.md`, and the feasibility
matrix's cross-repo analysis (2026-07-21). The rendering half reuses the exact pattern
`ScribeRowElement` already runs — this is the primary reason Strategy 1 was chosen over a PNG
atlas or voxel mesh (see `glyph_forge_feasibility.md` §2).

### Cairo drawing onto an owned surface (the proven pattern)

- **`Cairo.Context` on a private `ImageSurface`** — `ScribeRowElement.ComposeElements`
  (`src/Mod/ScribeRowElement.cs:106-141`) already creates `new ImageSurface(Format.Argb32, w, h)`,
  draws vector primitives (`DrawRuling` uses `MoveTo`/`LineTo`/`Stroke`; `DrawCheckboxGlyph` uses
  `Arc`/`ClosePath`), and uploads via the `GuiElement.generateTexture(surface, ref tex)` helper.
  A glyph's strokes draw the same way: each stroke → four `LineTo`s of a filled quad → `Fill()`.
  `cairo-sharp.dll` is already a referenced dependency (`src/Mod/Mod.csproj`).
- **`api.Render.Render2DTexturePremultipliedAlpha(texId, x, y, w, h)`** —
  `ScribeRowElement.RenderInteractiveElements` (`src/Mod/ScribeRowElement.cs:210-220`) already
  uses this to blit a baked `LoadedTexture`. The GUI-side glyph render path is identical.
- **Two-render-pass discipline** (`VSAPI-NOTES.md`; `ScribeRowElement.cs:9-27`) — bake the glyph
  texture in the **static/compose pass** (only when the text changes), blit every frame in the
  **interactive pass**. Never run Cairo in the per-frame loop. Same rule the whole GUI obeys.
- **`LoadedTexture` lifecycle** — `new LoadedTexture(capi)`, uploaded once, `Dispose()`d in the
  element's `Dispose()` (`ScribeRowElement.cs:44,67,256-260`). Every glyph/line texture,
  `ImageSurface`, and `Context` must be disposed — see Performance risks in the feasibility matrix.

### In-world rendering (NEW territory — phase 2)

The feasibility matrix flags this as the one piece with **no codebase precedent** (grep confirms
zero `MeshData`/`tesselator`/`ITexPositionSource`/`BlockEntityRenderer`/`IRenderer` usage in
`src/`). Two candidate hooks, unproven here, to prototype when phase 2 is scoped:

- **`IRenderer` attached to the block entity** (via `capi.Event.RegisterRenderer` /
  `BlockEntity`-owned renderer) that blits the baked glyph texture onto a world-space quad on the
  tablet face in `OnRenderFrame`. Reuses the Cairo→texture half that phase 1 proves.
- **`ITexPositionSource`** feeding the baked texture into the tesselator so the tablet's own model
  face samples it. More engine-idiomatic but needs the atlas/tesselation research the mod has
  never done.

Phase 1 (GUI dialog) needs **none** of this — it is pure reuse of `ScribeRowElement`'s path.

### Asset loading (vendored glyph JSON)

- **`api.Assets.TryGet(AssetLocation)` / `IAsset.ToText()`** — load a vendored
  `assets/scribe/config/glyphs/glyph-<slug>.json` at runtime. Mod domain is `scribe`
  (confirmed `modinfo.json`). `Newtonsoft.Json` is already referenced (`Mod.csproj`) for parsing;
  Core parsing should use a game-agnostic JSON reader (see C# structures).
- **No `HttpClient` at runtime.** The glyph-forge save server (`localhost:8791`) is dev-loop only
  and is not present on shipped clients/servers.

---

## C# data structures

**`src/Core/` (game-agnostic, NO VS API) — new `Scribe.Core.Glyphs` namespace.** All of this is
pure data and math, unit-testable in `tests/Core.Tests` without booting the game.

```csharp
// Mirrors glyph-forge EXPORT-FORMAT.md. Coordinates are on a gridSize×gridSize em-grid,
// origin TOP-LEFT, y increases DOWNWARD (HTML-canvas convention — matches Cairo, so the
// GUI path needs no y-flip; a future bottom-up mesh path flips: y' = gridSize - y).
public readonly record struct GlyphPoint(double X, double Y);

public sealed record GlyphStroke(GlyphPoint Start, GlyphPoint End, double Weight);

public sealed record Glyph(
    string Character,
    int    FormatVersion,          // from the hardened contract; 0 == legacy/unversioned
    double GridSize,               // 100 in practice
    double LeftWidth, double RightWidth,     // footprint measured from grid center (GridSize/2)
    double LeftPadding, double RightPadding, // min inter-glyph clearance (kerning floor; may be <0)
    IReadOnlyList<GlyphStroke> Strokes,      // ORDER IS LOAD-BEARING — never sort/dedupe/reorder
    IReadOnlyDictionary<string,double> Kerning);  // sparse: followingSlug -> adjustment
```

```csharp
// The ONE C# source of truth for stroke geometry — mirrors glyph-forge render.js strokeCorners
// (EXPORT-FORMAT.md "Computing a stroke's rectangle corners"). No other file may re-derive this.
public static class GlyphGeometry
{
    // Four corners of the filled quad for a stroke, in winding order
    // start+perp, end+perp, end-perp, start-perp. Square-cut ends, no curves.
    public static (GlyphPoint,GlyphPoint,GlyphPoint,GlyphPoint) StrokeCorners(GlyphStroke s);
}
```

```csharp
// Layout of arbitrary text into placed glyph instances — decision (2): compute in the mod.
// Advance width = LeftWidth + RightWidth; gap between neighbors >= sum of adjoining paddings,
// widened (never narrowed past the floor) by the left glyph's Kerning[rightSlug]. Handles
// wrapping to a max line width. Space = a valid instance with no strokes.
public sealed record GlyphInstance(char Character, double X, double Y, int Line);
public static class GlyphLayout
{
    public static IReadOnlyList<GlyphInstance> LayoutText(
        string text, IReadOnlyDictionary<char,Glyph> glyphs, double maxLineWidth, double lineHeight);
}
```

```csharp
// Canonical slug map — mirrors glyph-forge's ONE canonical source (harden-export-contract).
// Letters/digits -> self; punctuation -> descriptive name. Keep values identical; when the
// hardened contract lands, cite characters.json as the upstream source of truth.
public static class GlyphSlug { public static string For(char c); }
```

```csharp
// Parser: JSON text -> Glyph. Branches on formatVersion (decision 3). Rejects/flags unknown
// future versions loudly rather than silently mis-reading; treats missing version as 0/legacy
// and applies the same footprint migration glyph-forge documents (leftWidth==rightWidth==width/2,
// etc.) so a stale vendored file degrades predictably instead of throwing.
public static class GlyphParser { public static bool TryParse(string json, out Glyph glyph, out string error); }
```

**`src/Mod/` (client-only) — Cairo render + texture cache.**

```csharp
// Loads vendored glyph JSON via api.Assets, parses with Core GlyphParser, caches Glyph by char.
// Renders a Glyph (or a laid-out line) to a LoadedTexture with Cairo, caching per-character
// textures to cap VRAM (feasibility matrix §4.2). Owns disposal of every texture/surface/context.
sealed class ScribeGlyphRenderer /* : IDisposable */
{
    LoadedTexture RenderGlyph(Glyph g, int sizePx);          // draws each stroke as a filled quad
    LoadedTexture RenderLine(IReadOnlyList<GlyphInstance> line, ...);
    // Cache invalidated on text change only; regenerate-on-change, never per frame.
}
```

No `src/Core/` collision with README §1–6 conventions: no codec version, no `ScribeAccessPolicy`,
no `ChronicleStamp`, no `docId` change. Glyph data is a **separate, additive** Core namespace.

---

## Implementation spec

### Step 0 — land the hardened glyph-forge contract first

The `harden-export-contract` change in glyph-forge (formatVersion + canonical slug source) is a
soft prerequisite: Scribe *can* build against today's unversioned files by treating them as
version 0, but the importer is cleaner and future-proof if `formatVersion` exists. Sequence the
glyph-forge change first; build Scribe's parser to accept both (missing == 0).

### Step 1 — the vendor/sync tool (`tools/`, no runtime coupling)

A small script (PowerShell or `dotnet`-run C#, matching the repo's existing `tools/` conventions)
that:

1. Reads the **authoritative per-glyph** files from a configured glyph-forge checkout
   (`glyph-forge/glyphs/glyph-*.json`) — **never** `glyphs-1.json` (the stale bundle) and **never**
   the localhost server.
2. **Validates on copy:** requires the current shape (`leftWidth`/`rightWidth` present) and a
   matching `formatVersion`; fails loud on a legacy/stale file so a pre-metrics glyph can never
   silently ship. Refuses to overwrite with a file that fails validation.
3. Writes into `assets/scribe/config/glyphs/glyph-<slug>.json` and stamps provenance (source
   commit hash) in a sidecar or header comment.
4. Offers a `--verify` mode that diffs vendored assets against the glyph-forge source and reports
   drift (the guard against silent staleness), so CI or a pre-release check can catch a stale set.

**Discipline:** vendored files are generated artifacts — never hand-edit them; edit in glyph-forge
and re-sync. Because glyph spacing/kerning isn't final yet, run the tooling now against a dev
snapshot but hold the *final* vendor freeze until metrics are locked. Nothing downstream needs
final numbers to be built and tested.

### Step 2 — Core: parse + geometry + layout (all unit-tested, no game)

1. `GlyphParser.TryParse` — JSON → `Glyph`, version-branching per Step 0. Test: parse the real
   `glyph-A.json`, assert 3 strokes in order, correct metrics.
2. `GlyphGeometry.StrokeCorners` — the one geometry function. Test: assert the four corners of a
   known stroke match the glyph-forge `strokeCorners` output (port a fixture from `render.js`).
   This is the "render identically to glyph-forge" guarantee, unit-pinned.
3. `GlyphLayout.LayoutText` — advance/kerning/padding/wrap. Test: a two-glyph run's second-glyph X
   equals `leftWidth+rightWidth` of the first plus resolved gap; a kerning pair narrows the gap
   only to the padding floor; a space produces a stroke-less instance; wrapping breaks at
   `maxLineWidth`.
4. `GlyphSlug.For` — mirror the canonical table; test every punctuation char maps to its slug.

Encapsulate the **y-axis convention** in Core comments and (if a mesh path arrives) a single flip
helper — one place decides y-down (Cairo/GUI, no flip) vs. y-up (future mesh, `gridSize - y`).

### Step 3 — Mod phase 1: render in the GUI dialog (pure reuse)

1. `ScribeGlyphRenderer` loads + caches glyphs from `api.Assets`, parses via Core.
2. For a glyph/line, create an `ImageSurface`, set the ink color, loop strokes **in array order**,
   draw each as a filled quad from `GlyphGeometry.StrokeCorners` scaled by `pxPerUnit = sizePx /
   gridSize`, `generateTexture` → `LoadedTexture`.
3. Blit in `RenderInteractiveElements` via `Render2DTexturePremultipliedAlpha`, inside the active
   clip. Cache per-character textures; regenerate only on text change; dispose on element dispose.
4. First target: render the tablet's stored text in the read/edit dialog. Zero new render
   primitives — direct `ScribeRowElement` pattern reuse. Ships visible value immediately.

### Step 4 — Mod phase 2 (LATER): render on the block/item in the world

The new-territory piece. Prototype an `IRenderer`/`ITexPositionSource` that draws the baked glyph
texture onto the tablet face (see VS API hooks). Contained, well-understood risk once phase 1
proves the texture-generation half. Defer until phase 1 ships.

### Step 5 (FUTURE) — carving animation

glyph-forge's ordered `strokes` (and the compositor's flattened `strokeSequence`) map directly
onto a stamp-by-stamp reveal: draw N strokes per tick in the interactive pass. This is the
original motivation for the whole export format. Ties to the `presentation-and-fonts.md` S4
animation surface; spec when the tablet's stamping mechanic is scoped.

---

## Dependencies & sequencing

- **glyph-forge `harden-export-contract`** — sequence first (soft prereq; Scribe tolerates
  version 0 either way). Gives the canonical slug source Scribe's `GlyphSlug` mirrors and the
  `formatVersion` its parser branches on.
- **v3 clay tablet (`v3-clay-tablet.md`)** — the primary consumer. This spec is the *rendering*
  half of the tablet's "cuneiform-style stamped text"; `v3` owns the item/docId/editor. Glyph
  rendering can be built and proven in the **v1 lectern** GUI first (render its body text via
  glyphs) to de-risk the path before a tier depends on it — same de-risking approach
  `presentation-and-fonts.md` proposes for the FreeType font path.
- **Relationship to `presentation-and-fonts.md` Item 3 (bundled TTF fonts)** — these are
  **two alternative routes to "themed letters."** The TTF path swaps the Cairo *font face*; this
  path draws letters from *stroke primitives*. Stroke rendering is the better fit for the clay
  tablet specifically, because (a) it needs no font-license clearance (the hard gate on Item 3),
  and (b) only stroke data can drive the stamp-by-stamp carving animation (a TTF face cannot). The
  TTF path likely still wins for the notebook's flowing script (v2). Decide per-tier; they can
  coexist. Cross-reference both specs when either is proposed.
- **No new hard dependencies.** Vanilla `VintagestoryAPI` + already-vendored Cairo +
  already-referenced Newtonsoft.Json. No Core convention (README §1–6) touched: no codec bump, no
  access/timestamp/docId change. Phase 1 adds **no** persistence/sync surface (it renders
  already-synced text). Phase 2's in-world renderer is the only genuinely new API surface.
- **Testing:** Core parse/geometry/layout are unit-tested in `tests/Core.Tests` (no game). Mod
  rendering is manual-playtest coverage (client-visual, like the rest of the render cluster).

---

## Open questions

1. **Sync-tool form & trigger.** PowerShell vs. `dotnet run` C# console (match existing `tools/`);
   run manually, on pre-release, or wired into a build target? And does `--verify` run in CI, or
   is it a manual pre-freeze check?
2. **When to freeze the vendored set.** Spacing/kerning in glyph-forge isn't final. Confirmed
   approach: build all plumbing now against a dev snapshot, freeze the final vendor after metrics
   lock. Is there a target milestone for that lock, or does it ride with v3 scoping?
3. **Per-character vs. per-line texture caching.** Cache one texture per glyph (small fixed set,
   compose lines by blitting glyphs at laid-out positions) vs. one texture per rendered string
   (fewer draws, more textures). Feasibility matrix §4.2 leans per-character to cap VRAM; confirm
   under real tablet-density playtest.
4. **In-world render hook (phase 2).** `IRenderer`-on-block-entity vs. `ITexPositionSource` into
   the tesselator — needs a prototype/decompile pass; no precedent in the repo. Which to try first?
5. **Stroke vs. TTF for each tier.** This spec argues stroke-rendering for the clay tablet (no
   license gate, enables carving animation) and leaves the notebook to the TTF path in
   `presentation-and-fonts.md`. Confirm that per-tier split, or unify on one mechanism?
6. **Character-set coverage.** glyph-forge captures uppercase A–Z, 0–9, and a fixed punctuation
   set (no lowercase, `characters.js`). Confirm the tablet's text input is constrained to (or
   gracefully degrades outside) that set — a missing glyph should render a blank/placeholder, not
   throw (feasibility matrix §4.1 version-skew note).
