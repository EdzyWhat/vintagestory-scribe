# Scribe custom icon SVGs (art spec)

> **Status:** art spec (2026-07-21, mechanism corrected by decompile 2026-07-21). Feeds the
> icon-font audit follow-up swap tasks in `lectern-gui-polish.md` item 8. **Registration mechanism
> and all four assets are now LANDED** (change `add-custom-svg-row-icons`): the four SVGs live at
> `src/Mod/assets/scribe/textures/icons/{pin,grip,close,edit}.svg` and are registered at client
> init as `scribepin`/`scribegrip`/`scribeclose`/`scribeedit`. What remains is **button
> repointing** — wiring each code to a real row-control button — owned by the affordance changes
> (`restore-row-affordance-columns` for the pin/delete columns, `lectern-drag-reorder-feedback`
> for the grip). This doc is the art + mechanism reference for that wiring.

## Why custom SVGs

The icon-font audit (see `lectern-gui-polish.md` item 8) decided to move four row/control icons
off the built-in glyph font onto custom SVGs:

- **Pin** — no built-in pushpin glyph (`wpCircle` reads as a dot).
- **Drag handle** — no built-in grip glyph (currently the literal text `"::"`).
- **Edit** — no built-in pencil/edit glyph (currently the text button "Edit").
- **Close / delete** — the built-in `x` (or `wpCross`) would work, but going **all-custom** keeps
  the four in one visual family (matched line quality, weight, and ink) rather than mixing a font
  glyph in with three hand-drawn ones. (Decision 2026-07-21.)

## How custom icons actually render (corrected — decompiled 2026-07-21)

**The earlier draft of this spec was wrong.** It claimed `IconUtil.DrawIcon` "falls through to
`Gui.DrawSvg` for non-built-in codes." It does **not**. Decompiling
`Vintagestory.API.Client.IconUtil` (`ilspycmd` against `/Applications/Vintage Story.app/
VintagestoryAPI.dll`) shows `DrawIconInt(cr, type, …)`:

1. First looks the code up in a `Dictionary<string, IconRendererDelegate> CustomIcons`.
2. If not found, runs a `switch` over the **hardcoded built-in names** (`plus`, `eraser`, `wpBee`, …).
3. **There is NO `default` case.** An unrecognized code (e.g. `"scribe:pin"`) matches nothing and
   **draws nothing — silently.** No exception, no fallback, just an empty button.

So a custom SVG is **not** a drop-a-file-and-pass-a-new-string change. The supported path is to
**register the SVG into `CustomIcons` at client startup** — but **NOT** with the obvious
`CustomIcons[code] = SvgIconSource(asset)` one-liner, which captures the asset and crashes once VS
unloads its `.Data` (see gotcha below). Use the re-resolving helper Scribe now ships
(`ScribeModSystem.RegisterSvgIcon`), which captures the `AssetLocation` and re-fetches each draw:

```csharp
// In client-side mod init (ICoreClientAPI capi available), once per icon:
api.Gui.Icons.CustomIcons[code] = (ctx, x, y, w, h, rgba) =>
{
    var asset = api.Assets.TryGet(loc, loadAsset: true);   // re-fetch each draw; reloads if unloaded
    if (asset?.Data is null) return;                        // never throw — draw nothing if missing
    api.Gui.Icons.SvgIconSource(asset)(ctx, x, y, w, h, rgba);
};
// loc = new AssetLocation("scribe", "textures/icons/pin.svg")
```

After registration, `new ScribeHoverIconButton(capi, "scribepin", …)` works — the button's base
`GuiElementToggleButton` calls `api.Gui.Icons.DrawIcon(ctx, icon, …, Font.Color)` every draw
(confirmed: `GuiElementToggleButton` lines ~98/131/146 in the decompile), which hits our
`CustomIcons` entry.

**Asset path (CONFIRMED — this is the load-bearing correction):** the file MUST live under a real
`AssetCategory`, i.e. `assets/scribe/textures/icons/pin.svg`, resolved as
`new AssetLocation("scribe", "textures/icons/pin.svg")`. A bare `assets/scribe/icons/` folder is
**never scanned** (VS has no `icons` `AssetCategory`) → `TryGet` returns null → silent empty icon.
**Asset-unload crash:** capturing the `IAsset` (the naive `SvgIconSource(asset)` form) crashes the
client mid-compose because `AssetManager.UnloadAssets()` nulls `.Data` on non-patched assets after
startup — hence the re-resolve-by-location delegate above. Both gotchas and the decompile evidence
are recorded in `VSAPI-NOTES.md` "Icon-button glyphs".

### Tinting: RESOLVED — author single-color, the button tints

`DrawIcon` passes the button's color down (`Font.Color` for a toggle button), and the interface
method is `DrawSvg(IAsset, ImageSurface, int posx, int posy, int width, int height, int? color)`
— i.e. `DrawSvg` **recolors** the SVG with the passed color (via `ColorUtil.FromRGBADoubles(rgba)`
in `SvgIconSource`). Consequences for the art:

- **Author each glyph as a single flat shape in one neutral color** (e.g. black `#000` /
  `currentColor`). Do **not** bake `#261C14` into the file — the button supplies the ink color.
- **Hover / pressed recolor works for free**: `ScribeHoverIconButton` can pass a different
  `Font.Color` per state and the same SVG re-tints. No second asset needed for hover.
- Practically this means the "ink" color lives in code (match the checkbox's `RulingColor`
  `#261C14` when constructing the button), not in the SVG. Multi-color glyphs are NOT supported
  through this path — the whole SVG is flood-recolored to one color, so design in one tone.

### Built-in escape hatch worth knowing

The same decompile shows `wpCross` is itself a `CustomIcons` entry that vector-draws a cross via
`capi.Gui.Icons.DrawCross(ctx, x, y, 4.0, w)`. So a clean X is available **built-in with zero
art** if the custom close glyph is ever deferred — it's the natural fallback for the close/delete
icon (task #1).

## Aesthetic: hand-inked, not geometric (direction set 2026-07-21)

Vintage Story's fiction is handmade items built with primitive tools. The icons should read as
**drawn by hand with a quill**, not as a clean geometric icon font. This is a deliberate reversal
of this spec's first draft (which specified uniform round-capped strokes — too clean).

Target line quality:

- **Irregular strokes.** Hand-drawn Bézier paths with slight wobble/waver along their length,
  not mathematically straight segments. A ruler-straight line reads as machine-made.
- **Variable stroke width.** A quill/nib leaves thick-and-thin: heavier on the down-stroke,
  tapering at the ends. Prefer filled paths whose *outline* varies in width over a constant
  `stroke-width` line, or a stroke with width variation where the tool allows.
- **Imperfect joins & asymmetry.** Corners that slightly overshoot or don't quite meet; two
  halves of a shape that aren't perfectly mirror-symmetric. Small imperfections sell "handmade."
- **Organic terminals.** Ends taper or blob like a lifted pen, rather than a clean round cap.
- **One ink tone.** Because the button flood-recolors (see tinting above), express all of this
  in *shape*, not color — the color is uniform ink. A little internal white space / broken line
  can imply a dry stroke, but it must survive downscaling.

Keep it restrained: hand-drawn, not scribbled. The glyph must still be instantly recognizable at
a glance and legible at small size (below). Think "confidently inked in two or three strokes,"
not "sketchy."

## Size & layout constraints (unchanged by the mechanism correction)

| Property | Value | Why |
|----------|-------|-----|
| `viewBox` | `0 0 24 24` | Conventional icon grid; resolution-independent, one file serves every text-size scale. |
| Safe area | artwork within `2 … 22` (≈2-unit padding all sides) | Drawn into a square gutter box; must not clip at the box edge. |
| Visual weight | strokes/filled paths roughly `1.8–2.4` units at the heaviest | Reads as a confident ink line; stays visible scaled toward the ~15px floor. Vary within this range for the quill thick/thin, don't go finer at the thin end than survives 15px. |
| Color | single flat neutral (`#000` / `currentColor`) | The button recolors to ink; see tinting. Do **not** bake a color. |

**Render-size reality:** the icon renderer computes a negative size and the SVG rasterizer
crashes below ~15px (see `ScribeClientConfig.MinRowHeight` doc comment; `MinRowHeight = 20` keeps
a margin — confirmed in `ScribeHoverIconButton` render notes). Design for legibility across
**~15px → ~40px** square. At the small end, keep the heaviest strokes near the top of the weight
range and avoid fine detail that only resolves at 40px — hand-drawn wobble must not turn to mush.

**Gutter box widths the glyph centers in** (from `ScribeClientConfig`): `DragHandleWidth = 24`,
`PinWidth = 32`, `DeleteWidth = 32`. Row height (hence box height) scales with text size but never
below `MinRowHeight`. Center each glyph in a square of the box's *shorter* dimension.

## Asset location (CONFIRMED — landed)

Under the `textures/` `AssetCategory` (sibling to the existing `textures/gui/`), because that is
the only place VS scans (see the mechanism note above):

```
src/Mod/assets/scribe/textures/icons/pin.svg     (AssetLocation "scribe:textures/icons/pin.svg")
src/Mod/assets/scribe/textures/icons/grip.svg
src/Mod/assets/scribe/textures/icons/close.svg
src/Mod/assets/scribe/textures/icons/edit.svg
```

The `CustomIcons` code strings (the keys registered at startup, and passed to the buttons) are
`"scribepin"`, `"scribegrip"`, `"scribeclose"`, `"scribeedit"` (no colon, to stay clearly distinct
from built-in names and from `AssetLocation` strings). Registered in
`ScribeModSystem.RegisterCustomIcons`.

---

## The four glyphs

All on a `0 0 24 24` viewBox, single flat color, **hand-inked line quality per the aesthetic
section** (the coordinates below are guide geometry — the author should hand-draw around them
with quill-like waver and thick/thin, not snap to exact points).

### 1. `pin` — pushpin / thumbtack

Replaces `wpCircle`. A **thumbtack pushed straight in**, not a map-pin teardrop (the teardrop
reads as a waypoint — the exact confusion we're leaving). Guide geometry:

- A rounded **head/cap** near the top, centered ~`(12, 6)` — a slightly lopsided disc or short
  capsule, not a perfect circle.
- A tapering **needle** from under the head down to a point ~`(12, 21)`, thick near the head and
  thinning to the point (natural quill taper).
- Optional two short, uneven "shoulder" ticks where cap meets pin, to sell the tack read.

Keep it roughly upright and centered so a future "pinned/unpinned" state could tilt or fill it.
Reads at 15px as "an upright tack," clearly distinct from a dot.

### 2. `grip` — six-dot drag handle

Replaces the text `"::"`. The conventional reorder grip: **two columns × three rows of dots**
(⠿). Filled dots — but hand-inked, so slightly irregular blobs of varying size, not six identical
circles.

- Columns at x ≈ `9` and `15`; rows at y ≈ `7`, `12`, `17`.
- Dot radius ≈ `1.6–2.0` units, varied slightly per dot.

The drag-handle gutter is the narrowest (24px) — six dots at this spacing stay legible; don't
shrink any dot below ~1.5 units or it vanishes at the small end. (This is the one glyph where the
"imperfection" must stay subtle — too much variation and it stops reading as a regular grip.)

### 3. `edit` — pen / nib (on-theme) or pencil

Replaces the "Edit" text button. **Author the nib pen as primary** (this is a writing mod — a
quill/nib is on-brand *and* still says "write/edit"), with a plain diagonal pencil as fallback if
the nib doesn't read at 15px:

- **Nib pen (primary):** a diagonal shaft from lower-left ~`(6, 18)` to upper-right ~`(18, 6)`,
  ending in a **split nib** (a short V) at the upper-right tip with a tine slit. A hand-drawn
  quill barrel (slight curve, thick/thin) suits this better than a straight pencil. Optional tiny
  ink dot/blot at the lower-left to imply a just-written stroke.
- **Pencil (fallback):** diagonal body `(6,18)`→`(17,7)`, a triangular tip at the upper end, a
  short ferrule/eraser band at the lower end.

Either way the diagonal orientation (lower-left → upper-right) is what signals "edit" at a glance
— keep it. The nib especially benefits from quill-like thick/thin along the barrel.

### 4. `close` — X / delete

Replaces `eraser`. **Two crossing ink strokes** corner-to-corner within the safe area — but as
two confident hand-drawn slashes, not a geometric X:

- Stroke A: ~`(7, 7)` → ~`(17, 17)`.
- Stroke B: ~`(17, 7)` → ~`(7, 17)`.
- Each with quill thick/thin and slight waver; they may cross slightly off-center and overshoot a
  touch at the ends (that's the handmade read).

Kept deliberately simple so it never competes with the pin/edit glyphs in the same row.
**Zero-art fallback:** the built-in `wpCross` (a `CustomIcons` vector cross) if the custom set is
deferred — see the escape-hatch note above.

---

## Handoff checklist for the author

1. **Wire one icon end-to-end first** to de-risk the path: register a single SVG in
   `CustomIcons` via `SvgIconSource(new AssetLocation("scribe:icons/…"))` at client init, point a
   button at its code string, and confirm it renders (recall: a wrong asset path or unregistered
   code draws **nothing, silently** — so "empty button" means the wiring, not the art, is wrong).
   Confirm the button's color tints it. This proves the mechanism before drawing all four.
2. Author the four SVGs on the `0 0 24 24` grid, single flat color, **hand-inked line quality**
   (irregular quill strokes, thick/thin, imperfect joins — see aesthetic section). Not geometric.
3. Verify each at ~15px and ~40px (the row-height extremes) — no clipping, still legible, wobble
   not mush.
4. Wire via the item 8 swap tasks (`lectern-gui-polish.md`) — each is **register icon + repoint
   button + keep/add the `AddHoverText` tooltip**, NOT a one-line code-string swap (that was the
   mistaken framing from the false "fallthrough" assumption). These land **after S2 settles**
   (they touch `ScribeBlockRowCell` / the read-view compose).
