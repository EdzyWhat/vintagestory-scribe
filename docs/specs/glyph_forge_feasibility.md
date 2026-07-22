# Feasibility: rendering glyph-forge characters on clay tablets in vintagestory-scribe

**Status:** Research only — no code written. Analysis of two sibling repos under `~/claude`:
`glyph-forge` (browser stroke-glyph editor) and `vintagestory-scribe` (this mod).

**Date:** 2026-07-21 · **VS API target:** 1.22.x / .NET 10 (`modinfo.json:12`, `Directory.Build.props:5`)

**TL;DR:** Feasible and low-risk. glyph-forge emits **stroke-primitive JSON** (no TTF/SVG — those
are explicitly out of scope). The mod's existing `ScribeRowElement` already proves the winning
path: draw strokes with **Cairo onto a private `ImageSurface`, upload to a `LoadedTexture`, blit in
the interactive pass**. Recommended approach is **Strategy 1 (Cairo 2D → LoadedTexture)**. Strategy 2
(PNG atlas) fights glyph-forge's actual output and the ordered-stroke requirement; Strategy 3 (voxel
carving) is high-effort with no codebase precedent and should be deferred.

---

## 0. Correcting the brief's premise

The task framing assumes glyph-forge exports "TTF, SVG, PNG, JSON." That is **not** what the tool
produces. Confirmed by full-source grep (zero `fontTools`, `.ttf`, `.otf`, `<svg>`, `createElementNS`):

| Format | Exists? | Notes |
|---|---|---|
| **JSON (per-glyph)** | ✅ **Primary** | `glyphs/glyph-<slug>.json`, written by `GlyphStore.toExportJson` (`glyphStore.js:286-303`). The real contract. |
| **JSON (bundle)** | ⚠️ Convenience | `glyphs-1.json` via `tools/build_glyphs_bundle.py`. Manually regenerated, **can be stale** (its `A` lacks the metrics fields the per-glyph file has). glyph-forge's own CLAUDE.md forbids the compositor from reading it. |
| **JSON (composed line)** | ✅ | `composed-line.json` from the compositor — pre-resolved `(x,y)` per character + flattened `strokeSequence`. Ideal for multi-character lines. |
| **PNG** | ⚠️ Thumbnail only | `renderGlyphToPngDataUrl`, hardcoded 100×100 (`editor.js:638-651`). No metrics/order survive. Not a data-interchange path. |
| **TTF / OTF** | ❌ **Out of scope** | glyph-forge CLAUDE.md: "No font-file export yet … unresolved spike." Do not architect around this. |
| **SVG** | ❌ Does not exist | Not even a promised deliverable. |

**Consequence for the mod:** the integration is *geometry ingestion*, not *font loading*. This is
actually favorable — it sidesteps the unresolved cross-platform FreeType/TTF-loading spike noted in
`VSAPI-NOTES.md:557-568`.

---

## 1. Data pipeline

### 1.1 The glyph-forge glyph schema (authoritative)

Real file `glyphs/glyph-A.json`:

```json
{
  "character": "A",
  "gridSize": 100,
  "leftWidth": 22.25, "rightWidth": 24.45,
  "leftPadding": 6, "rightPadding": 6,
  "strokes": [
    { "start": {"x": 52.8, "y": 26.43}, "end": {"x": 31, "y": 76.63}, "weight": 6.5 },
    { "start": {"x": 72,   "y": 78.61}, "end": {"x": 49.2, "y": 23.01}, "weight": 6.5 },
    { "start": {"x": 34,   "y": 67.61}, "end": {"x": 68.4, "y": 57.21}, "weight": 6.5 }
  ]
}
```

Field semantics (from `EXPORT-FORMAT.md`):

- **`gridSize`** — abstract em-grid (always 100). Not pixels; scale by a chosen `pxPerUnit` at render.
- **Coordinates** — 0–100, origin **top-left, y increases downward** (HTML-canvas convention).
  A bottom-up consumer must flip: `y' = gridSize - y`.
- **`strokes`** — ordered `{start, end, weight}` centerline segments. **Order is load-bearing**
  (it drives a stamp-by-stamp carving sequence) — glyph-forge's guardrails forbid reorder/sort/dedupe
  anywhere in the pipeline. The mod must preserve array order verbatim.
- **`leftWidth`/`rightWidth`** — horizontal footprint measured from grid **center** (50), so box =
  `[50 - leftWidth, 50 + rightWidth]`.
- **`leftPadding`/`rightPadding`** — minimum inter-glyph clearance (kerning floor; may be negative).
- **`kerning`** *(optional)* — sparse `{followingSlug: adjustment}`, omitted when empty.
- No curves anywhere (guardrail): every "round" letter is a faceted polygon of straight strokes.

Stroke→quad math (`render.js:14-28`, `strokeCorners`) — replicate exactly in C#:

```
dx = end.x - start.x;  dy = end.y - start.y
len = hypot(dx, dy) || 1
hw  = weight / 2
px  = (-dy / len) * hw;  py = (dx / len) * hw
corners = [ (start.x+px, start.y+py), (end.x+px, end.y+py),
            (end.x-px,  end.y-py),  (start.x-px, start.y-py) ]   // square-cut ends
```

### 1.2 How the mod ingests it

The save server (`tools/save_server.py`, localhost:8791, `GET/POST /api/glyph/<slug>`) is a **dev
tool**, not a shipping API — don't have the mod call it at runtime. Two viable ingestion routes:

- **(Recommended) Vendor a snapshot as a mod asset.** Copy the per-glyph JSON (or a regenerated
  bundle) into `assets/scribe/config/glyphs/…` and load via `api.Assets.Get(AssetLocation)`.
  Deterministic, offline, multiplayer-safe, version-pinnable. A tiny build step or `tools/`
  script copies from glyph-forge → mod assets.
- **(Dev-loop only) HTTP fetch** from the save server for live iteration while designing.

**Slug mapping** (needed to resolve files): alphanumerics use the char; punctuation uses fixed slugs
(`period, comma, apostrophe, hyphen, colon, semicolon, exclaim, question, quote, lparen, rparen` —
`save_server.py:30-33`). The mod must mirror this table.

---

## 2. Rendering method evaluation

### Strategy 1 — Cairo 2D → dynamic `LoadedTexture` ✅ RECOMMENDED

Parse each glyph's strokes, draw each as a filled quad on a Cairo `ImageSurface`, upload to a
`LoadedTexture`, blit. **This is the pattern the mod already runs** in `ScribeRowElement`
(`ComposeElements` bakes a surface via `generateTexture(...)`; `RenderInteractiveElements` blits via
`api.Render.Render2DTexturePremultipliedAlpha`). Cairo (`cairo-sharp.dll`) is already a referenced dep.

- **Fit with glyph-forge output:** perfect — strokes → `ctx.MoveTo/LineTo/ClosePath/Fill`, order
  preserved naturally, no-curves honored, weight in grid-units scales cleanly.
- **Effort:** Low. Port `strokeCorners` (~10 lines) + a draw loop; reuse existing bake/blit plumbing.
- **Where it renders:** trivially inside a **GUI dialog** (identical to today). Rendering onto the
  **block/item in-world** requires either a `BlockEntityRenderer`/`IRenderer` that blits the texture
  onto a quad, or feeding the texture through an `ITexPositionSource` — **new territory** (no mesh/
  atlas/BE-renderer code exists in the mod today), but the texture *generation* half is solved.
- **Carving animation:** the ordered `strokeSequence` maps directly onto the mod's planned S4
  stamp-animation hook — draw N strokes per tick.
- **Risk:** must dispose surfaces/contexts/textures (mod already does); dynamic textures are
  per-client and un-atlased (see §4).

### Strategy 2 — Composite PNG onto a texture atlas ⚠️ NOT RECOMMENDED

Overlay pre-rendered glyph PNGs onto the tablet texture (e.g. via `ITexPositionSource` /
`GenerateAtlasSprites`).

- **Fit with glyph-forge output:** poor. glyph-forge's only PNG is a 100×100 preview thumbnail with
  **no metrics, no per-stroke order** — so partial/animated carving is impossible and kerning is lost.
  You'd have to build a PNG-rasterization pipeline that doesn't exist on either side.
- **Atlas mechanics:** the atlas is sized/generated at load; arbitrary user-authored strings can't be
  pre-baked. Per-tablet dynamic text = per-tablet atlas churn, which the atlas isn't meant for.
- **Only viable** if the glyph set were tiny, fixed, and non-animated — not the case here.
- **Verdict:** fights both glyph-forge's actual export and the ordered-stroke requirement. Skip.

### Strategy 3 — 3D voxel carving via generated `MeshData` ⚠️ DEFER

Generate depth geometry (recessed strokes) as custom `MeshData` for a genuine carved-clay look.

- **Fit with glyph-forge output:** good in principle — straight strokes → extruded/inset quads map
  cleanly to voxel/mesh geometry, and stroke order → progressive carving.
- **Effort:** High. **No precedent in the codebase** — zero `MeshData`, `tesselator`, `GenMesh`,
  `ITexPositionSource`, or BE-renderer usage anywhere. Requires learning VS tesselation, UV/normal
  generation, and chunk-mesh integration from scratch.
- **Perf:** per-tablet unique meshes defeat vanilla block-mesh batching; many carved tablets in a
  chunk inflate vertex counts.
- **Verdict:** most immersive, highest cost. Revisit only after Strategy 1 ships and if a true 3D
  carved appearance (vs. a drawn texture) is a hard requirement. The v3-clay-tablet spec already
  treats stamping as a *presentational* layer with no data-model change — consistent with deferring.

### Scorecard

| Criterion | S1 Cairo→Texture | S2 PNG Atlas | S3 Voxel Mesh |
|---|---|---|---|
| Matches glyph-forge export | ✅ strokes native | ❌ thumbnail only | ✅ strokes → geometry |
| Preserves stroke order / animation | ✅ | ❌ | ✅ |
| Codebase precedent | ✅ `ScribeRowElement` | ❌ none | ❌ none |
| Implementation effort | Low | Medium | High |
| In-world (non-GUI) rendering | Needs BE-renderer | Native-ish | Native |
| Multiplayer/VRAM risk | Medium (§4) | Low–Med | Med–High |
| **Recommendation** | **Adopt** | Reject | Defer |

---

## 3. Proof-of-concept C# (illustrative — not committed to code files)

> Sketches only, to demonstrate the API surface. Not wired into the mod. Namespaces/classes follow
> existing conventions (`Scribe` mod assembly; Cairo already referenced).

**3a. Load a glyph from a vendored asset (shipping path):**

```csharp
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

// slug mapping mirrors glyph-forge tools/save_server.py
static string SlugFor(char c) => c switch {
    '.' => "period", ',' => "comma", '\'' => "apostrophe", '-' => "hyphen",
    ':' => "colon",  ';' => "semicolon", '!' => "exclaim", '?' => "question",
    '"' => "quote",  '(' => "lparen", ')' => "rparen",
    _   => c.ToString()
};

JObject LoadGlyph(ICoreAPI api, char c) {
    var loc = new AssetLocation("scribe", $"config/glyphs/glyph-{SlugFor(c)}.json");
    IAsset asset = api.Assets.TryGet(loc);
    return asset == null ? null : JObject.Parse(asset.ToText());
}
```

**3b. Dev-loop fetch from the save server (NOT for shipping):**

```csharp
// Only for local iteration against a running `python3 tools/save_server.py`.
using var http = new System.Net.Http.HttpClient();
string json = await http.GetStringAsync($"http://localhost:8791/api/glyph/{SlugFor(c)}");
```

**3c. Render one glyph to a `LoadedTexture` via Cairo (mirrors `ScribeRowElement`):**

```csharp
using Cairo;
using Vintagestory.API.Client;

LoadedTexture RenderGlyph(ICoreClientAPI capi, JObject glyph, int sizePx) {
    double grid = (double)glyph["gridSize"];          // 100
    double pxPerUnit = sizePx / grid;

    using var surface = new ImageSurface(Format.Argb32, sizePx, sizePx);
    using var ctx = new Context(surface);
    ctx.SetSourceRGBA(0.10, 0.07, 0.05, 1.0);          // carved-clay ink

    foreach (var s in glyph["strokes"]) {              // preserve array order verbatim
        double sx = (double)s["start"]["x"], sy = (double)s["start"]["y"];
        double ex = (double)s["end"]["x"],   ey = (double)s["end"]["y"];
        double w  = (double?)s["weight"] ?? 0.8;

        double dx = ex - sx, dy = ey - sy;
        double len = Math.Max(Math.Sqrt(dx*dx + dy*dy), 1);
        double hw = w / 2;
        double px = (-dy / len) * hw, py = (dx / len) * hw;

        // NOTE: y is top-down in glyph-forge; matches Cairo, so no flip needed here.
        ctx.MoveTo((sx + px) * pxPerUnit, (sy + py) * pxPerUnit);
        ctx.LineTo((ex + px) * pxPerUnit, (ey + py) * pxPerUnit);
        ctx.LineTo((ex - px) * pxPerUnit, (ey - py) * pxPerUnit);
        ctx.LineTo((sx - px) * pxPerUnit, (sy - py) * pxPerUnit);
        ctx.ClosePath();
        ctx.Fill();                                    // each stroke = its own filled quad
    }

    var tex = new LoadedTexture(capi);
    capi.Gui.LoadOrUpdateCairoTexture(surface, true, ref tex);   // upload to GPU
    return tex;                                        // caller owns Dispose()
}
```

**3d. Blit in a GUI dialog's interactive pass (proven pattern):**

```csharp
public override void RenderInteractiveElements(float dt) {
    if (glyphTex.TextureId == 0) return;
    capi.Render.Render2DTexturePremultipliedAlpha(
        glyphTex.TextureId, Bounds.renderX, Bounds.renderY, Bounds.InnerWidth, Bounds.InnerHeight);
}
```

**3e. In-world on a block entity (new territory — sketch):**
A `BlockEntityScribeTablet` would either (a) hold the `LoadedTexture` and, from an attached
`IRenderer`, draw it onto a world-space quad in `OnRenderFrame`, or (b) expose the baked texture
through an `ITexPositionSource` so the tesselator maps it onto the tablet face. Both are unimplemented
today; (a) reuses the Cairo→texture half already validated by `ScribeRowElement`.

**3f. Item storage** mirrors the existing lectern (`BlockEntityScribeLectern`): store the *text/docId*
(not the rendered pixels) in `ItemStack.Attributes` / block-entity `TreeAttributes`, and regenerate the
texture client-side from that data — same server-authoritative Sign pattern the mod already uses.

---

## 4. Performance & network risks

### 4.1 Multiplayer sync

- **Sync the source text, never the texture.** Follow the mod's established Sign pattern: the
  authoritative text/docId lives server-side in a `TreeAttribute` (`BlockEntityScribeLectern` stores
  `scribeDocument` bytes) and syncs via `MarkDirty(redrawOnClient:true)` + the `scribe` protobuf
  channel. Each client renders its own texture locally from the synced text. Glyph geometry itself
  never travels the wire — it's a static asset shipped with the mod.
- **Determinism:** because every client ships the same vendored glyph JSON, all clients render the
  same result from the same text. Avoid depending on the localhost save server at runtime (it isn't
  present on clients/servers).
- **Bottleneck to watch:** the mod already throttles autosave to ~1/sec and is single-editor-locked.
  Keep tablet edits on the same throttled/locked path — don't push a packet per keystroke or per
  rendered frame.
- **Version skew:** if a server ships a newer glyph set than a client (or vice-versa), a missing slug
  should degrade gracefully (blank/placeholder), not throw. Pin the glyph-asset version in `modinfo`.

### 4.2 VRAM / GC

- **Dynamic textures are per-client GPU allocations** and are **not atlased**. One `LoadedTexture` per
  visible tablet (or per glyph, if cached per-character) consumes VRAM until disposed.
- **Leak risk:** every `LoadedTexture`, `ImageSurface`, and `Context` must be `Dispose()`d. The mod
  already does this correctly in `ScribeRowElement.Dispose()` — the tablet renderer must follow suit,
  especially on block-entity unload / chunk unload / dialog close. A forgotten dispose leaks VRAM per
  tablet.
- **Regenerate-on-change, not per-frame:** bake the texture once when text changes (static/compose
  pass), then only blit each frame — never re-run Cairo in the render loop. This is exactly the mod's
  two-pass discipline (`VSAPI-NOTES.md`; `ScribeRowElement.cs:9-27`).
- **Cache per character glyph** (small fixed set) rather than per full string where possible, to cap
  texture count. Render full lines only when a string is finalized.
- **Culling:** only hold textures for tablets within render range; free them when the block entity
  leaves view/unloads to bound worst-case VRAM in tablet-dense areas.
- **Cairo surface churn** produces short-lived managed+unmanaged allocations; batching the redraw to
  text-change events (not ticks) keeps GC pressure negligible.

### 4.3 Residual unknowns

- **In-world (non-GUI) rendering** has no codebase precedent — the `BlockEntityRenderer`/
  `ITexPositionSource` path is unproven here and is the main implementation risk. GUI-dialog rendering
  is essentially solved.
- glyph-forge's `glyphs-1.json` bundle can be **stale** relative to per-glyph files — vendor from the
  per-glyph files (or regenerate the bundle) as part of the asset-copy step, and validate the schema
  (presence of `leftWidth`/`rightWidth`) on import.

---

## 5. Recommendation

1. **Adopt Strategy 1.** Ingest per-glyph stroke JSON as a vendored mod asset; render with Cairo to a
   `LoadedTexture`; reuse the `ScribeRowElement` bake/blit discipline.
2. **Phase 1 — GUI dialog:** render the tablet's text in the edit/read dialog (zero new rendering
   primitives; direct reuse of proven code). Ships value immediately.
3. **Phase 2 — in-world quad:** add a `BlockEntityRenderer`/`IRenderer` that blits the baked texture
   onto the tablet face. Contained, well-understood risk.
4. **Defer Strategy 3** (voxel carving) until/unless true 3D relief is required; treat it as a purely
   presentational upgrade with no data-model impact (consistent with `v3-clay-tablet.md`).
5. **Reject Strategy 2** (PNG atlas) — incompatible with glyph-forge's actual export and the
   ordered-stroke/animation requirement.

Cross-repo boundary reminder: glyph-forge's guardrails state it produces and documents JSON only and
does **not** integrate with this mod; all consumption logic lives here, built against
`EXPORT-FORMAT.md` / `COMPOSED-LINE-FORMAT.md`.
