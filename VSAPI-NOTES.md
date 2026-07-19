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

## Entry template

```
**Symptom: <what you observed, in the words someone debugging it later would use>.**

<the actual mechanism, confirmed via decompile — name the type/method>.

**Fix pattern:** <what to do instead>. See `<file>`.
```
