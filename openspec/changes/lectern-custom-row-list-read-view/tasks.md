## 1. Pre-flight

- [x] 1.1 Confirm the working tree is clean on the `row-list-rework` branch, then re-read the
      read-view composition in `src/Mod/GuiDialogScribeLectern.cs` (`ComposeReadView`, the row
      loop, `BeginClip`/scroll wiring, `OnRowListScroll`) and the custom-element pattern in
      `src/Mod/ScribeBlockRowCell.cs` (`ScribeDragHandleElement`, `ScribeHoverIconButton`) that
      the new element will mirror.
- [x] 1.2 Confirm the exact server-authoritative done-toggle call path the editor already uses.
      FINDING: the editor's only mutation path is `ScribeEditDocumentMessage` → `ApplyEdit`,
      which is **lock-gated** (`fromPlayer.PlayerUID != lockHolderUid` → rejected). The read
      view deliberately holds no lock, so it **cannot** reuse this path verbatim. Resolved with
      the user: add a new lock-free `ScribeToggleTaskMessage` (Decision 6, updated). Core's
      `ScribeDocument.ToggleTask(index)` already exists and is bounds-safe/task-only — no Core
      change.

## 1b. Lock-free toggle message (server plumbing)

- [x] 1b.1 Add `ScribeToggleTaskMessage` (ProtoContract, following `ScribeReleaseLockMessage`'s
      shape): `PosX/PosY/PosZ` + a `BlockIndex` int. Client→server.
- [x] 1b.2 Register it on the network channel in `ScribeModSystem.Start` (same order both sides)
      and add a server-side handler in `StartServerSide` that looks up the lectern and calls a
      new `BlockEntityScribeLectern.ToggleTaskFromReader(index)`.
- [x] 1b.3 Add `BlockEntityScribeLectern.ToggleTaskFromReader(int index)` (server-side): calls
      `Document.ToggleTask(index)` with **no lock check**, and on success calls
      `MarkDirty(redrawOnClient: true)` to persist + re-sync (mirrors `ApplyEdit`'s sync, minus
      the lock gate). A bad/non-task index is a no-op (ToggleTask returns false).

## 2. Shared layout metric

- [x] 2.1 Add a `RowTextLayout` helper (own file in `src/Mod/`, or alongside the row element)
      that computes, from a row's bounds + the current text-size scale, the single source of
      truth for text x-offset, baseline Y, and font (design.md Decision 5). Use the engine's
      font/text-measurement helpers rather than hand-rolled metrics.

## 3. Config knobs

- [x] 3.1 Add documented `ScribeClientConfig` fields for the ruling, following the existing
      `Row*` layout-knob style: ruling color, alpha, and thickness, plus the base text↔ruling
      padding (stored unscaled; scaled at point of use by `TextSizeScale`, consistent with the
      existing `ScaledRowSpacing`/`ScaledRowDividerThickness` pattern).

## 4. Custom row element

- [x] 4.1 Add `ScribeRowElement : GuiElement` (interactive-pass element) with a read/edit mode
      flag (design.md Decision 1). Only the read path is implemented in this change; leave the
      edit branch stubbed with a clear `// S2:` marker.
- [x] 4.2 In `RenderInteractiveElements`, draw the row per frame: the lined-paper ruling as a
      structural part of the row (Decision 3), the row text via `RowTextLayout` (Decision 5),
      and — for a task — the checkbox glyph. Ensure the ruling and its scaled padding are drawn
      as part of the row so they scroll with it.
- [x] 4.3 Draw the checkbox through a small, clearly-named routine (e.g.
      `DrawCheckboxGlyph(done, rect)`) that renders checked/unchecked states and scales with
      text size (Decision 4). Add an explicit `// S4: stamp/erase animation hook` comment at the
      seam so the later animation can slot in without touching layout or hit-testing.
- [x] 4.4 Implement checkbox hit-testing in the element: a click within the glyph rect fires a
      callback the read view handles by sending `ScribeToggleTaskMessage` (block index) — no
      optimistic local mutation; the authoritative re-sync updates `lectern.Document` and
      triggers `RefreshReadView`, which recomposes with the new state. Rely on
      `IsPositionInside`/`InsideClipBounds` so clipped/off-screen rows reject hits (Decision 6).
      Everything else in the row is inert.

## 5. Wire the read view onto the new element

- [x] 5.1 Replace `ComposeReadView`'s static per-row loop with one
      `AddInteractiveElement(new ScribeRowElement(...))` per row, composed inside the existing
      `BeginClip` region (design.md Decision 2).
- [x] 5.2 Scroll by shifting the rows' `fixedY` by `-scrollValue` (the `GuiElementFlatList`
      model), and remove the read-view-only scroll workarounds that the native clip now makes
      unnecessary (recompose-on-scroll / any mask/snap compensation) — leaving the editor view's
      current path untouched until S2.
- [x] 5.3 Verify the read view still respects the existing "continuous scroll, no pagination"
      and "checkbox scales with text size" requirements with the new element. (By inspection: no
      page-turn controls exist; the row's checkbox size flows from `ToggleWidth * TextSizeScale`
      via `RowTextLayout`, so it scales with text size. Confirmed visually in 7.4.)

## 6. Sample content seed

- [x] 6.1 Seed a few hardcoded read-view rows (mixed tasks + notes, including at least one long
      line to stress wrapping/clipping) behind a single clearly-commented, easily-removed seam
      (design.md Decision 7). It must never touch persisted document state.

## 7. Build and verify

- [x] 7.1 `dotnet build src/Mod/Mod.csproj` (Debug) and `--configuration Release` — both clean.
      (Added a `cairo-sharp` reference to Mod.csproj — the game-runtime 2D lib the custom element
      draws with; not a new mod dependency.)
- [x] 7.2 `dotnet test tests/Core.Tests/Core.Tests.csproj` — all green (35/35; no Core change).
- [x] 7.3 Restage on the Mac (`bash build/restage.sh`) and fully relaunch.
- [x] 7.4 Manual playtest of the read view (2026-07-21): (a) rows render with the lined-paper
      ruling and custom checkbox glyph ✓; (b) scrolling clips boundary rows partially (slice), no
      pop, continuous/sub-row ✓; (c) ruling scrolls with its row and padding scales with text
      size ✓; (d) clicking a checkbox toggles done and syncs ✓; (e) elsewhere on a row is inert ✓.
      A multi-line clipping bug (last wrapped line cut off) was found and fixed (scaled-vs-fixed
      unit mismatch → shared `RowHeightFixed`); re-verified clean. Checkbox glyph enlarged + a
      ~20% forgiving hitbox added and accepted. (f) two-client lock-free toggle not machine-tested;
      reasoned: the toggle rides `ScribeToggleTaskMessage` → `ToggleTaskFromReader`, which has no
      lock check, so it applies regardless of who holds the editor lock.
- [x] 7.5 No regression to the editor view (still composes/scrolls via its unchanged path) or to
      dialog open/close, text-size slider, or pin persistence — confirmed in the same session.
