## 1. Pre-flight

- [ ] 1.1 Confirm the working tree is clean on the `row-list-rework` branch, then re-read the
      read-view composition in `src/Mod/GuiDialogScribeLectern.cs` (`ComposeReadView`, the row
      loop, `BeginClip`/scroll wiring, `OnRowListScroll`) and the custom-element pattern in
      `src/Mod/ScribeBlockRowCell.cs` (`ScribeDragHandleElement`, `ScribeHoverIconButton`) that
      the new element will mirror.
- [ ] 1.2 Confirm the exact server-authoritative done-toggle call path the editor already uses,
      so the read-view checkbox can reuse it verbatim (no new packet, no Core change).

## 2. Shared layout metric

- [ ] 2.1 Add a `RowTextLayout` helper (own file in `src/Mod/`, or alongside the row element)
      that computes, from a row's bounds + the current text-size scale, the single source of
      truth for text x-offset, baseline Y, and font (design.md Decision 5). Use the engine's
      font/text-measurement helpers rather than hand-rolled metrics.

## 3. Config knobs

- [ ] 3.1 Add documented `ScribeClientConfig` fields for the ruling, following the existing
      `Row*` layout-knob style: ruling color, alpha, and thickness, plus the base text↔ruling
      padding (stored unscaled; scaled at point of use by `TextSizeScale`, consistent with the
      existing `ScaledRowSpacing`/`ScaledRowDividerThickness` pattern).

## 4. Custom row element

- [ ] 4.1 Add `ScribeRowElement : GuiElement` (interactive-pass element) with a read/edit mode
      flag (design.md Decision 1). Only the read path is implemented in this change; leave the
      edit branch stubbed with a clear `// S2:` marker.
- [ ] 4.2 In `RenderInteractiveElements`, draw the row per frame: the lined-paper ruling as a
      structural part of the row (Decision 3), the row text via `RowTextLayout` (Decision 5),
      and — for a task — the checkbox glyph. Ensure the ruling and its scaled padding are drawn
      as part of the row so they scroll with it.
- [ ] 4.3 Draw the checkbox through a small, clearly-named routine (e.g.
      `DrawCheckboxGlyph(done, rect)`) that renders checked/unchecked states and scales with
      text size (Decision 4). Add an explicit `// S4: stamp/erase animation hook` comment at the
      seam so the later animation can slot in without touching layout or hit-testing.
- [ ] 4.4 Implement checkbox hit-testing in the element: a click within the glyph rect toggles
      done via the path confirmed in 1.2; rely on `IsPositionInside`/`InsideClipBounds` so
      clipped/off-screen rows reject hits (Decision 6). Everything else in the row is inert.

## 5. Wire the read view onto the new element

- [ ] 5.1 Replace `ComposeReadView`'s static per-row loop with one
      `AddInteractiveElement(new ScribeRowElement(...))` per row, composed inside the existing
      `BeginClip` region (design.md Decision 2).
- [ ] 5.2 Scroll by shifting the rows' `fixedY` by `-scrollValue` (the `GuiElementFlatList`
      model), and remove the read-view-only scroll workarounds that the native clip now makes
      unnecessary (recompose-on-scroll / any mask/snap compensation) — leaving the editor view's
      current path untouched until S2.
- [ ] 5.3 Verify the read view still respects the existing "continuous scroll, no pagination"
      and "checkbox scales with text size" requirements with the new element.

## 6. Sample content seed

- [ ] 6.1 Seed a few hardcoded read-view rows (mixed tasks + notes, including at least one long
      line to stress wrapping/clipping) behind a single clearly-commented, easily-removed seam
      (design.md Decision 7). It must never touch persisted document state.

## 7. Build and verify

- [ ] 7.1 `dotnet build src/Mod/Mod.csproj` (Debug) and `--configuration Release` — both clean.
- [ ] 7.2 `dotnet test tests/Core.Tests/Core.Tests.csproj` — all green (no Core change expected;
      confirm no regression).
- [ ] 7.3 Restage on the Mac (`bash build/restage.sh`) and fully relaunch.
- [ ] 7.4 Manual playtest of the read view: (a) rows render with the lined-paper ruling and
      custom checkbox glyph; (b) scrolling a long document clips boundary rows partially (no
      pop) and is continuous/sub-row; (c) the ruling scrolls with its row and its padding scales
      when text size changes; (d) clicking a checkbox toggles done and syncs; (e) clicking/
      hovering elsewhere on a row does nothing (inert).
- [ ] 7.5 Confirm no regression to the editor view (still composes and scrolls as before) and to
      overall dialog open/close, text-size slider, and pin persistence.
