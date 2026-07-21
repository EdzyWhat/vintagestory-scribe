# VintagestoryAPI notes

Facts about `VintagestoryAPI`/vanilla-mod internals learned by decompiling, kept here so
later tiers don't re-derive them or waste a round misdiagnosing a known failure mode as
something else (e.g. staging). **Check this file before decompiling anything** — if the
symptom isn't listed, decompiling is still fair game, just add the finding here once you
have it (see the entry template at the bottom).

Cross-reference: `openspec/changes/add-lectern-block/tasks.md` group 8 has the full
incident writeups this file distills.

## GUI composer / element lifecycle

**Symptom: a crash or corrupted layout right after `SetValue`/`SetPlaceHolderText`-style
calls on a text element, especially right after adding it to a composer.**

`GuiComposer.Compose()` doesn't calculate real `Bounds` (e.g. `InnerWidth`) until it runs
`CalcWorldBounds()` on the whole tree. Calling `SetValue` on a text input/area *before*
that point runs the auto-height/line-wrap math against `InnerWidth == 0`, corrupting the
baked-in value and, transitively, the dialog's outer size. `GuiComposer.Compose()` also
swallows any exception `CalcWorldBounds()` throws (log-only, no rethrow), so the actual
crash surfaces somewhere unrelated downstream (for us: a Cairo `BlurPartial` "surface
width/height must be above 0" exception).

**Fix pattern:** split element-adding code from value-seeding code. Add all elements, call
the composer's own `.Compose()`, *then* seed values in a second pass. See
`ScribeBlockRowCell.Compose` vs. `ApplyValues` in `src/Mod/ScribeBlockRowCell.cs`.

---

**Symptom: after any recompose, typing focus/caret jumps to element 0 (or a slider's drag
resets after one step).**

The composer's default `.Compose()` call uses `focusFirstElement: true`. Any full rebuild
of `SingleComposer` (e.g. from an add/delete/toggle button) is a *brand-new* element tree —
old element references (and their in-progress interaction state, like a slider mid-drag or
a text area's caret position) are gone. There is no way to "keep" an old element across a
recompose; you must snapshot state before and restore it onto the new instance after.

**Fix pattern:** capture (focused element key, caret position) before recomposing, then
call `composer.FocusElement(tabIndex)` (not `OnFocusGained` directly — that leaves two
elements marked `HasFocus`) and restore the caret after. See
`GuiDialogScribeLectern.RecomposeEditorViewPreservingFocus`. For a slider specifically,
`GuiElementSlider.TriggerOnlyOnMouseUp` (the API's own fix) is `internal` and unusable
from mod code — defer the recompose yourself to the dialog's own `OnMouseUp` instead. See
`textSizePendingRecompose` in `GuiDialogScribeLectern.cs`.

---

**Symptom: `GetTextInput(key)` (or `GetTextArea`) throws `InvalidCastException` on some
rows but not others.**

`AddTextInput` registers a `GuiElementTextInput`; `AddTextArea` registers a
`GuiElementTextArea`. `Get*` helpers cast to the specific type and throw if you call the
wrong one. Any code that doesn't know a row's kind ahead of time (e.g. hit-testing during
drag-reorder) must not assume which accessor applies.

**Fix pattern:** use `composer.GetElement(key)?.Bounds` (base `GuiElement`, no kind-specific
cast) when you only need bounds/position, not text-editing behavior.

---

**Symptom: a row/element's rendered content overlaps the element below it once text gets
long.**

`GuiElementTextInput` (single-line) never wraps — long text scrolls horizontally instead.
`GuiElementTextArea` (multi-line) *does* wrap and grows past whatever fixed height you laid
it out at. A fixed row-height constant is fine until content wraps past it; nothing warns
you when that happens, it just visually overlaps the next row.

**Fix pattern:** measure first with the engine's own wrap-aware sizing —
`ICoreClientAPI.Gui.Text.GetMultilineTextHeight(font, text, width)` (the same mechanism
`GuiElementTextArea.TextChanged()` uses internally) — and lay out using the max of that and
your minimum height. See `ScribeBlockRowCell.MeasureWrappedHeight`. Only text areas need
this; text inputs never wrap so their fixed height is already correct.

**Symptom: a dialog's close (X) button only registers a click on a small sliver of the
visible icon, not the whole glyph.**

**Not a bug in our mod — confirmed against vanilla.** `GuiElementDialogTitleBar`'s
`closeIconRect` hit-test math is internally consistent (confirmed via decompile and a
live hover-position diagnostic: logged mouse coordinates matched the computed hit-rect
exactly). The visible X glyph (plus its drop-shadow/hover-glow padding) simply reads
larger to the eye than the tight ~17x17 logical-pixel rectangle that actually registers
clicks. Reproduced identically on a plain vanilla dialog (e.g. a chest) — same tight
hitbox, same visual-vs-clickable mismatch. Likely a general engine/Retina-display
interaction (untested at 100% GUIScale / non-Retina), not specific to `GuiDialogBlockEntity`
or any of our composer setup.

**Fix pattern:** none needed — don't spend a round re-investigating this if it resurfaces
on a different dialog. If it's ever worth truly fixing (e.g. accessibility), the lead
would be `GuiElementDialogTitleBar.unscaledCloseIconSize` / its hit-rect math, but that's
vanilla engine code we can't patch from mod code — not actionable from here.

**Diagnostic technique that worked, for next time:** a click-based test is ambiguous once
a successful click closes the dialog mid-test (you lose the ability to compare "before"
state). Prefer a throttled hover-position log (`OnMouseMove`, logged via
`ICoreClientAPI.ShowChatMessage`) plus one screenshot with the cursor visibly on the
target and the chat log visible in the same frame — gives an unambiguous side-by-side
without repeated clicking.

**Symptom: a row list needs to scroll instead of running off the bottom of a fixed-height
dialog, or growing the dialog itself without bound.**

`GuiComposer` has a built-in clip+scroll idiom, confirmed against the real
`anegostudios/vsapi`/`vssurvivalmod` source (`GuiDialogTrader.cs`,
`GuiDialogBlockEntityInventory.cs`), not just decompiled: `BeginClip(clipBounds)` pushes a
`GuiElementClip` that calls `api.Render.PushScissor(Bounds)` and sets
`composer.InsideClipBounds = clipBounds`; every element added afterward
(`AddInteractiveElement`/`AddStaticElement`) inherits that as its own `InsideClipBounds`.
`EndClip()` pops the scissor and clears it. A `AddVerticalScrollbar(onNewValue, bounds,
key)` + `.GetScrollbar(key).SetHeights(visibleHeight, totalHeight)` (called *after* the
composer's own `.Compose()`, once the real content height is known) drives a callback that
sets the *content* bounds' `fixedY = 0 - value; fixedY.CalcWorldBounds()` — shifting every
child's `absY` in one call, since `CalcWorldBounds()` recurses into `ChildBounds`.

Mouse hit-testing is scroll-aware **for free**: `GuiElement.IsPositionInside` ANDs
`Bounds.PointInside` with `InsideClipBounds.PointInside`, and any hit-test that reads a live
`Bounds.absY` (rather than recomputing layout math independently) picks up the scroll shift
automatically, since `absY` is recalculated by the same `CalcWorldBounds()` call the scroll
callback triggers. No manual scroll-offset arithmetic needed in hit-test code.

**Correction (this entry originally overclaimed): `BeginClip`/`PushScissor` alone does
NOT visually clip a mixed static+interactive row list's rendering — it only sets up the
plumbing `IsPositionInside` reads for hit-testing.** Confirmed live during
`skeuomorphic-lectern-gui` playtesting (a document with enough rows to overflow visibly
bled its dividers/text through the controls below the clip region —
`screenshots/debug/2026-07-18_20-43-11_hover-hide-behavior.png`), then confirmed the
mechanism against real vsapi source: `GuiComposer.Render()` draws every *static* element
(e.g. `AddInset` dividers, `AddStaticText` rows) in one single always-unclipped texture
blit, generated at the very top of `Render()` before any `GuiElementClip`'s
`RenderInteractiveElements` (which is where the scissor push actually happens) ever runs.
Separately, `GuiElementTextInput.RenderInteractiveElements` (a task row's own text box)
issues its own `api.Render.GlScissor(...)` scoped to its own bounds, then unconditionally
calls `GlScissorFlag(false)` afterward — which cancels scissoring outright rather than
restoring whatever outer scissor `BeginClip` had pushed. Vanilla's own reference usages
(`GuiDialogTrader`'s item-slot-grid scrollbar, `GuiDialogBlockEntityInventory`'s) get away
with this because a slot grid is a single well-behaved interactive element with no static
children and no scissor-canceling side effects — they never hit either failure mode.

**Fix pattern:** don't trust `BeginClip`/`PushScissor` to hide overflow for a row list that
mixes static elements (dividers, read-view text) with `GuiElementTextInput`/
`GuiElementTextArea` rows. Viewport-cull instead: measure every row's position/height in a
first pass, then only actually add/compose (`AddStaticText`/`ScribeBlockRowCell.Compose`)
the rows whose measured range overlaps the current scrolled viewport (plus a small buffer,
so minor scroll movement doesn't force a recompose on every tick) in a second pass. Still
use `BeginClip`/`AddVerticalScrollbar` for the scrollbar control itself and for hit-testing
scroll-awareness (both of those parts of this entry's original finding hold) — just don't
rely on the clip to hide rows outside the buffered window; visibility comes from never
composing them, not from the engine hiding them after the fact. See
`GuiDialogScribeLectern.ComposeReadView`/`ComposeEditorView`'s two-pass measure/cull
structure, `RowListCullBuffer`, and `OnRowListScroll`.

---

**Symptom: with the row-list culling fix above already in place, a row's tail still
renders past the dialog's bottom edge once scrolled to a specific position (not
necessarily at the very top of the scroll range).**

The cull test above must require *full containment* of a row within the visible window,
not mere *overlap*. An overlap test (`rowBottom < windowTop || rowTop > windowBottom` →
skip) still composes a row that only partially intersects the window — and since nothing
here visually clips a composed row's rendering (see the entry above), that row renders at
its full, unclipped height, with the portion outside the window bleeding straight past the
dialog's drawn frame. Confirmed live via the playtest-checklist app: scrolling to a
position where a row straddled `windowBottom` made its tail (up to a full row's height,
here ~30px, coincidentally close to but unrelated to the title bar's height) render below
the dialog.

**Fix pattern:** require full containment, not overlap: `rowTop < windowTop || rowBottom >
windowBottom` → skip. A row now only composes once entirely inside the visible window,
popping in/out cleanly at the scroll boundary instead of rendering a partial tail.
Tradeoff: a single row taller than the visible window itself can never be fully contained
at any scroll position and will never render — inherent to cull-don't-clip; would need real
clipping (confirmed unavailable, see the entry above) to fix. See
`GuiDialogScribeLectern.cs`'s pass-2 comments in `ComposeReadView`/`ComposeEditorView`.

---

**Symptom: scrolling a hand-stacked row list (parent `fixedY = 0 - scrollValue` +
`CalcWorldBounds()`) moves some parts of a row but not others. An all-static list (read
view) doesn't visually move at all on scroll — rows just cull in/out in place. A mixed
static+interactive list (editor view) scrolls the interactive parts but leaves the static
parts frozen: text-input content moves, but its border stays; the checkbox's check +
highlight move, but the box outline stays; a static drag glyph stays. The frozen widgets
are still fully clickable/typable where they landed after scroll.**

VS renders GUI elements in TWO passes with TWO different Y coordinates, and a parent
`fixedY` shift only reaches ONE of them. Confirmed via `ElementBounds` decompile:
- **Static pass** — `GuiElement.ComposeElements(Context ctxStatic, ...)`, baked ONCE into
  a cached texture at compose time — draws at **`bgDrawY`/`drawY`**:
  `bgDrawY = absFixedY + absMarginY + absOffsetY + ParentBounds.drawY`. No scroll term; the
  texture is not re-baked on scroll.
- **Interactive pass** — `RenderInteractiveElements(float dt)`, redrawn EVERY frame —
  draws at **`renderY`**: `renderY = absFixedY + ... + ParentBounds.renderY +
  renderOffsetY`. This DOES pick up the shifted parent.

So shifting the content parent's `fixedY` moves `renderY` (live pass) but not the
already-baked static texture (`drawY`). Which elements sit in which pass:
`AddStaticText`/`AddInset` dividers are wholly static (→ read view rows don't move at all).
`GuiElementTextInput`/`GuiElementTextArea` draw their *text content* in the interactive
pass but their *border/background* in `ComposeElements`; `GuiElementSwitch` draws its box
outline in `ComposeElements` (`RoundRectangle`/`EmbossRoundRectangleElement`) but the
check + hover highlight in `RenderInteractiveElements` (→ editor view: text/check move,
box/border don't).

This is the same underlying static/interactive split as the "BeginClip doesn't visually
clip" entry above — that one is the *clip* half, this is the *scroll-shift* half.

**Fix pattern:** don't rely on shifting the parent `fixedY` to scroll a hand-stacked
static+interactive list. Position each row at a **viewport-relative Y** at compose time
(`rowY - scrollValue`) so BOTH passes bake at the already-scrolled coordinate. Combine
with viewport culling (rows outside the window aren't composed at all) exactly as the
entries above require. See `GuiDialogScribeLectern.ComposeReadView`/`ComposeEditorView`.

---

**Symptom: dragging a `GuiElementScrollbar` (or `AddSlider`) thumb moves it one step/pixel
then the drag dies; mouse-wheel and track-clicks work fine.**

A sustained drag gesture is being interrupted by a mid-gesture recompose. If the value-
change callback (`OnRowListScroll` / a slider's `onChanged`) rebuilds `SingleComposer`,
the freshly composed scrollbar/slider is a BRAND-NEW element that never received the
mouse-down, so the drag is orphaned after one step. One-shot inputs (wheel, track-click)
survive because they don't rely on a held gesture spanning frames.

**Fix pattern:** defer the recompose out of the drag. Set a "pending recompose" flag and
actually rebuild in `OnMouseUp` instead of inside the change callback. This dialog already
does exactly this for its text-size slider (`textSizePendingRecompose`, drained in
`OnMouseUp`) — mirror it for the scrollbar (`rowListScrollPendingRecompose`). Detect an
in-progress thumb-drag via the scrollbar's own public `mouseDownOnScrollbarHandle` field, so
one-shot inputs (wheel, track-click) still recompose immediately while only held drags defer.
See `GuiDialogScribeLectern.OnRowListScroll`/`OnMouseUp`.

**Interaction with the compose-at-scrolled-Y fix (entry above):** once rows are composed at a
viewport-relative Y and the parent `fixedY` shift is gone, there is no cheap live state to
nudge during the drag — the only thing that moves rows is a recompose, which is exactly what's
being deferred. So the thumb tracks the mouse live (the scrollbar element draws its own handle
every frame) but the row content stays put and snaps to the final position on mouse-up. That
snap-on-release is an accepted consequence of cull-don't-clip + compose-at-scrolled-Y, not a
separate bug. Do NOT try to restore a live-`fixedY`-nudge to make the content track
continuously — it would reintroduce the static-chrome-frozen glitch the entry above fixes.

---

**Gotcha (engine inconsistency, not yet hit but worth flagging): `GuiElementTextArea`'s own
wrap-height write skips a GUIScale division that `GuiElementDynamicText`/
`GuiElementTextBase` both apply for the same operation.** `GuiElementTextArea.TextChanged()`
assigns the wrap-height straight to `Bounds.fixedHeight` (no `/ RuntimeEnv.GUIScale`), but
`GuiElementDynamicText.AutoHeight()` / `GuiElementTextBase.GetMultilineTextHeight()` both
divide by `RuntimeEnv.GUIScale` for the equivalent calculation.
`ScribeBlockRowCell.MeasureWrappedHeight` correctly mirrors the `TextArea` convention (no
division) since our text-section rows use `GuiElementTextArea` — but a future "fix" to make
it consistent with the other convention would silently double effective row height at any
non-1.0 GUIScale. If a similar height-measurement helper is ever added for a
`GuiElementDynamicText`-backed element, don't copy `MeasureWrappedHeight`'s no-division
convention without checking which base class is actually involved.

**Symptom: a toggle/icon-button's `On` state, seeded to reflect persisted model state,
silently reverts right after any mouse-up elsewhere in the dialog -- not just clicks on
the button itself.**

`GuiElementToggleButton.OnMouseUp` (the base of `AddIconButton`'s icon-button widget)
unconditionally runs `if (!Toggleable) On = false;` -- and this override fires on *every*
`OnMouseUp` dispatched to the dialog, not gated by whether the click landed on this
specific button. `Toggleable` defaults to `false` in the constructor if not explicitly
passed `true`. So any icon button meant to visually persist an on/off model state (not
just a momentary fire-once action like a delete button) needs `toggleable: true` at
construction, or its seeded `On` value gets wiped on the very next unrelated click.

**Fix pattern:** pass `toggleable: true` for any icon button whose `On` represents real
persisted state; leave it `false` only for momentary actions with no state to preserve.
See `ScribeHoverIconButton`'s constructor doc comment in `ScribeBlockRowCell.cs`.

## Localization (`Lang`)

**Symptom: every player-facing string renders as its own raw lang key (e.g.
`scribe-gui-title` shown literally), even right after confirming `en.json` is present,
correctly formatted, and freshly staged.**

**This is not a staging bug — don't spend a round re-checking staging first.** Every lang
entry loaded from a mod's `assets/<modid>/lang/en.json` is registered keyed by its owning
domain: `TranslationService.LoadEntry` stores it as `"<modid>:<key>"`, not bare `"<key>"`.
`Lang.Get(key)` resolves via `KeyWithDomain(key)`, which defaults to the `"game"` domain
when `key` contains no `:` — it does **not** infer "the calling mod's own domain" from
context. So `Lang.Get("scribe-gui-title")` actually looks up `"game:scribe-gui-title"`,
which never exists, and `Lang.Get` silently falls back to printing the raw key (its
documented behavior on a missing key — no exception, no log line pointing at the mistake).

Independently corroborated: a real third-party mod (`xlib:levelup`) prefixes every one of
its own `Lang.Get` calls the same way, confirming this isn't a quirk of how our lang file
was authored.

**Fix pattern:** every `Lang.Get` call site (including string literals passed over the
network, like a `RefusalReason`, since the *receiving* client is the one that resolves it)
must use `"<modid>:<key>"`, e.g. `Lang.Get("scribe:scribe-gui-title")`. Don't forget
`WorldInteraction.ActionLangCode` — same resolution path.

**Diagnostic shortcut for next time:** if strings render as raw keys, grep the call sites
for a `"<modid>:"` prefix before touching staging/build output at all.

## VSImGui debug overlay

**Question: what key toggles the VSImGui debug overlay in-game (for the Debug-only
`RegisterDebugSliders` layout tuning)?**

**Ctrl + P.** Confirmed by decompiling the vendored `src/Mod/lib/VSImGui.dll` (v1.2.7):
`RegisterHotKey("imguitoggle", ..., (GlKeys)98, (HotkeyType)2, false, true, false)`. The
signature is `RegisterHotKey(code, name, key, type, altPressed, ctrlPressed, shiftPressed)`,
so it's `ctrlPressed: true` + `GlKeys 98`, and `GlKeys 98 == P` in
`VintagestoryAPI.dll`'s enum. Same file also registers `imguiincfont` = **Ctrl+F9** and
`imguidecfont` = **Ctrl+F8** for overlay font size (useful if the slider labels render too
small to read).

These are the code-registered defaults; a rebind would live in
`VintagestoryData/clientsettings.json`'s `keyMapping` (empty by default = defaults in
effect). If Ctrl+P doesn't open the overlay, check `keyMapping` for an override or a
conflicting bind before assuming the mod failed to load.

**Symptom: VSImGui loads and Ctrl+P registers, but pressing it shows NOTHING on screen
(no overlay, no error dialog) -- specifically on Apple Silicon.**

The overlay cannot render on macOS Apple Silicon. macOS caps OpenGL at **4.1** (Apple
deprecated OpenGL; it's emulated over Metal), but ImGui.NET's GL renderer -- which VSImGui
wraps -- issues calls the 4.1/Metal path rejects. Confirmed from `client-main.log`: a
startup `GLFW Exception: VersionUnavailable Requested OpenGL version 4.3, got version 4.1`
(`Graphics Card Renderer: Apple M4`), then **thousands of per-frame**
`[Error] after final compo - OpenGL threw an error: InvalidOperation` (8000+ in one
session), where "after final compo" == `EnumRenderStage.AfterFinalComposition` (value 10),
the exact stage VSImGui's `OffWindowRenderer` registers into (`RegisterRenderer(..., (EnumRenderStage)10, ...)`
in the decompiled `VSImGui.dll`). So Ctrl+P *does* toggle overlay state and the `Draw`
event *does* fire -- the draw call just errors out every frame, drawing nothing.

This is a platform incompatibility, NOT a mod bug, hotkey problem, or staging error --
don't chase it as one. The mod's `#if DEBUG` `RegisterDebugSliders` tuning path is
therefore unavailable on this Mac; run it on a machine with OpenGL >= 4.3 (a Windows box
with a normal GPU) via a **Debug**-configuration stage (`build/restage.ps1 -Configuration
Debug`, or `build/restage.sh Debug`). Note a plain restage builds Release, which excludes
VSImGui entirely (Mod.csproj `Configuration == 'Debug'` Condition) -- so even on capable
hardware the sliders only exist in a Debug stage. ConfigLib's own settings panel is pure
VS GUI (no ImGui) and works on any platform as an alternative live-ish editing path.

## Entry template

```
**Symptom: <what you observed, in the words someone debugging it later would use>.**

<the actual mechanism, confirmed via decompile — name the type/method>.

**Fix pattern:** <what to do instead>. See `<file>`.
```
