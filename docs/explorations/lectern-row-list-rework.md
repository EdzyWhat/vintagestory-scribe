# Exploration: unified custom-drawn lectern row list

**Status:** exploring (not yet an OpenSpec change). Created 2026-07-20.

> **Temporary holding pen (decided 2026-07-20).** OpenSpec has no native "exploration"
> artifact — the `spec-driven` schema defines only proposal / specs / design / tasks. This is
> therefore a plain repo doc, kept only while the exploration is still fluid. When we open the
> change, its convergent content migrates into that change's `proposal.md` and `design.md`
> (especially design's "Decisions" and "Open Questions" sections, which exist for exactly this),
> and **this file is deleted**. Do not treat it as a durable spec.

## The through-line

Several separate threads all converge on one architectural move — **replace the lectern's
current mixed static+interactive row list with a single custom-drawn row list, shared by both
the read and editor views, whose rows render entirely in the interactive pass.**

What this one move resolves:

1. **Scroll pop / clip limitation** (the whole `skeuomorphic-lectern-gui` Decision-4 saga).
   Custom rows drawn 100% in the interactive pass are clipped correctly by the engine's
   `BeginClip` scissor — so scrolling becomes smooth and sub-pixel, with no viewport culling,
   no cull-don't-clip pop, no mask, and no fade. This is confirmed by how vanilla Handbook /
   Journal lists work (see research below).
2. **Read/edit views feeling "glued together"** (user, 2026-07-20). Today they're two separate
   layouts (different chrome, different sizes) that drift apart. A shared row renderer with the
   interactive bits toggled on/off makes them one list in two modes.
3. **Tech-stack simplification** (user). One row-rendering path to maintain instead of two
   divergent view compositions.
4. **The "gamey" base-game row look** (user). User wants a **lined-paper aesthetic** (ruled
   lines, not distinct embossed boxes) — only achievable by drawing the rows ourselves.
5. **Disliked checkboxes** (user, recurring). A custom row makes a custom checkbox first-class,
   and folds in the parked "custom checkbox visual with stamp/erase animation" ROADMAP item.
6. **Smooth drag-reorder** (`lectern-drag-reorder-feedback`, now ON HOLD). The lift-ghost and
   insertion indicator build naturally on a custom-drawn row list; better to design them
   together than bolt them onto the old rows.

## The hard part (known, with a tractable mitigation)

**In-place text editing.** Drawing display text + checkbox + icons per-frame is easy; a live
caret/selection/typing field is not something to hand-roll. Mitigation surfaced by research:
**custom-drawn rows for display, plus a single real `GuiElementTextArea`/`TextInput` overlaid
only on the one row currently being edited** (spreadsheet-style edit-in-place). Bonus: only ever
one live text input, which sidesteps the `GlScissorFlag(false)` multi-input scissor clobber
(see research). This is the "Large" item — a v1.x refactor of the lectern GUI, not a small task.

## Decisions already made in scoping (2026-07-20)

- **Skip snap-scrolling.** It was a workaround for the pop; real clipping removes the pop, so
  snap would be throwaway work. Not building it.
- **Fold `lectern-drag-reorder-feedback` into this.** That proposal is marked ON HOLD; its
  ghost/indicator/drop-settle will build on this row infrastructure.
- **Both read and edit views** get reworked onto the shared row list (not edit-only).

## Resolved decisions (2026-07-20, scoping conversation)

- **Architecture: one `ScribeRowElement` with a mode flag** (read vs. edit), NOT a base +
  subclasses. Subclassing would re-fork the "glued-together / different sizes" drift we're
  killing. One element draws the shared skeleton; edit mode lights up interactive zones.
- **Edit-in-place = one shared floating input.** The row element draws a static text *label*
  per frame; on focus, the ONE real `GuiElementTextInput` is repositioned onto the focused row
  and that row **suppresses drawing its own text label for that frame** ("blank the zone" =
  skip painting those pixels, NOT clearing/deleting data — `block.text` is untouched). Avoids a
  double-draw and the only-ever-one-live-input sidesteps the multi-input `GlScissorFlag(false)`
  clobber. Handoff is invisible iff label and input share baseline/font/x-offset (see #1).
- **#1 Baseline alignment → Option A: one shared `RowTextLayout` metric.** A single helper
  computes x-offset, baseline Y, font size once; BOTH the label draw and the input placement
  read from it. Structural single-source-of-truth, not tuned-until-it-looks-right. This is also
  the layout authority the checkbox, gutters, and ruling all key off.
- **#2 Ruling → Option B, hairline (solid, not dotted).** The rule is a **real structural part
  of the row**, not a cosmetic afterthought — so it can be swapped for an **image** later. Small
  but reasonable padding around it, and that padding **scales with font size**. The ruling
  **scrolls with the rows** (not fixed preprinted lines). Color/alpha/thickness = config knobs.
- **#3 Checkbox → Option A: clean custom glyph now, animation hook later.** Replace the gamey
  `GuiElementSwitch` with our own drawn glyph; leave a clearly-marked seam for the parked
  stamp/erase animation (deferred to S4).
- **#4 Read-mode interactivity → Option A (scoped): the read-mode checkbox IS interactive**
  (click to toggle done). NOTHING else in read mode is interactive — no editing, no drag, no
  hover actions. All other mutation stays in edit mode.
- **#5 Edit navigation → Option B+ : Enter advances / Shift+Tab retreats between rows** (Enter
  and blur commit; Esc reverts to stored string). PLUS richer in-field caret conventions the
  user wants: Cmd/Ctrl+Arrow to line start/end, Alt/Option+Arrow to skip by word, and Shift with
  any of those to extend selection. **See open risk below — native support unverified.**
- **#6 Scope → Option B: staged sequence**, each independently shippable + Mac-playtestable:
  - **S1** — `ScribeRowElement` + read view: custom rows, ruled lines, real interactive-pass
    clipping (this alone fixes the original scroll-pop), custom checkbox glyph, interactive
    read-mode checkbox. Prove the aesthetic on the lower-risk view first.
    **S1 must seed fake/sample lectern content** so there's something realistic to test against.
  - **S2** — edit-in-place: floating input, zone-blanking, `RowTextLayout` alignment, Enter/
    Shift+Tab nav + caret conventions; retire the old editor view.
  - **S3** — unhold `lectern-drag-reorder-feedback`; build lift-ghost/indicator on landed rows.
  - **S4** — checkbox stamp/erase animation (fills the S3-era hook).

## Open questions / risks still to resolve

- **#5 caret conventions — native support UNVERIFIED (S2 risk).** VS `GuiElementTextInput` /
  `GuiElementTextArea` may not natively support Cmd/Ctrl+Arrow, Alt/Option word-skip, or
  Shift-extend-selection. Before committing S2's spec, investigate what the base element handles
  vs. what we'd have to subclass/reimplement (and whether cross-platform Cmd-vs-Ctrl is even
  surfaced by the engine's key events). Decompile `GuiElementEditableTextBase` if the wiki/source
  is thin — and record findings in `VSAPI-NOTES.md`.
- **Fixed vs. scrolling ruling** — resolved to scroll-with-rows for now; revisit only if the
  future "replace rule with an image" idea wants fixed preprinted lines.
- Exact seed-content shape for S1 testing (how many rows, mix of tasks/notes, long lines to
  stress wrapping/clipping) — decide when proposing S1.

---

## Research findings (2026-07-20, three background agents)

All three read the archived `skeuomorphic-lectern-gui/design.md`, `VSAPI-NOTES.md`, and real
engine source (`/private/tmp/vsapi_repo`, `vssurvivalmod_repo`) + decompiles. Verbatim-faithful
summaries below.

### Root cause (confirmed by all three)

VS renders a composer in two stages (`GuiComposer.Render`):
1. **One static-texture blit** of `staticElementsTexture`, baked once at `Compose()` from every
   element's `ComposeElements()`. Runs first, **always unclipped**.
2. **Then each interactive element's `RenderInteractiveElements()`** — the *only* stage a
   `BeginClip`/`PushScissor` scissor is active for.

So `AddStaticText`, `AddInset` dividers, `GuiElementSwitch` box outlines, and
`GuiElementTextInput`/`TextArea` **borders** land in stage 1 and are never scissored — that's
why our mixed list bleeds. Second, independent clobber: `GuiElementTextInput.RenderInteractive
Elements` calls `GlScissorFlag(false)` (bypassing the `PushScissor`/`PopScissor` stack),
disabling scissoring for anything drawn after a task-row text input that frame.

**Decisive discovery:** vanilla scrolling lists (`GuiElementFlatList` in Handbook,
`GuiElementRichtext`/`GuiElementContainer` in Journal) draw their **entire** content — including
text — in `RenderInteractiveElements`, so `BeginClip` clips all of it and they scroll by a
simple `fixedY = -value` shift. Our list bleeds **only** because it uses stage-1 static
elements. This is the crux that makes "custom-drawn rows" the real fix.

### Approach A — Fade (rejected)

A clean full-row proximity fade is **not achievable** without the custom-draw rewrite. Static
row chrome (checkbox outlines, text-box borders, dividers) is baked into **one dialog-wide
texture blitted once per frame with a single global `Color`** (`GuiComposer.Color`) — there is
no per-row/per-element static alpha. You *can* fade the whole dialog's static layer at once
(`SingleComposer.Color = new Vec4f(1,1,1,a)`, read fresh each frame — useful for open/close),
and you *can* fade individual interactive elements via subclassing, but you cannot fade one
row's static chrome independently. A partial (interactive-only) fade is *worse* — text fades
while its border stays crisp then pops. Confirmed the `color` multiply does affect alpha
(`gui.fsh` line 81: `outColor = texture(tex2d, uv) * color`; premultiplied-alpha). Gotcha:
`GuiElementSwitch`'s check mark uses the color-less `Render2DLoadedTexture` — can't be tinted
without reimplementing it.

### Approach B — Mask / cover (feasible fallback, medium)

Let overflowing rows render past the edge (revert to overlap-cull + a buffer), keep `clipBounds`
= exact visible rectangle, and draw opaque cover strips **on top** (interactive element at
z ~150, above rows' z=50, below scrollbar's z=200) that redraw a **slice of the same
`BackdropTexture`** — so rows slide "under the parchment." Must be a separate interactive
element, not the backdrop art (backdrop is static/z=50, *behind* the text).

**Click-through is already solved for free:** the engine separates render-clip from hit-test-
clip. `GuiElement.IsPositionInside` ANDs `InsideClipBounds.PointInside` — every element inside
`BeginClip` inherits `InsideClipBounds = clipBounds`, so a mouse over the mask strip is outside
`clipBounds` and **every buffered row element rejects the hit automatically**. Set `clipBounds`
exactly = visible window, pin masks to cover everything outside it; the three boundaries
coincide. The mask element itself (added *outside* `BeginClip`) should swallow its own clicks
and forward wheel to the scrollbar. Complexity **medium**. Top risk: verify in-game that a
higher-z `Render2DTexture` actually draws on top (scrollbar@200, focus overlay@800, outlines@500
all imply yes) — a Windows-restage/VSImGui check. Do **not** use `PushScissor` for the mask
(same `GlScissorFlag(false)` bug defeats it); rely on z-order painter draw.

### Approach C — the alternatives (this is where the real answer is)

- **A1 — real clipping via the interactive pass.** READ view (100% `AddStaticText`+`AddInset`):
  replace the per-row loop with a single `GuiElementRichtext` inside the existing `BeginClip`,
  scroll via `fixedY`. Real pixel clipping, smooth sub-row scroll, no cull, no recompose-on-
  scroll, drag-thumb works with zero handoff hackery. **Small–Medium**, well-precedented
  (Handbook detail / Journal). EDITOR view (mixed): riskier — live text inputs inside a
  container clip their border correctly (container captures children's `ComposeElements`) but
  the text input's `GlScissorFlag(false)` still clobbers the clip for later elements that frame,
  and there's **no shipped precedent for a live editable field inside a `GuiElementContainer`**.
  **Large/uncertain** for editor via this path.
- **A2 — fully custom-drawn rows** (`IFlatListItem`-style / bespoke `GuiElement`). Everything in
  stage 2 → `BeginClip` clips natively, scroll is a `fixedY` shift (exactly how
  `GuiElementFlatList` works). Honest cost = editable text: realistic shape is **display-only
  custom rows + one real edit field overlaid on the focused row** (Medium–Large), which also
  sidesteps the multi-input scissor clobber (only ever one live input). **Strongest checkbox
  bonus** — makes the custom/circular checkbox and hover icons first-class and removes the
  `GuiElementSwitch` `size`/`toggleable` workarounds. `GuiElementFlatList` itself lives in
  `VSSurvivalMod.dll` (not the API) so can't be referenced — must be reimplemented.
- **A3 — render-to-texture** = already provided by `GuiElementContainer` (composes children to
  an offscreen `ImageSurface`, blits clipped in stage 2). Collapses into A1's container path;
  same caveat for live inputs. A hand-rolled framebuffer would be redundant/Large.
- **A4 — snap-scrolling.** Cheapest (**Small**, zero-risk): quantize scroll offset to row
  boundaries so the pop becomes an intentional one-row advance. Wheel already advances one
  `RowStep`; thumb-drag/track/keyboard snap by rounding `CurrentYPosition` to the nearest
  cumulative `rowYs` boundary in `OnRowListScroll`, keeping the existing drag-handoff. **Does
  not** fix "row taller than viewport never renders." **Decided against** in favor of the custom
  row work, which removes the pop entirely.

### Research's ranked recommendation

1. Snap-scrolling first (unconditional, cheap) — **superseded by our decision to skip it.**
2. Read-view → single `GuiElementRichtext` inside `BeginClip` (medium, well-precedented).
3. Editor-view → custom-drawn display rows + single overlaid edit field (larger, but the only
   path that makes editor clipping trivial **and** delivers the custom checkbox).

**Our lean (2026-07-20):** pursue a **unified custom-drawn row list (A2-style) for both views**,
because the user's goals (unify read/edit, one maintained path, lined-paper look, custom
checkbox) argue against the A1/A2 *split* the research proposed — a shared custom renderer
serves all of them, at the cost of building read view as custom rows too rather than reusing
richtext. To be resolved in the open questions above before proposing.
