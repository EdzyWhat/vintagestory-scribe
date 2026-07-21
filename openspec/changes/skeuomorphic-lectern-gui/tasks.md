## 1. Pre-flight

`add-lectern-block`'s in-flight changes (playtesting bugfixes, tasks 8.10–8.18) and
`reduce-agent-overhead`'s code-hygiene tasks have both landed since this change was
proposed — no longer blocked. Still worth a fresh look since the files changed:

- [x] 1.1 Confirm `src/Mod/GuiDialogScribeLectern.cs`, `src/Mod/ScribeBlockRowCell.cs`,
      and `src/Core/ScribeBlock.cs`/`ScribeDocument.cs`/`ScribeDocumentCodec.cs` have no
      uncommitted changes in this or any other worktree before proceeding.
- [x] 1.2 Re-read the current state of all files above (they changed since this change
      was proposed) before making any edits, so task descriptions below are applied
      against current code, not a stale mental model.

## 2. Core: pin flag, reserved assignment field, codec version bump

- [x] 2.1 Add `Pinned` (bool, default false) and `AssignedToUid` (nullable string,
      default null) to `ScribeBlock`. Both are per-block fields, not a side table (design.md
      decision 7). Update its constructor and any existing call sites that construct one
      positionally.
- [x] 2.2 Add `ScribeDocument.TogglePinned(int index)`, mirroring `ToggleTask`'s shape and
      task-only restriction (fails on a text-section block or an out-of-range index,
      never throws). No mutation method for `AssignedToUid` yet — it stays reserved/unset
      until the future Desk work defines real semantics.
- [x] 2.3 Bump `ScribeDocumentCodec.Version` and serialize/deserialize the two new fields.
      No backward-compat parse path for the old version (design.md decision 8, explicitly
      accepted) — a version mismatch continues to fail safe via the existing
      magic/version check, it just now also rejects the previous version's bytes.
- [x] 2.4 Add `Core.Tests` coverage: `TogglePinned` (toggle on/off, fails on a text
      section, fails on an out-of-range index) and a codec round-trip test asserting
      `Pinned`/`AssignedToUid` survive serialize→deserialize (including a null
      `AssignedToUid`).
- [x] 2.5 `dotnet test tests/Core.Tests/Core.Tests.csproj` — confirm all tests pass,
      including the new ones. (35/35 passing.)
- [x] 2.6 The codec version bump (2.3) breaks `tests/Integration.Tests/fixtures/lectern.vcdbs`
      (built under v2) — confirmed empirically: `PersistenceScenarios` failed with 0 blocks
      instead of 2 after the bump, since `TryDeserialize` correctly rejects the old-version
      bytes (this is the accepted breaking-change behavior from design.md decision 8, now
      proven by the integration suite rather than assumed). Regenerated via
      `atlas fixture ... --scenario BuildsLecternWithDocumentFixture --out
      tests/Integration.Tests/fixtures/lectern.vcdbs --force` per README's documented
      workflow. `dotnet test tests/Integration.Tests --filter "FullyQualifiedName!~FixtureBuilders"`
      — 10/10 passing again.

## 3. Scrollable/clipped content region (resolves `add-lectern-block` task 8.15)

- [x] 3.1 Investigate `GuiComposer`'s clipped-region support, starting from
      `GuiDialogTrader`'s scrollbar usage (already referenced in
      `ScribeBlockRowCell`'s class doc comment) and `VSAPI-NOTES.md`. Decompile only if
      the wiki/shipped-mod-source don't answer it, per existing project discipline — add
      any new finding to `VSAPI-NOTES.md`. (Confirmed against real upstream
      `anegostudios/vsapi`/`vssurvivalmod` source: `BeginClip`/`InsideClipBounds`
      propagation genuinely makes mouse hit-testing scroll-aware, but does NOT clip
      *rendering* for a mixed static+interactive row list — see design.md's Decision 4
      correction and task 3.2a. `VSAPI-NOTES.md`'s "row list needs to scroll" entry has
      been corrected accordingly.)
- [x] 3.2 Add a scrollable/clipped region wrapping the row list in both
      `ComposeReadView` and `ComposeEditorView`, replacing the current unclipped
      absolute-Y stacking. (`BeginClip`/`BeginChildElements(contentBounds)`/
      `EndChildElements`/`EndClip`/`AddVerticalScrollbar` + `OnRowListScroll` +
      `rowListContentBounds` provide the scrollbar control/value plumbing; actual visual
      hiding of off-screen rows comes from 3.2a's viewport culling, not from the clip
      itself.)
- [x] 3.2a Rework the row list to viewport-cull instead of relying on the engine's
      scissor for visual correctness: only add/compose rows (and their dividers) whose
      position falls within the current visible scrolled window, recomposing on scroll
      rather than assuming `BeginClip` hides the rest. (Two-pass measure/cull structure
      in both `ComposeReadView`/`ComposeEditorView`: pass 1 measures every row's
      position/height; pass 2 only composes rows overlapping
      `[scrollValue - RowListCullBuffer, scrollValue + VisibleListHeight +
      RowListCullBuffer]`. `OnRowListScroll` only triggers a recompose once the live
      scroll position escapes the last-composed range, not on every scroll tick/drag
      pixel — editor-view recomposes go through `RecomposeEditorViewPreservingFocus` so
      scrolling while typing doesn't reset focus/caret. `isComposingRowList` suppresses
      the scrollbar's own synchronous `SetHeights`-triggered callback (which always
      reports 0 on a freshly constructed scrollbar) so restoring the real scroll position
      right after doesn't re-enter/snap to top. `HitTestRowIndex`'s end-of-list fallback
      now uses the last actually-composed index, not `Blocks.Count - 1`, since with
      culling those can differ. `ApplyValues` only seeds rows in the composed range, since
      a culled-out index has no live element to seed. `VSAPI-NOTES.md`'s "row list needs
      to scroll" entry corrected to document this.

      **Correction after a first live retest still showed bleed-through** (a row rendered
      past "Done Editing", directly on the world behind the dialog — user caught this,
      correctly diagnosed it as "just don't draw anything outside a bounding box" being
      the missing piece):`RowListCullBuffer` was initially set to a full viewport height
      to throttle recompose frequency, but ANY nonzero buffer means a buffered-but-
      off-screen row is still fully composed and rendered at its true position, with
      nothing constraining it to the dialog's own drawn area — the whole point of
      culling is that an uncomposed row can't render anywhere, and a buffer directly
      undermines that. Set to `0`: only rows genuinely within the visible scrolled window
      are ever composed. Accepted tradeoff: recomposes on every scroll tick rather than
      only when the visible set changes — judged acceptable since this dialog already
      recomposes on other frequent interactions with no observed cost.

      **Second correction after a submitted playtest report (2026-07-19) again showed
      bleed-through, this time only when scrolled (not at the top of the range):** with
      `RowListCullBuffer` already at 0, the remaining bug was that the cull test itself
      used *overlap* (`rowBottom < windowTop || rowTop > windowBottom` → skip), not full
      *containment* — a row that only partially intersected the visible window still got
      composed, and (since nothing here visually clips a composed row's rendering) still
      rendered at its full, unclipped height, tail bleeding past the dialog's bottom edge
      by up to a full row's height. Fixed by requiring full containment instead: `rowTop <
      windowTop || rowBottom > windowBottom` → skip. A row now only composes once
      entirely inside the visible window. Accepted tradeoff (same shape as the
      `RowListCullBuffer` tradeoff above): a single row taller than the visible window
      itself can never be fully contained at any scroll position and will never render —
      inherent to cull-don't-clip. See design.md's Decision 4 (second correction) and
      `VSAPI-NOTES.md`'s matching entry.

      **Second correction after that retest showed a NEW symptom** (a row rendering fully
      detached above the title bar, over the chat log — user again caught this and asked
      how the vanilla Handbook handles its own scrollable list, prompting investigation of
      real `vssurvivalmod` source: `GuiDialogHandbook`/`GuiElementFlatList`). That
      investigation found the Handbook uses a fundamentally different architecture —
      one permanent `GuiElementFlatList` element that gates each item's rendering with a
      live per-frame position check inside its own `RenderInteractiveElements`, never
      recomposing on scroll at all. Adopting that literally for this row list was
      evaluated and rejected: `GuiElementSwitch`/`GuiElementTextInput`/
      `GuiElementTextArea`/`GuiElementStaticText`/`GuiElementInset` all bake their visuals
      into the composer's shared static texture during `ComposeElements`, unlike
      `GuiElementFlatList`'s items which draw 100% of their own appearance per-frame —
      matching the Handbook's approach genuinely would mean reimplementing checkbox
      rendering and, worse, text input/area editing (caret, selection, typing) from
      scratch, a large, bug-prone undertaking for something the engine currently provides
      for free.

      Root-caused the actual detached-row symptom instead: `OnRowListScroll` was
      recomposing `SingleComposer` *synchronously*, from inside `GuiElementScrollbar`'s own
      `OnMouseMove`/`OnMouseWheel` — both invoked by `GuiComposer.OnMouseMove`/
      `OnMouseWheel` while those methods are still iterating their own `interactiveElements`
      collection. Reassigning `SingleComposer` mid-iteration corrupts that in-progress
      dispatch. This is the exact same class of bug `textSizePendingRecompose` was already
      written to avoid (a slider drag calling back mid-`OnMouseMove`) — generalized here to
      a `rowListRecomposePending` flag checked once per frame in a new
      `GuiDialogScribeLectern.OnRenderGUI` override, rather than `OnMouseUp` alone, since
      mouse-wheel scrolling has no "mouse up" to hook. `OnRowListScroll` now only sets the
      flag; the actual `RecomposeEditorViewPreservingFocus`/`ComposeReadView` call happens
      outside any dispatch loop. Verified: clean build, 35/35 Core.Tests, 12/12 Atlas
      Integration.Tests all still pass — this is a client-GUI-only change with no
      server-observable behavior, so neither suite exercises the fix directly; live
      in-game re-verification of the actual overflow/scroll behavior (with the deferred-
      recompose version) is 3.5's job. The recompose-based-without-this-fix state is
      preserved at commit `bb9eecf` for reference if this approach also needs
      reconsidering.)

      **Third correction after that retest showed a further variant of the same symptom**
      (user report: rapidly clicking "Add Task" — before any scrollbar interaction at all
      — produced rows bleeding onto/over the title bar; a provided screen recording
      (`~/Desktop/V1-Scroll.mov`, frame-extracted via `cv2` for inspection) confirmed the
      very first bad frame occurs while the scrollbar handle is still static, ruling out
      the scrollbar as this occurrence's trigger). The `rowListRecomposePending`/
      `OnRenderGUI` deferral only covered `OnRowListScroll`'s own recompose — every OTHER
      button/toggle handler in this dialog (`OnClickAddTask`, `OnClickToggleToolPanel`,
      `OnRowDelete`, `OnRowTextChanged`'s live-height recompose, `OnClickSwitchToRead`) was
      still calling `ComposeEditorView`/`ComposeReadView`/`EnterMode` *synchronously* from
      inside its own click callback — and `GuiElementToggleButton`/`GuiElementTextButton`/
      `GuiElementSwitch` all fire those callbacks from `OnMouseDownOnElement`/
      `OnMouseUpOnElement`, called by `GuiComposer.OnMouseDown`/`OnMouseUp`/`OnKeyDown`
      while those methods are still iterating their own `interactiveElements` collection —
      the identical reentrancy hazard already fixed for the scrollbar, just untouched on
      every other call path. Independently corroborated by the client crash log itself
      (`~/Library/Application Support/VintagestoryData/Logs/client-crash.log`): a
      `GuiElementDialogBackground` blur exception with stack `OnClickAddTask` <-
      `GuiElementToggleButton.OnMouseDownOnElement` <- `GuiComposer.OnMouseDown` — the exact
      same class of bug, pre-dating even this GUI redesign.

      Generalized the fix: `rowListRecomposePending` (bool) replaced with
      `pendingRecomposeAction` (a deferred `System.Action?`, via a new `RequestRecompose()`
      helper that captures the correct view), still drained once per frame in
      `OnRenderGUI`. Every mid-dispatch-unsafe call site now calls `RequestRecompose()`
      instead of recomposing directly: `OnClickAddTask`, `OnClickToggleToolPanel`,
      `OnRowDelete`, `OnRowTextChanged`, `OnRowListScroll`. `OnClickSwitchToRead` assigns
      `pendingRecomposeAction` directly (bypassing `RequestRecompose`, since that helper
      only knows how to recompose the *current* view, but this handler also flips
      `IsEditorMode` itself via `EnterMode`). Two call sites were confirmed safe to leave
      as direct, synchronous calls: `OnMouseUp`'s own drag-reorder and text-size-slider
      recomposes, both of which run *after* `base.OnMouseUp(args)` has already returned —
      by that point the composer-level dispatch loop has finished, so there is no
      in-progress iteration left to corrupt. Verified: clean build, 35/35 Core.Tests, 13/13
      Atlas Integration.Tests all still pass (same as before — this remains a
      client-GUI-only change with no server-observable behavior). Live in-game
      re-verification (rapid Add-Task clicking, and scrolling, together) is still 3.5's
      job.)

      **Reopened 2026-07-20:** live testing on Windows (VSImGui slider tool) showed the
      recompose/reentrancy work above is sound, but scroll never actually moved rows
      visually — the culling window advanced while rows stayed nailed to their unscrolled
      positions (read view) or only their interactive parts moved (editor view). Root
      cause is the static-vs-interactive render-pass split (design.md Decision 4, third
      correction): the `contentBounds.fixedY` shift this task relies on only reaches the
      interactive pass. The two-pass culling structure itself stands and is reused; the
      `fixedY`-shift-for-visual-movement assumption does not. Superseded by 3.4a (switch
      to viewport-relative row Y). Leaving unchecked until 3.4a lands and 3.5 re-verifies.

      **Re-closed 2026-07-20:** 3.4a has landed. The two-pass measure/cull structure this
      task built stands unchanged and is reused as-is — only the pass-2 row Y coordinate
      moved from absolute-content to viewport-relative (`rowTop - scrollValue`), which is
      3.4a's edit, not a rework of the culling itself. The full-containment cull test,
      `isComposingRowList` guard, `HitTestRowIndex` last-composed-index fallback, and
      `ApplyValues` composed-range seeding are all still in place and correct. Re-checked
      rather than left open since the culling mechanism this task is about is done and
      sound; the remaining "does it actually scroll correctly in-game" question belongs to
      3.5's live pass, not to this structural task.
- [x] 3.3 Confirm drag-reorder (`OnMouseMove`/`OnMouseUp`/`HitTestRowIndex`) still works
      correctly when the row list is scrolled away from its top position — hit-testing
      must account for scroll offset. (`HitTestRowIndex` already reads live
      `bounds.absY`, which `CalcWorldBounds()` recalculates on every scroll — confirmed
      by code inspection that no offset math is needed; live drag-while-scrolled retest
      still pending in 3.5/9.3.)
      **Re-verify after 3.4a (2026-07-20):** 3.4a moves rows from a parent-`fixedY` shift
      to a viewport-relative composed Y. Hit-testing reads live `bounds.absY`, so the
      change should keep click targets aligned — but this must be re-confirmed in-game
      once 3.4a lands, since the coordinate the rows are composed at is changing.
- [x] 3.4 Revisit `MaxTextSizePercent` (currently 150, capped as a stopgap for the missing
      scroll region) now that overflow is handled by scrolling — raise or remove the cap
      per design.md's note that the original constraint no longer applies. Confirm with
      the user before removing it outright if any doubt remains about other reasons for
      the cap. (Raised to 300 as a looser sanity bound rather than removed outright.)
- [x] 3.4a Fix scroll not visually moving composed rows (design.md Decision 4, third
      correction — confirmed live on Windows via the VSImGui slider tool, 2026-07-20).
      The current model shifts the content parent's `fixedY` (`0 - scrollValue`) and
      recalcs world bounds, but that only reaches the interactive render pass (`renderY`);
      the static compose pass (`drawY`/`bgDrawY`, baked once into a cached texture) has no
      scroll term, so read-view rows don't move at all and editor-view row chrome
      (text-box borders, checkbox outlines, drag glyphs) freezes while text scrolls.
      Fix: in `ComposeReadView`/`ComposeEditorView` pass 2, position each composed row at
      a viewport-relative Y (`rowY - scrollValue`) so BOTH passes bake at the already-
      scrolled coordinate, and drop reliance on the `contentBounds.fixedY` parent shift
      for visual movement. Preserve the existing viewport culling (rows outside the window
      still aren't composed) and `RecomposeEditorViewPreservingFocus` (scroll-while-typing
      must not reset caret). Re-confirm hit-testing after: `HitTestRowIndex` reads live
      `bounds.absY`, so the Y-model change must keep click targets aligned with what's
      drawn.
      **Implemented 2026-07-20:** both `ComposeReadView` and `ComposeEditorView` pass 2 now
      compose each row at `rowTop - rowListScrollValue` (viewport-relative), and the
      end-of-compose `contentBounds.fixedY = 0 - scrollValue` / `CalcWorldBounds()` parent
      shift was removed from both (a parent shift on top of the viewport-relative Y would
      double-offset the rows). `OnRowListScroll` no longer nudges `fixedY` live either --
      visual movement now comes solely from recomposing at the new scroll value. Row
      dividers follow the same shifted Y. `HitTestRowIndex` is unchanged (still reads live
      `bounds.absY`, which now reflects the composed-at-scrolled-Y coordinate). Clean
      Debug+Release build, 35/35 Core.Tests. `VSAPI-NOTES.md`'s "static vs interactive
      render pass" entry already documented this fix pattern. Live re-verify is 3.5's job.
- [x] 3.4b Fix scrollbar thumb-drag dying after ~1px in both views (design.md Decision 4,
      fourth correction). `OnRowListScroll` calls `RequestRecompose()` the moment the
      viewport escapes the composed range, which rebuilds `SingleComposer` including a
      brand-new scrollbar element that never saw the mouse-down — orphaning the in-progress
      drag after one step (mouse-wheel and track-clicks survive because they're one-shot,
      not sustained gestures). Fix follows the existing `textSizePendingRecompose`
      template: while the thumb is held, update `scrollValue`/`fixedY` live but defer the
      cull-triggered recompose to `OnMouseUp` instead of firing it mid-drag.
      **Implemented 2026-07-20 (with a deviation from the wording above, forced by 3.4a):**
      the original plan said "update `scrollValue`/`fixedY` live during the drag." Since
      3.4a removed the parent-`fixedY` shift entirely (it only ever moved the interactive
      pass, and did nothing for the all-static read view), there is no `fixedY` to nudge
      live anymore -- attempting it would reintroduce the exact static-chrome-frozen glitch
      3.4a fixes. Resolution: while the thumb is held (detected via
      `GuiElementScrollbar.mouseDownOnScrollbarHandle`, a public field on the real vsapi
      element) the thumb tracks the mouse live but the row content stays put; the recompose
      that re-bakes rows at the new scroll position is deferred via a new
      `rowListScrollPendingRecompose` flag, drained in `OnMouseUp` after
      `base.OnMouseUp(args)` has cleared the scrollbar's own drag state. So the content
      snaps to the final position on release rather than tracking continuously mid-drag --
      an accepted consequence of cull-don't-clip + compose-at-scrolled-Y. Mouse-wheel and
      track-clicks (`mouseDownOnScrollbarHandle` false) recompose immediately via the normal
      next-frame `RequestRecompose` path. Clean Debug+Release build, 35/35 Core.Tests. Live
      re-verify (smooth thumb-drag, no dying after one step) is 3.5's job.
- [ ] 3.5 Manually test: create a document with enough rows to overflow the visible
      dialog height; confirm every row is reachable by scrolling, in both read and editor
      view — rows visually move on scroll (not just cull in/out in place), editor-view row
      chrome (borders/checkbox outlines/drag glyphs) tracks its text, and dragging the
      scrollbar thumb scrolls smoothly rather than dying after one step.

## 4. Portrait reshape

- [x] 4.1 Pick concrete portrait `ElementBounds` dimensions (design.md leaves exact pixel
      dimensions as an open question) and update `DialogBounds()`. (`DialogBounds()` uses
      `ElementStdBounds.AutosizedMainDialog`, which auto-fits to children -- narrowing the
      row-list width (4.2) combined with the fixed `VisibleListHeight` from group 3 is
      sufficient to make the dialog read taller-than-wide; no direct edit to
      `DialogBounds()` itself was needed.)
- [x] 4.2 Update `EditorListWidth` and the read view's `listWidth` to the new, narrower
      portrait column width. (`ReadListWidth = 300`, `EditorListWidth = 340` -- down from
      480/540.)
- [x] 4.3 Re-check `ScribeBlockRowCell.TextWidth`/`MeasureWrappedHeight` call sites for any
      layout assumptions tied to the old wide dimensions. (All call sites already
      reference the `listWidth`/`EditorListWidth` constants, not hardcoded numbers --
      confirmed via grep; they pick up the new widths automatically, no further change
      needed.)
- [ ] 4.4 Manually test in-game (rewritten 2026-07-19 — the original wording ("confirm
      it reads well ... at the vanilla lectern's typical viewing distance") was flagged
      by the user as unclear how to actually act on): place a lectern, right-click it
      open at normal interaction range (don't back away or move closer than a normal
      right-click requires), and check concretely: (a) the dialog fits entirely within
      the screen with no part cut off or overlapping the hotbar/other HUD elements at
      the default GUI scale; (b) row text at the default `TextSizeScale` is legible
      without needing to lean in or zoom; (c) the portrait proportions (taller than
      wide) read as intentional rather than cramped — if any of these look wrong, note
      which one and what looked off rather than a bare pass/fail.

## 5. Custom-drawn backdrop

- [x] 5.1 Decide placeholder backdrop implementation: a static image asset under
      `src/Mod/assets/scribe/textures/gui/` vs. a purely procedural Cairo fill+border draw
      (design.md leaves this open) — pick based on whichever is less code for a first cut.
      (Static image, using the engine's own `AddImageBG` helper — a tiled/scaled
      `SurfacePattern` fill with rounded corners, confirmed via
      `GuiElementImageBackground.ComposeElements` in the real vsapi source — rather than a
      hand-rolled Cairo draw call. Placeholder generated via PIL: a mottled parchment-tone
      512×512 PNG, `assets/scribe/textures/gui/lecternbackdrop.png`.)
- [x] 5.2 Implement the backdrop, composited behind composer content the way the reference
      mod does it (Cairo `ImageSurface`/custom draw behind ordinary `GuiComposer`
      elements) — replacing `AddShadedDialogBG`. (Both `ComposeReadView`/`ComposeEditorView`
      now call `.AddImageBG(bgBounds, BackdropTexture)` instead.)
- [x] 5.3 Confirm the backdrop swap point (one asset path or one draw call) is isolated
      enough that a future real per-tier texture can replace it with no change to
      `GuiDialogScribeLectern.cs`'s layout/composition logic — this is the requirement
      from `specs/lectern-gui-shell/spec.md`'s "Backdrop is swappable" scenario; verify it
      concretely (e.g. swap in a second placeholder image and confirm nothing else needs
      to change) rather than assuming the architecture satisfies it. (Concretely verified:
      generated a second placeholder image, overwrote the same asset path, rebuilt clean
      with zero code changes, then restored the original.)
- [ ] 5.4 Manually test in-game: confirm the backdrop renders correctly behind both read
      and editor view content, with no rows rendered behind/under an opaque part of the
      backdrop.

## 6. Row-list visual restyle

- [x] 6.1 Fix checkbox scaling: pass `size: ToggleWidth * textSizeScale` (or equivalent)
      to `AddSwitch` in `ScribeBlockRowCell.Compose`, reusing the same `textSizeScale`
      factor `RowHeight` already applies (design.md decision 5). Manually test: drag the
      text-size slider across its range and confirm the checkbox grows/shrinks with the
      row text, not staying a fixed pixel size. (`textSizeScale` threaded through
      `Compose`/`TextWidth`; manual in-game slider test still pending in 9.3.)
- [ ] 6.2 Investigate whether `GuiElementSwitch`'s existing rendering can read as
      "circular" via its own constructor/params, or needs a custom element (design.md's
      open question) — decide based on how it actually looks in-game at the new scaling.
- [x] 6.3 Increase row spacing and add a subtle divider between rows (or between the row
      list and the toolbar), matching the Slack reference's generous, airy spacing rather
      than the current tight stacking. (`RowSpacing` raised 6px -> 14px; `AddRowDivider`
      adds a thin embossed `AddInset` line centered in the gap between rows, in both
      views.)
- [x] 6.4 Implement hover-conditional icon visibility (delete icon, and the new pin
      toggle from group 7) via a render-time mouse-position check inside each icon
      element's own rendering, not a recompose (design.md decision 6 — mirrors the
      vanilla title bar's own close/menu-icon hover-glow technique). Confirm no existing
      `AddIf`/recompose call sites are used for this. (New `ScribeHoverIconButton : GuiElementToggleButton`
      overrides `RenderInteractiveElements` to skip drawing when the mouse isn't over a
      caller-supplied `HoverRegion` -- the whole row's bounds, not the icon's own small
      bounds. Used for both the delete icon and the new pin icon. Confirmed via grep: no
      `AddIf` call site touches these icons.)
- [x] 6.5 Add a focus ring scoped to the actively-focused text field (task input or note
      text area), rather than the whole row, matching the Slack reference's blue focus
      box around only the field being edited. Investigate what focus-indication the base
      `GuiElementEditableTextBase`/composer already draws by default before adding a
      custom one — this may already exist and only need confirming, not building.
      (Confirmed via Explore agent against real vsapi source: both `GuiElementTextInput`
      and `GuiElementTextArea` already draw a `HasFocus`-conditional white-wash highlight
      in their own `RenderInteractiveElements`, scoped to that element only -- no custom
      code needed. It's a subtle full-area alpha wash, not a crisp border; live visual
      confirmation of whether that reads well enough deferred to 9.3.)
- [ ] 6.6 Manually test in-game: confirm hover-icon show/hide does not reset focus or
      caret position while typing (the exact regression this render-time approach is
      meant to avoid — verify it actually holds, don't just assume the mechanism works).

## 7. Pin toggle GUI affordance

- [x] 7.1 Add a pin-toggle icon button to each task row in `ScribeBlockRowCell.Compose`,
      wired to a new `onTogglePin` callback (mirrors the existing `onToggle`/`onDelete`
      wiring shape). Hover-conditional per group 6.4. Text-section rows get no pin
      affordance (design.md decision 7 — pin is task-only, same as `Done`). (Uses
      `ScribeHoverIconButton` with the `wpCircle` waypoint-circle icon, `toggleable: true`
      so its `On` state survives mouse-up elsewhere in the dialog -- see the class's own
      doc comment for why `toggleable: false`, the delete icon's setting, would have
      silently reset a seeded `Pinned` state.)
- [x] 7.2 Wire the callback through `GuiDialogScribeLectern` to
      `scratchDocument.TogglePinned(index)`, following the same `isDirty = true` +
      autosave pattern as `OnRowToggle`. (New `OnRowTogglePin` handler, identical shape.)
- [x] 7.3 Seed the pin toggle's visual on/off state from `block.Pinned` in
      `ScribeBlockRowCell.ApplyValues`, alongside the existing `Done`/text seeding.
      (`composer.GetToggleButton(PinKey(index)).On = block.Pinned`.)
- [x] 7.4 Confirm `AssignedToUid` has zero GUI surface anywhere in this file — no column,
      no toggle, nothing composed or seeded (design.md non-goal; verify by grep, not
      assumption). (Grepped `src/Mod/` for `AssignedToUid` — zero matches.)
- [ ] 7.5 Manually test in-game: pin and unpin a task in editor view; switch to read view
      and back; confirm the pinned state persists across a save/reload (fully quit and
      relaunch, not just close/reopen the dialog).

## 8. Atlas integration-test coverage

`tests/Integration.Tests` (Pixnop.Atlas.XUnit, local-only — see README's "Running the Atlas
suite") already covers persistence, the network edit round-trip, and the single-editor
lock. It has no coverage yet of this change's new server-observable behavior: the pinned
flag toggling and surviving a restart. (Scroll/portrait/backdrop are pure client-side GUI
layout with no server-observable state, so they're out of scope for Atlas — covered by
manual playtesting in group 9 instead.)

- [x] 8.1 Add a scenario (e.g. to `ServerAuthoritativeEditScenarios.cs` or a new
      `PinScenarios.cs`) that places a lectern, applies an edit pinning a task via
      `ApplyEdit`/`ScribeDocumentCodec.Serialize`, and asserts `Document.Blocks[i].Pinned`
      is `true` — mirroring the existing `ApplyEdit` usage pattern in
      `FixtureBuilders.BuildsLecternWithDocumentFixture`. (New `PinScenarios.cs`: pin and
      unpin, both through the real `ApplyEdit` path.)
- [x] 8.2 Extend `PersistenceScenarios` (or add a sibling `[AtlasWorld]` class against a
      new/updated fixture) asserting a pinned task's `Pinned` flag survives a genuine
      `RestartWorld` — same shape as the existing
      `Lectern_document_survives_a_server_restart` scenario. If this needs its own fixture,
      regenerate via the `atlas fixture` workflow (per 2.6) rather than hand-editing the
      `.vcdbs` file. (`FixtureBuilders.BuildsLecternWithDocumentFixture` now pins its task
      before applying; `PersistenceScenarios` asserts `blocks[0].Pinned` post-restart;
      fixture regenerated via `atlas fixture ... --force`.)
- [x] 8.3 `dotnet test tests/Integration.Tests --filter "FullyQualifiedName!~FixtureBuilders"`
      — confirm all scenarios pass, including the new ones. (12/12 passing, up from 10.)

## 9. Cleanup and verification

- [x] 9.1 `dotnet build src/Mod/Mod.csproj` — confirm clean build.
- [x] 9.2 `dotnet test tests/Core.Tests/Core.Tests.csproj` — confirm all pass, including
      group 2's new tests. (35/35 passing.)
- [ ] 9.3 Full manual playtest pass: place a lectern, open read + editor view, add enough
      rows to require scrolling, drag-reorder while scrolled, resize via text-size slider,
      pin/unpin a task, confirm no regression versus `add-lectern-block`'s existing
      playtesting checklist (its tasks.md group 7) for anything not specific to the old
      wide layout or the old fixed checkbox size.
- [x] 9.4 Confirm old-version saved documents fail to load safely (no crash, no partial/
      corrupt document) rather than silently misreading old bytes as new-format data —
      this is the accepted-breaking-change behavior from design.md decision 8; proven
      empirically by task 2.6 (the integration suite's own fixture hit exactly this case:
      failed safe with an empty document, no crash, no corruption).
- [x] 9.5 Update ROADMAP.md: mark the "skeuomorphic open-book UI... turn-page paging"
      parked bullet as done/superseded per this change (backdrop delivered; pagination
      explicitly rejected in favor of scrolling) rather than leaving it duplicated.
