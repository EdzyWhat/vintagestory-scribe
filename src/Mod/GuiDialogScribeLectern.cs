using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Scribe.Core;

namespace Scribe;

/// <summary>
/// The lectern's GUI. Two independent view modes:
/// - Read view (default, lock-free): renders <see cref="BlockEntityScribeLectern.Document"/>
///   live; never mutates anything, never sends anything.
/// - Editor view (shift+right-click, or the in-GUI toggle; holds the server-tracked
///   single-editor lock): edits a private scratch copy, autosaved to the server on a throttled
///   tick while dirty.
///
/// A <c>GuiDialogBlockEntity</c> rather than a plain <c>GuiDialog</c>, for the engine's own
/// per-block-position dialog dedup and walk-away auto-close. (The auto-close needs a range-check
/// override for Creative mode -- see <see cref="IsInRangeOfBlock"/>.)
/// </summary>
public sealed class GuiDialogScribeLectern : GuiDialogBlockEntity
{
    private readonly BlockEntityScribeLectern lectern;
    private readonly ScribeClientConfig clientConfig;

    public bool IsEditorMode { get; private set; }

    /// <summary>Editor-view-only scratch copy; never aliased to <see cref="BlockEntityScribeLectern.Document"/>.</summary>
    private ScribeDocument? scratchDocument;

    private bool isDirty;
    private long? autosaveTickListenerId;

    private int? draggedBlockIndex;
    private int? hoverTargetIndex;

    private bool isToolPanelExpanded = true;

    /// <summary>Row height used the last time <see cref="ComposeEditorView"/> laid out each note
    /// row, keyed by block index -- lets <see cref="OnRowTextChanged"/> detect when a note has
    /// wrapped to a different number of lines without re-measuring against a stale value, so it
    /// only recomposes on an actual height change rather than on every keystroke.</summary>
    private readonly System.Collections.Generic.Dictionary<int, double> composedNoteRowHeights = new();

    /// <summary>
    /// One entry per tool-panel button. <c>Icon</c> is a built-in icon-font code (see
    /// <c>Vintagestory.API.Client.IconUtil</c>) drawn on a small square button, with
    /// <c>LangKey</c>'s text shown as a hover tooltip rather than a label — the built-in icon set
    /// has no dedicated reorder/edit glyph, but a bare icon reads better at this size than a
    /// truncated word. <c>IsVisible</c> is the gating hook for future context-sensitive tools
    /// (e.g. hide "Reorder" below two rows) — v1 wires every option visible unconditionally, per
    /// task 5.3.
    /// </summary>
    private readonly record struct ToolbarOption(string Key, string Icon, string LangKey, System.Func<bool> IsVisible, ActionConsumable OnActivate);

    public GuiDialogScribeLectern(ICoreClientAPI capi, BlockEntityScribeLectern lectern, bool isEditorMode, byte[]? documentBytes)
        : base(Lang.Get("scribe:scribe-gui-title"), lectern.Pos, capi)
    {
        this.lectern = lectern;
        this.clientConfig = capi.LoadModConfig<ScribeClientConfig>(ScribeModSystem.ClientConfigFileName) ?? new ScribeClientConfig();

        // Clamp a pre-existing saved value into the current range -- a config saved before
        // these bounds were introduced (or before they were changed) could fall outside them.
        clientConfig.TextSizeScale = System.Math.Clamp(clientConfig.TextSizeScale, clientConfig.MinTextSizePercent / 100f, clientConfig.MaxTextSizePercent / 100f);

        if (IsDuplicate) return;

        // -------------------------------------------------------------------------------------
        // TEMP SAMPLE SEED (row-list-rework S1): the read view now renders custom ScribeRowElements,
        // but there is no edit-in-place yet (S2) to author content on this Mac, so seed some junk
        // rows into an empty document to have something realistic to look at -- a mix of tasks and
        // notes plus one deliberately long line to stress wrapping/clipping. Purely a client-side
        // display convenience: it mutates only the locally-cached Document, never persists or sends
        // anything. DELETE this whole block (and SeedSampleContentIfEmpty below) once S2's editor
        // can create rows normally.
        SeedSampleContentIfEmpty();

        EnterMode(isEditorMode, documentBytes);

#if DEBUG
        RegisterDebugSliders();
#endif
    }

#if DEBUG
    /// <summary>Debug-only (see add-imgui-configlib-tuning design.md decisions 1-2 --
    /// excluded from Release builds by <c>Mod.csproj</c>'s Condition on the VSImGui
    /// <c>ItemGroup</c>, not just this preprocessor guard) live-tuning sliders for the
    /// row-list layout fields most relevant to diagnosing rendering issues. Each
    /// <c>FloatSlider</c> call registers a persistent entry drawn automatically by
    /// VSImGui's own always-on debug-window handler (confirmed via decompile -- no manual
    /// per-frame draw call or event subscription needed here); the returned ids are
    /// tracked so <see cref="OnGuiClosed"/> can remove them via
    /// <see cref="VSImGui.Debug.DebugWidgets.Remove"/>, since a closed dialog's
    /// <c>clientConfig</c> instance would otherwise leave stale getter/setter closures
    /// registered against a dialog that no longer exists.</summary>
    private readonly System.Collections.Generic.List<int> debugSliderIds = new();

    private const string DebugDomain = "scribe";
    private const string DebugCategory = "Lectern Layout";

    private void RegisterDebugSliders()
    {
        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.FloatSlider(
            DebugDomain, DebugCategory, "Visible List Height", 100f, 1000f,
            () => (float)clientConfig.VisibleListHeight,
            value => { clientConfig.VisibleListHeight = value; RequestRecompose(); }));

        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.FloatSlider(
            DebugDomain, DebugCategory, "Row Spacing", 0f, 60f,
            () => (float)clientConfig.RowSpacing,
            value => { clientConfig.RowSpacing = value; RequestRecompose(); }));

        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.FloatSlider(
            DebugDomain, DebugCategory, "Top Content Gap", 0f, 100f,
            () => (float)clientConfig.TopContentGap,
            value => { clientConfig.TopContentGap = value; RequestRecompose(); }));

        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.FloatSlider(
            DebugDomain, DebugCategory, "Read List Width", 100f, 800f,
            () => (float)clientConfig.ReadListWidth,
            value => { clientConfig.ReadListWidth = value; RequestRecompose(); }));

        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.FloatSlider(
            DebugDomain, DebugCategory, "Editor List Width", 100f, 800f,
            () => (float)clientConfig.EditorListWidth,
            value => { clientConfig.EditorListWidth = value; RequestRecompose(); }));

        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.FloatSlider(
            DebugDomain, DebugCategory, "Row Divider Thickness", 0f, 10f,
            () => (float)clientConfig.RowDividerThickness,
            value => { clientConfig.RowDividerThickness = value; RequestRecompose(); }));

        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.FloatSlider(
            DebugDomain, DebugCategory, "Row Divider Brightness", 0f, 1f,
            () => clientConfig.RowDividerBrightness,
            value => { clientConfig.RowDividerBrightness = value; RequestRecompose(); }));

        debugSliderIds.Add(VSImGui.Debug.DebugWidgets.Button(DebugDomain, DebugCategory,
            "Save to scribe-client-config.json",
            () => capi.StoreModConfig(clientConfig, ScribeModSystem.ClientConfigFileName)));
    }

    private void UnregisterDebugSliders()
    {
        foreach (int id in debugSliderIds)
        {
            VSImGui.Debug.DebugWidgets.Remove(DebugDomain, id);
        }
        debugSliderIds.Clear();
    }
#endif

    /// <summary>TEMP (row-list-rework S1): seeds sample rows into an empty local document so the
    /// custom read view has content to render before S2's edit-in-place exists. See the call site
    /// in the constructor; delete both together when S2 lands. Client-only: never persisted or sent.</summary>
    private void SeedSampleContentIfEmpty()
    {
        var doc = lectern.Document;
        if (doc.Blocks.Count > 0) return;

        doc.AddTask("Gather 32 firewood");
        doc.AddTask("Smelt copper for a pickaxe");
        doc.ToggleTask(1);
        doc.AddTextSection("Remember: the abandoned mineshaft is two ridges east, past the birch grove.");
        doc.AddTask("Trade rusty gears at the trader before winter");
        doc.AddTextSection("A deliberately long note to stress text wrapping and boundary clipping: the quick brown fox jumps over the lazy dog, then wanders off to find enough sailcloth, resin, and iron bloom to finish the second floor of the cabin before the first snow.");
        doc.AddTask("Plant flax near the water");
        doc.AddTask("Fire the clay bricks");
        doc.ToggleTask(7);
    }

    private CairoFont RowFont() =>
        CairoFont.TextInput().WithFontSize((float)(GuiStyle.NormalFontSize * clientConfig.TextSizeScale));

    private int TextSizePercent => (int)System.Math.Round(clientConfig.TextSizeScale * 100);

    /// <summary>Row gap scaled by the current text size, so spacing grows/shrinks with the rows
    /// (mirrors how <c>ScribeBlockRowCell.RowHeight</c> scales row height). The config value
    /// itself stays the unscaled base -- scaling here at the point of use, not by mutating the
    /// stored value, avoids compounding the scale on every recompose/reopen.</summary>
    private double ScaledRowSpacing => clientConfig.RowSpacing * clientConfig.TextSizeScale;

    /// <summary>Divider thickness scaled by the current text size, same reasoning as
    /// <see cref="ScaledRowSpacing"/>.</summary>
    private double ScaledRowDividerThickness => clientConfig.RowDividerThickness * clientConfig.TextSizeScale;

    /// <summary>The row list's content bounds (the single element every row is parented under)
    /// for whichever view is currently composed. Re-set at the top of each ComposeXxxView call;
    /// only one view is ever live at a time so one field suffices. No longer scroll-shifted --
    /// rows are composed at a viewport-relative Y (design.md Decision 4, third correction), so
    /// this is now only a non-null "a row list has been composed" guard for
    /// <see cref="OnRowListScroll"/>.</summary>
    private ElementBounds? rowListContentBounds;

    /// <summary>The row list's current scroll offset, in the same units <c>AddVerticalScrollbar</c>
    /// reports. Read by ComposeReadView/ComposeEditorView at the start of every compose (not just
    /// the first) so a culling-triggered recompose (see <see cref="OnRowListScroll"/>) preserves
    /// scroll position instead of snapping back to the top. Reset to 0 in <see cref="EnterMode"/>
    /// since opening a dialog or switching view mode should start scrolled to the top.</summary>
    private double rowListScrollValue;

    /// <summary>The pixel Y-range (in row-list content coordinates) for which rows are currently
    /// composed -- set at the end of each ComposeXxxView call from the same buffered window used
    /// to decide which rows to add. <see cref="OnRowListScroll"/> only recomposes once the live
    /// scroll viewport escapes this range.</summary>
    private double rowListComposedRangeTop;
    private double rowListComposedRangeBottom;

    /// <summary>The contiguous block-index range actually composed (added to the composer) on the
    /// last ComposeEditorView pass -- both -1 if none (e.g. an empty document). Rows outside this
    /// range have no live elements, so <c>ApplyValues</c> and <c>HitTestRowIndex</c>'s fallback
    /// must not assume every block index has one.</summary>
    private int rowListComposedFirstIndex = -1;
    private int rowListComposedLastIndex = -1;

    /// <summary>Suppresses <see cref="OnRowListScroll"/>'s recompose-triggering logic while a
    /// ComposeXxxView call is already in progress. <c>AddVerticalScrollbar(...).SetHeights(...)</c>
    /// always fires this callback synchronously with a freshly-constructed scrollbar's value (0),
    /// regardless of the real scroll position being restored right after -- without this guard,
    /// that transient 0 would immediately trigger a second, unnecessary (and endlessly
    /// re-entrant, since the recursive compose call would trigger it again) recompose.</summary>
    private bool isComposingRowList;

    /// <summary>How far beyond the visible viewport, in each direction, rows stay composed once
    /// added. MUST be 0 -- a nonzero buffer was tried first and rejected: any row inside the
    /// buffer zone still renders at its true, unclipped position with nothing else stopping it
    /// (rows are viewport-culled rather than relying on <c>BeginClip</c>/<c>PushScissor</c> to
    /// visually hide overflow -- confirmed against real vsapi source that the engine's scissor
    /// does not clip this row list's rendering at all; see design.md's Decision 4 correction).
    /// With any buffer greater than zero, a buffered-but-off-screen row renders wherever its
    /// computed position lands, independent of the dialog's own drawn background box -- confirmed
    /// live: <c>screenshots/debug/2026-07-18_22-20-31_scroll-retest.png</c> shows a row rendering
    /// past "Done Editing", directly on top of the world behind the dialog. Zero is the only value
    /// that actually guarantees "nothing renders outside the box" -- accept the tradeoff that
    /// every scroll tick (not just each time the visible set changes) triggers a recompose; this
    /// dialog already recomposes on other frequent interactions (toggle, delete, drag-reorder)
    /// with no observed cost.</summary>
    private const double RowListCullBuffer = 0;

    /// <summary>Set by any callback that determines a recompose is needed, but must not act on
    /// it synchronously -- drained on the next <see cref="OnRenderGUI"/> tick instead. Every
    /// button/toggle/switch click handler in this dialog (<c>GuiElementToggleButton</c>,
    /// <c>GuiElementTextButton</c>, <c>GuiElementSwitch</c>) fires its callback from inside
    /// <c>OnMouseDownOnElement</c>/<c>OnMouseUpOnElement</c>, which <c>GuiComposer</c> calls from
    /// its own <c>OnMouseDown</c>/<c>OnMouseUp</c>/<c>OnKeyDown</c> while those methods are still
    /// iterating their own <c>interactiveElements</c> collection -- tearing down and replacing
    /// <c>SingleComposer</c> mid-iteration corrupts that in-progress dispatch. Confirmed two ways:
    /// live (a row rendered detached above the title bar, from a build that recomposed
    /// synchronously from the scrollbar's scroll callback) and via the client crash log itself
    /// (a <c>GuiElementDialogBackground</c> blur exception with stack
    /// <c>OnClickAddTask</c> &lt;- <c>GuiElementToggleButton.OnMouseDownOnElement</c> &lt;-
    /// <c>GuiComposer.OnMouseDown</c> -- the exact same reentrancy class, via the Add Task button
    /// rather than the scrollbar). Originally scoped to just the scrollbar's callback
    /// (<c>rowListRecomposePending</c>); generalized to every mid-dispatch recompose site
    /// (<see cref="OnClickAddTask"/>, <see cref="OnClickToggleToolPanel"/>,
    /// <see cref="OnRowDelete"/>, <see cref="OnRowTextChanged"/>, <see cref="OnClickSwitchToRead"/>)
    /// once each was found to share the identical hazard. Mirrors
    /// <see cref="textSizePendingRecompose"/>'s existing, already-proven defer-to-next-safe-point
    /// pattern for the same class of problem, generalized to <c>OnRenderGUI</c> rather than
    /// <c>OnMouseUp</c> alone since mouse-wheel scrolling (and most of these button clicks) have
    /// no "mouse up" to hook that fires after the dispatch loop completes. NOT used by the two
    /// call sites inside this dialog's own <see cref="OnMouseUp"/> override (drag-reorder,
    /// text-size-slider) -- those run AFTER <c>base.OnMouseUp(args)</c> has already returned, so
    /// the composer-level dispatch loop has already finished and a direct, synchronous recompose
    /// there is provably safe.</summary>
    private System.Action? pendingRecomposeAction;

    private void RequestRecompose()
    {
        pendingRecomposeAction = RecomposeCurrentView;
    }

    /// <summary>Recomposes whichever view is currently live, preserving typing focus in the
    /// editor view. The single place that knows how to rebuild "the current view" -- shared by
    /// <see cref="RequestRecompose"/>'s deferred path and <see cref="OnMouseUp"/>'s
    /// scroll-drag-release path.</summary>
    private void RecomposeCurrentView()
    {
        if (IsEditorMode) RecomposeEditorViewPreservingFocus();
        else ComposeReadView();
    }

    /// <summary>When a thumb-drag triggers a recompose, the drag's grab offset
    /// (<c>GuiElementScrollbar.mouseDownStartY</c>) captured just before the recompose, so the
    /// freshly-composed scrollbar can be told the drag is still in progress -- see
    /// <see cref="OnRowListScroll"/> and the compose-tail restore. Null when no drag handoff is
    /// pending.</summary>
    private int? rowListDragHandoff;

    private void OnRowListScroll(float value)
    {
        if (rowListContentBounds is null || isComposingRowList) return;

        rowListScrollValue = value;

        // Read view (row-list-rework S1): rows are custom ScribeRowElements drawn in the
        // interactive pass, which the BeginClip scissor clips natively. Scrolling is therefore the
        // plain vanilla idiom -- shift the content parent's fixedY and recalc; every row's renderY
        // (and its hit-test absY) follows in one call, with no cull, no recompose, and no
        // drag-handoff needed (all of which the editor branch below still needs until S2 reworks
        // it too). This is the whole payoff of the rework: smooth sub-pixel scrolling for free.
        if (!IsEditorMode)
        {
            rowListContentBounds.fixedY = 0 - value;
            rowListContentBounds.CalcWorldBounds();
            return;
        }

        // Editor view (unchanged, pre-rework path): rows are baked at a viewport-relative Y (see
        // ComposeEditorView pass 2), so visual movement on scroll comes only from recomposing at
        // the new scroll value -- there is no parent fixedY shift to nudge live (design.md
        // Decision 4, third correction). With RowListCullBuffer == 0 the composed range is exactly
        // the visible window, so any scroll movement escapes it and needs a recompose. (S2 will
        // move the editor view onto the read view's native-clip path above and delete this.)
        double viewportTop = value;
        double viewportBottom = value + clientConfig.VisibleListHeight;
        if (viewportTop < rowListComposedRangeTop || viewportBottom > rowListComposedRangeBottom)
        {
            // A thumb-drag is a sustained gesture across frames: the recompose rebuilds
            // SingleComposer including a brand-new scrollbar that never saw the mouse-down, so
            // without intervention the drag would orphan after one step (design.md Decision 4,
            // fourth correction). Rather than defer the whole recompose to mouse-up (which left
            // the rows frozen until release -- playtest feedback 2026-07-20), recompose every
            // frame via the normal next-frame OnRenderGUI path so the rows track the thumb
            // smoothly, and HAND THE GRAB OFF to the new scrollbar: capture the drag offset here,
            // reapply it (and mouseDownOnScrollbarHandle) at the compose tail. The mouse button
            // never physically came up, so the engine keeps sending OnMouseMove; the new
            // scrollbar, now aware it's mid-drag, keeps responding. Mouse-wheel scrolls aren't a
            // held gesture (mouseDownOnScrollbarHandle false), so nothing is captured and they
            // just recompose normally.
            var scrollbar = SingleComposer.GetScrollbar("rowListScrollbar");
            if (scrollbar is { mouseDownOnScrollbarHandle: true })
            {
                rowListDragHandoff = scrollbar.mouseDownStartY;
            }
            RequestRecompose();
        }
    }

    public override void OnRenderGUI(float deltaTime)
    {
        if (pendingRecomposeAction is { } action)
        {
            pendingRecomposeAction = null;
            action();
        }

        base.OnRenderGUI(deltaTime);
    }

    /// <summary>
    /// The base <c>GuiDialogBlockEntity.OnFinalizeFrame</c> auto-closes the dialog (and, via our
    /// <see cref="OnGuiClosed"/>, flushes the pending edit and releases the lock -- task 7.8) once
    /// the player leaves <c>IsInRangeOfBlock</c>. But the base measures against the player's
    /// <c>WorldData.PickingRange</c>, which the engine inflates to ~100 blocks in Creative mode
    /// (the <c>EnumGameMode</c> switch sets <c>PickingRange = PreviousPickingRange</c>, default
    /// 100). A creative player -- e.g. anyone who just placed the lectern from the creative
    /// inventory -- could therefore walk hundreds of blocks and the dialog would never close.
    /// Gate on the fixed survival interaction distance instead so walk-away auto-close fires in
    /// every game mode. This mirrors the base's exact selection-box distance math (confirmed via
    /// decompile), swapping only the mode-dependent range for <c>DefaultPickingRange</c>.
    /// </summary>
    public override bool IsInRangeOfBlock(BlockPos blockEntityPos)
    {
        Cuboidf[]? selectionBoxes = capi.World.BlockAccessor.GetBlock(blockEntityPos)
            .GetSelectionBoxes(capi.World.BlockAccessor, blockEntityPos);
        Vec3d eyePos = capi.World.Player.Entity.Pos.XYZ.Add(capi.World.Player.Entity.LocalEyePos);

        double nearest = 99.0;
        if (selectionBoxes != null)
        {
            foreach (Cuboidf box in selectionBoxes)
            {
                nearest = System.Math.Min(nearest, box.ToDouble()
                    .Translate(blockEntityPos.X, blockEntityPos.InternalY, blockEntityPos.Z)
                    .ShortestDistanceFrom(eyePos));
            }
        }

        return nearest <= GlobalConstants.DefaultPickingRange + 0.5;
    }

    /// <summary>Set while a text-size drag is pending a recompose (see <see cref="OnMouseUp"/>).
    /// Recomposing rebuilds the slider element from scratch, which would discard its own
    /// in-progress-drag state (<c>GuiElementSlider</c> has no public way to defer its own change
    /// callback to mouse-up, so this dialog does it instead) -- without deferring, every
    /// intermediate value during a single drag tears down and rebuilds a fresh slider that never
    /// saw the mouse-down, ending the drag after one step.</summary>
    private bool textSizePendingRecompose;

    private bool OnTextSizeSliderChanged(int percent)
    {
        clientConfig.TextSizeScale = percent / 100f;
        textSizePendingRecompose = true;
        return true;
    }

    /// <summary>
    /// Called by <see cref="BlockEntityScribeLectern.HandleServerReply"/> when a mode-switch
    /// request is granted for an already-open dialog. Also used internally by the constructor
    /// for the initial mode.
    /// </summary>
    public void SwitchMode(bool editorMode, byte[]? documentBytes)
    {
        EnterMode(editorMode, documentBytes);
    }

    private void EnterMode(bool editorMode, byte[]? documentBytes)
    {
        StopAutosaveTick();
        IsEditorMode = editorMode;
        draggedBlockIndex = null;
        hoverTargetIndex = null;
        rowListScrollValue = 0;
        pendingRecomposeAction = null;
        rowListDragHandoff = null;

        if (editorMode)
        {
            scratchDocument = ScribeDocumentCodec.TryDeserialize(documentBytes, out var doc) && doc is not null
                ? doc
                : new ScribeDocument();
            isDirty = false;
            StartAutosaveTick();
            ComposeEditorView();
        }
        else
        {
            scratchDocument = null;
            ComposeReadView();
        }
    }

    /// <summary>Called by <see cref="BlockEntityScribeLectern.FromTreeAttributes"/> whenever the authoritative document changes; a no-op while in editor mode.</summary>
    public void RefreshReadView()
    {
        if (!IsEditorMode && IsOpened())
        {
            ComposeReadView();
        }
    }

    private ElementBounds DialogBounds() =>
        ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

    /// <summary>Placeholder skeuomorphic backdrop, replacing the generic
    /// <c>AddShadedDialogBG</c> panel (design.md decision 2/3). A single texture path is the
    /// entire swap point -- replacing this asset with real per-tier art (or repointing it to
    /// a different <see cref="AssetLocation"/> per tier later) needs no change to this file's
    /// layout/composition logic, satisfying `specs/lectern-gui-shell/spec.md`'s "Backdrop is
    /// swappable" scenario. Uses the engine's own <c>AddImageBG</c> (a tiled/scaled Cairo
    /// `SurfacePattern` fill with rounded corners, confirmed via
    /// `GuiElementImageBackground.ComposeElements`) rather than a hand-rolled Cairo draw --
    /// same swappability, less code.</summary>
    private static readonly AssetLocation BackdropTexture = new("scribe:textures/gui/lecternbackdrop.png");

    /// <summary>Adds a subtle horizontal divider centered in the gap below a row, using the
    /// engine's own embossed-inset primitive (<c>AddInset</c>) rather than a hand-rolled Cairo
    /// draw -- a thin inset reads as a faint separator line, matching the Slack reference's
    /// light row dividers without inventing new rendering code. Thickness/brightness/the
    /// enclosing gap size are all config knobs (<see cref="ScribeClientConfig.RowSpacing"/>
    /// etc.) so the divider's look can be re-tuned without a rebuild.</summary>
    private void AddRowDivider(double y, double width)
    {
        var dividerBounds = ElementBounds.Fixed(0, y + ScaledRowSpacing / 2 - ScaledRowDividerThickness / 2, width, ScaledRowDividerThickness);
        SingleComposer.AddInset(dividerBounds, depth: 1, brightness: clientConfig.RowDividerBrightness);
    }

    // ---------------- Read view ----------------

    private void ComposeReadView()
    {
        var blocks = lectern.Document.Blocks;
        double rowSpacing = ScaledRowSpacing;
        double listWidth = clientConfig.ReadListWidth;
        double y = clientConfig.TopContentGap;

        var dialogBounds = DialogBounds();
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = Vintagestory.API.Client.ElementSizing.FitToChildren;

        // Pass 1: measure every row's position/height. Unlike the editor view (and the old read
        // view), the read view now draws each row as a ScribeRowElement in the interactive pass,
        // which the dialog's BeginClip scissor clips NATIVELY (row-list-rework S1) -- so there is
        // no viewport culling and no viewport-relative Y here: every row is composed once at its
        // absolute content Y, and scrolling is a plain parent-fixedY shift (see OnRowListScroll's
        // read-view branch and SetupRowListScrollbar). Row height = the wrapped text height plus
        // the ruling overhead the row draws around it (ScribeRowElement.RulingOverhead).
        var rowYs = new double[blocks.Count];
        var rowHeights = new double[blocks.Count];
        double contentY = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            // ScribeRowElement.RowHeightFixed is the single source of row height, shared with the
            // element's own drawing so the baked surface is always tall enough for the text (it
            // handles the scaled-vs-fixed unit conversion that a naive measure got wrong -- see
            // its doc comment).
            rowYs[i] = contentY;
            rowHeights[i] = ScribeRowElement.RowHeightFixed(capi, blocks[i], listWidth, RowFont(), clientConfig);
            contentY += rowHeights[i] + rowSpacing;
        }

        double hintHeight = 0;
        if (blocks.Count == 0)
        {
            hintHeight = ScribeBlockRowCell.MeasureWrappedHeight(capi, Lang.Get("scribe:scribe-gui-edit-hint"), RowFont(), listWidth, clientConfig.ControlRowHeight);
            contentY = hintHeight + rowSpacing;
        }

        // Clamp the restored scroll position against the real content height, so a document that
        // shrank while scrolled down (e.g. a task synced away) doesn't stay scrolled past the end.
        rowListScrollValue = System.Math.Clamp(rowListScrollValue, 0, System.Math.Max(0, contentY - clientConfig.VisibleListHeight));

        // The row list lives inside a fixed-height clipped region so a long document scrolls
        // instead of growing the dialog -- see VSAPI-NOTES.md for the BeginClip/AddVerticalScrollbar
        // idiom. contentBounds is the single parent every row is added under; the read-view branch
        // of OnRowListScroll shifts its fixedY on scroll and the BeginClip scissor clips the
        // interactive-pass row textures natively (no cull, no recompose-on-scroll).
        var clipBounds = ElementBounds.Fixed(0, y, listWidth, clientConfig.VisibleListHeight);
        var scrollbarBounds = ElementStdBounds.VerticalScrollbar(clipBounds);
        var contentBounds = ElementBounds.Fixed(0, 0, listWidth, contentY);

        SingleComposer = capi.Gui.CreateCompo("scribeLectern", dialogBounds)
            .AddImageBG(bgBounds, BackdropTexture)
            .AddDialogTitleBar(Lang.Get("scribe:scribe-gui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .BeginClip(clipBounds)
                .BeginChildElements(contentBounds);

        // Compose every row at its absolute content Y as a custom ScribeRowElement. The element
        // bakes its own texture (checkbox glyph + text + lined-paper ruling) and blits it in the
        // interactive pass, where the clip scissor applies -- so a row at the scroll boundary is
        // drawn partially clipped rather than popping in/out, and rows past the edge are hidden by
        // the scissor, not by never composing them.
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var rowBounds = ElementBounds.Fixed(0, rowYs[i], listWidth, rowHeights[i]);

            SingleComposer.AddInteractiveElement(
                new ScribeRowElement(
                    capi,
                    rowBounds,
                    ScribeRowMode.Read,
                    blockIndex: i,
                    isTask: block.IsTask,
                    done: block.Done,
                    text: block.Text,
                    font: RowFont(),
                    config: clientConfig,
                    onToggleClicked: block.IsTask ? OnReadViewToggleTask : null),
                ScribeBlockRowCell.TextKey(i));
        }

        if (blocks.Count == 0)
        {
            SingleComposer.AddStaticText(Lang.Get("scribe:scribe-gui-edit-hint"), RowFont(), ElementBounds.Fixed(0, 0, listWidth, hintHeight));
        }

        rowListContentBounds = contentBounds;

        SingleComposer
                .EndChildElements()
            .EndClip()
            .AddInteractiveElement(new ScribeRowListScrollbar(capi, OnRowListScroll, scrollbarBounds), "rowListScrollbar");

        y += clientConfig.VisibleListHeight + rowSpacing;

        var switchBounds = ElementBounds.Fixed(0, y, listWidth, clientConfig.ControlRowHeight);
        SingleComposer.AddSmallButton(Lang.Get("scribe:scribe-gui-switch-to-editor"), OnClickSwitchToEditor, switchBounds, key: "switchModeButton");

        SingleComposer.EndChildElements().Compose();

        SetupRowListScrollbar(contentY);

        // Native clipping means scrolling is a parent-fixedY shift; apply the restored scroll
        // position now (SetupRowListScrollbar set the thumb but fires no scroll callback), so a
        // recompose triggered by a live document resync keeps its scroll offset.
        rowListContentBounds.fixedY = 0 - rowListScrollValue;
        rowListContentBounds.CalcWorldBounds();
    }

    /// <summary>Read-view task checkbox click: fire-and-forget a lock-free toggle to the server.
    /// The read view holds no editor lock (see BlockEntityScribeLectern), so this uses the
    /// dedicated <see cref="ScribeToggleTaskMessage"/> rather than the lock-gated edit path. No
    /// optimistic local mutation -- the server applies it and re-syncs, and FromTreeAttributes ->
    /// RefreshReadView recomposes with the new state.</summary>
    private void OnReadViewToggleTask(int index)
    {
        capi.Network.GetChannel(ScribeModSystem.NetworkChannelName).SendPacket(new ScribeToggleTaskMessage
        {
            PosX = lectern.Pos.X,
            PosY = lectern.Pos.Y,
            PosZ = lectern.Pos.Z,
            BlockIndex = index,
        });
    }

    /// <summary>Post-Compose scrollbar wiring shared by both views. Sets the mouse-wheel step to
    /// one task-row height (so a notch scrolls one row, not the engine default ~2 -- playtest
    /// feedback 2026-07-20), tells the scrollbar its visible/total heights, restores the real
    /// scroll position, and -- if a thumb-drag was in progress when this recompose was triggered
    /// -- hands the drag grab off to this freshly composed scrollbar so the gesture survives the
    /// rebuild (see <see cref="rowListDragHandoff"/>/<see cref="OnRowListScroll"/>).</summary>
    private void SetupRowListScrollbar(double contentY)
    {
        // SetHeights synchronously fires OnRowListScroll(0) via the scrollbar's own
        // change-notify plumbing (GuiElementScrollbar.SetNewTotalHeight -> TriggerChanged),
        // regardless of the real scroll position -- isComposingRowList suppresses that spurious
        // call so it can't snap the list back to the top or trigger a re-entrant recompose. The
        // real scroll position is reapplied right after via CurrentYPosition's public setter (no
        // callback re-entrancy) so the thumb sits at the right spot.
        //
        // This method only positions the THUMB; it does not itself shift the content parent. The
        // two views then differ on how the rows follow (row-list-rework S1): the EDITOR view leaves
        // fixedY at 0 and bakes rows at a viewport-relative Y (a parent shift on top of that would
        // double-offset them -- design.md Decision 4, third correction), whereas the READ view sets
        // rowListContentBounds.fixedY = -scrollValue itself right after calling this (native-clip
        // path), since its ScribeRowElements are composed at absolute Y. Keep that read-view shift
        // at the ComposeReadView call site, not here, so this shared helper stays view-agnostic.
        isComposingRowList = true;
        var scrollbar = (ScribeRowListScrollbar)SingleComposer.GetScrollbar("rowListScrollbar");
        scrollbar.RowStep = clientConfig.TaskRowHeight * clientConfig.TextSizeScale;
        scrollbar.SetHeights((float)clientConfig.VisibleListHeight, (float)System.Math.Max(clientConfig.VisibleListHeight, contentY));
        scrollbar.CurrentYPosition = (float)rowListScrollValue;

        // Reattach an in-progress thumb-drag to this new scrollbar element (see OnRowListScroll):
        // mark it as being dragged and restore the same grab offset, so the engine's ongoing
        // OnMouseMove stream keeps driving it instead of the drag dying with the old element.
        if (rowListDragHandoff is { } grabOffset)
        {
            scrollbar.mouseDownOnScrollbarHandle = true;
            scrollbar.mouseDownStartY = grabOffset;
            rowListDragHandoff = null;
        }

        isComposingRowList = false;
    }

    private bool OnClickSwitchToEditor()
    {
        capi.Network.GetChannel(ScribeModSystem.NetworkChannelName).SendPacket(new ScribeRequestAccessMessage
        {
            PosX = lectern.Pos.X,
            PosY = lectern.Pos.Y,
            PosZ = lectern.Pos.Z,
            WantEditor = true,
        });
        return true;
    }

    // ---------------- Editor view ----------------

    private void ComposeEditorView()
    {
        if (scratchDocument is null) return;

        double rowSpacing = ScaledRowSpacing;
        double listWidth = clientConfig.EditorListWidth;
        double y = clientConfig.TopContentGap;

        var dialogBounds = DialogBounds();
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = Vintagestory.API.Client.ElementSizing.FitToChildren;

        // See the matching comment in ComposeReadView for the clip/scrollbar idiom, and for why
        // BeginClip/PushScissor doesn't visually clip this list on its own (RowListCullBuffer's
        // doc comment) -- same two-pass measure-then-cull structure here, just with
        // ScribeBlockRowCell.Compose's multi-element rows instead of a single AddStaticText per
        // row.
        var blocks = scratchDocument.Blocks;
        composedNoteRowHeights.Clear();

        // Pass 1: measure every row's position/height regardless of scroll position -- same
        // reasoning as ComposeReadView's pass 1.
        var rowYs = new double[blocks.Count];
        var rowHeights = new double[blocks.Count];
        double contentY = 0;
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            double minHeight = ScribeBlockRowCell.RowHeight(block, clientConfig);

            // Task rows (GuiElementTextInput) are single-line by design and never wrap, so the
            // fixed height is correct as-is. Text-section rows (GuiElementTextArea) DO wrap and
            // grow past this height the moment ApplyValues seeds their text below -- measure
            // ahead so later rows lay out at the height the text area will actually end up
            // being, not the pre-growth constant (confirmed live: an unmeasured long note
            // overlaps "Text Size"/"Collapse" below it). Recorded per-index so OnRowTextChanged
            // can tell whether a live edit has changed the wrapped height enough to need a
            // recompose (see OnRowTextChanged). Recorded for every row regardless of whether
            // pass 2 below actually composes it, so a note scrolled out of view still reports
            // its real height if scrolled back into view without an intervening edit.
            double rowHeight = minHeight;
            if (!block.IsTask)
            {
                double textWidth = ScribeBlockRowCell.TextWidth(listWidth, isTask: false, showDragHandle: true, clientConfig);
                rowHeight = ScribeBlockRowCell.MeasureWrappedHeight(capi, block.Text, RowFont(), textWidth, minHeight);
                composedNoteRowHeights[i] = rowHeight;
            }

            rowYs[i] = contentY;
            rowHeights[i] = rowHeight;
            contentY += rowHeight + rowSpacing;
        }

        rowListScrollValue = System.Math.Clamp(rowListScrollValue, 0, System.Math.Max(0, contentY - clientConfig.VisibleListHeight));
        double windowTop = System.Math.Max(0, rowListScrollValue - RowListCullBuffer);
        double windowBottom = rowListScrollValue + clientConfig.VisibleListHeight + RowListCullBuffer;

        var clipBounds = ElementBounds.Fixed(0, y, listWidth, clientConfig.VisibleListHeight);
        var scrollbarBounds = ElementStdBounds.VerticalScrollbar(clipBounds);
        var contentBounds = ElementBounds.Fixed(0, 0, listWidth, contentY);

        SingleComposer = capi.Gui.CreateCompo("scribeLectern", dialogBounds)
            .AddImageBG(bgBounds, BackdropTexture)
            .AddDialogTitleBar(Lang.Get("scribe:scribe-gui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .BeginClip(clipBounds)
                .BeginChildElements(contentBounds);

        // Pass 2: only compose rows (and their dividers) FULLY CONTAINED within the buffered
        // window -- see the matching comment in ComposeReadView for why full containment
        // (not mere overlap) is required now that nothing here visually clips a composed
        // row's rendering. rowListComposedFirstIndex/LastIndex record the actual composed
        // range so ApplyValues (below) and HitTestRowIndex (drag-reorder) know which indices
        // have live elements -- an index outside this range was never added to the composer
        // this pass. Note: a single row taller than VisibleListHeight itself (an unusually
        // long note at a large text-size scale) can never be fully contained at any scroll
        // position and will never render -- an inherent limit of cull-don't-clip, not
        // something this pass introduces; showing it would need real clipping, which is
        // confirmed unavailable here (see RowListCullBuffer's doc comment).
        rowListComposedFirstIndex = -1;
        rowListComposedLastIndex = -1;
        for (int i = 0; i < blocks.Count; i++)
        {
            double rowTop = rowYs[i];
            double rowBottom = rowTop + rowHeights[i];
            if (rowTop < windowTop || rowBottom > windowBottom) continue;

            // Viewport-relative Y (rowTop - scroll), same as ComposeReadView pass 2 -- both
            // render passes must bake at the scrolled coordinate. A mixed static+interactive row
            // (text-box border is static, its text content interactive; the checkbox outline is
            // static, its check interactive) would otherwise scroll only its interactive halves
            // and leave the chrome frozen (design.md Decision 4, third correction). Culling
            // guarantees rowTop >= scroll, so this is always >= 0.
            double rowDrawTop = rowTop - rowListScrollValue;
            var rowBounds = ElementBounds.Fixed(0, rowDrawTop, listWidth, rowHeights[i]);

            ScribeBlockRowCell.Compose(
                SingleComposer,
                blocks[i],
                i,
                rowBounds,
                RowFont(),
                showDragHandle: true,
                OnRowToggle,
                OnRowTextChanged,
                OnRowDelete,
                clientConfig,
                OnRowDragMouseDown,
                OnRowDragMouseUp,
                onTogglePin: OnRowTogglePin);

            if (i < blocks.Count - 1) AddRowDivider(rowDrawTop + rowHeights[i], listWidth);

            if (rowListComposedFirstIndex == -1) rowListComposedFirstIndex = i;
            rowListComposedLastIndex = i;
        }

        rowListContentBounds = contentBounds;
        rowListComposedRangeTop = windowTop;
        rowListComposedRangeBottom = windowBottom;

        SingleComposer
                .EndChildElements()
            .EndClip()
            .AddInteractiveElement(new ScribeRowListScrollbar(capi, OnRowListScroll, scrollbarBounds), "rowListScrollbar");

        y += clientConfig.VisibleListHeight + clientConfig.ListToControlsGap;
        SingleComposer.AddStaticText(Lang.Get("scribe:scribe-gui-textsize"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, clientConfig.TextSizeLabelWidth, clientConfig.ControlRowHeight));
        double sliderX = clientConfig.TextSizeLabelWidth + clientConfig.TextSizeLabelToSliderGap;
        SingleComposer.AddSlider(OnTextSizeSliderChanged, ElementBounds.Fixed(sliderX, y, listWidth - sliderX, clientConfig.ControlRowHeight), key: "textSizeSlider");
        SingleComposer.GetSlider("textSizeSlider").SetValues(TextSizePercent, clientConfig.MinTextSizePercent, clientConfig.MaxTextSizePercent, 10, "%");
        y += clientConfig.ControlRowGap;

        var toolPanelToggleBounds = ElementBounds.Fixed(0, y, clientConfig.ToolPanelToggleWidth, clientConfig.ControlRowHeight);
        string collapseLangKey = isToolPanelExpanded ? "scribe:scribe-gui-collapse" : "scribe:scribe-gui-expand";
        SingleComposer.AddSmallButton(Lang.Get(collapseLangKey), OnClickToggleToolPanel, toolPanelToggleBounds, key: "toolPanelToggleButton");
        y += clientConfig.ControlRowGap;

        if (isToolPanelExpanded)
        {
            double optionX = 0;
            foreach (var option in ToolbarOptions())
            {
                var optionBounds = ElementBounds.Fixed(optionX, y, clientConfig.ToolbarIconWidth, clientConfig.ToolbarIconHeight);
                SingleComposer.AddIf(option.IsVisible());
                SingleComposer.AddIconButton(option.Icon, _ => option.OnActivate(), optionBounds, key: option.Key);
                SingleComposer.AddHoverText(Lang.Get(option.LangKey), CairoFont.WhiteSmallText(), (int)clientConfig.HoverTextWidth, optionBounds.FlatCopy());
                SingleComposer.EndIf();
                optionX += clientConfig.ToolbarIconSpacing;
            }

            y += clientConfig.ControlRowGap;
        }

        var switchBounds = ElementBounds.Fixed(0, y, clientConfig.SwitchButtonWidth, clientConfig.ControlRowHeight);
        SingleComposer.AddSmallButton(Lang.Get("scribe:scribe-gui-switch-to-read"), OnClickSwitchToRead, switchBounds, key: "switchModeButton");

        SingleComposer.EndChildElements().Compose();

        SetupRowListScrollbar(contentY);

        // Seed row values (toggle state, text) only after Compose() has calculated real bounds --
        // see the doc comment on ScribeBlockRowCell.Compose for why doing this earlier corrupts
        // the text elements' auto-height calc and, transitively, the whole dialog's outer size.
        // Only rows actually composed this pass (rowListComposedFirstIndex..LastIndex) have live
        // elements to seed -- a culled-out index has none, and ApplyValues would throw on a
        // GetTextInput/GetTextArea call against a missing key.
        for (int i = rowListComposedFirstIndex; i != -1 && i <= rowListComposedLastIndex; i++)
        {
            ScribeBlockRowCell.ApplyValues(SingleComposer, blocks[i], i);
        }
    }

    private System.Collections.Generic.IEnumerable<ToolbarOption> ToolbarOptions()
    {
        yield return new ToolbarOption("addTaskButton", "plus", "scribe:scribe-gui-addtask", () => true, OnClickAddTask);
    }

    private bool OnClickToggleToolPanel()
    {
        isToolPanelExpanded = !isToolPanelExpanded;
        RequestRecompose();
        return true;
    }

    private void OnRowToggle(int index)
    {
        scratchDocument?.ToggleTask(index);
        isDirty = true;
    }

    private void OnRowTogglePin(int index)
    {
        scratchDocument?.TogglePinned(index);
        isDirty = true;
    }

    private void OnRowTextChanged(int index, string text)
    {
        scratchDocument?.SetBlockText(index, text);
        isDirty = true;

        // Recompose immediately when a note's wrapped height has changed -- otherwise the
        // textarea grows/shrinks its own box live (GuiElementTextArea.Autoheight) while every
        // row below it stays put until the next unrelated recompose, visibly overlapping
        // (confirmed live: screenshots/debug/2026-07-18_14-32-1[3-6]_editor-note-normalwords.png).
        // Scoped to notes only, since a task's GuiElementTextInput never wraps/grows.
        var block = scratchDocument?.Blocks[index];
        if (block is null || block.IsTask) return;

        double minHeight = ScribeBlockRowCell.RowHeight(block, clientConfig);
        double textWidth = ScribeBlockRowCell.TextWidth(clientConfig.EditorListWidth, isTask: false, showDragHandle: true, clientConfig);
        double newHeight = ScribeBlockRowCell.MeasureWrappedHeight(capi, text, RowFont(), textWidth, minHeight);

        if (composedNoteRowHeights.TryGetValue(index, out double composedHeight) && newHeight != composedHeight)
        {
            RequestRecompose();
        }
    }

    /// <summary>
    /// Recomposes the editor view without disturbing whichever text row the player is actively
    /// typing in -- a plain recompose (<c>GuiComposer.Compose()</c>, the default
    /// <c>focusFirstElement: true</c>) yanks focus/caret to row 0's element, which would make it
    /// impossible to keep typing past the point a note first wraps to a new line. Captures the
    /// focused row's key and caret position beforehand and restores both on the freshly composed
    /// element (a recompose creates brand-new <see cref="GuiElement"/> instances, so the old
    /// reference cannot simply be refocused).
    /// </summary>
    private void RecomposeEditorViewPreservingFocus()
    {
        int? focusedIndex = null;
        int caretPosInLine = 0;
        int caretPosLine = 0;

        var blocks = scratchDocument?.Blocks;
        if (blocks is not null)
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].IsTask) continue;

                var textArea = SingleComposer.GetTextArea(ScribeBlockRowCell.TextKey(i));
                if (textArea is { HasFocus: true })
                {
                    focusedIndex = i;
                    caretPosInLine = textArea.CaretPosInLine;
                    caretPosLine = textArea.CaretPosLine;
                    break;
                }
            }
        }

        ComposeEditorView();

        if (focusedIndex is { } index)
        {
            var textArea = SingleComposer.GetTextArea(ScribeBlockRowCell.TextKey(index));
            if (textArea is not null)
            {
                // FocusElement (not OnFocusGained directly) so the element-0 focus that
                // Compose()'s default focusFirstElement:true already applied gets properly
                // unfocused first -- otherwise two elements end up marked HasFocus at once.
                SingleComposer.FocusElement(textArea.TabIndex);
                textArea.SetCaretPos(caretPosInLine, caretPosLine);
            }
        }
    }

    private void OnRowDelete(int index)
    {
        scratchDocument?.DeleteBlock(index);
        isDirty = true;
        RequestRecompose();
    }

    private bool OnClickAddTask()
    {
        scratchDocument?.AddTask(Lang.Get("scribe:scribe-gui-newtask-placeholder"));
        isDirty = true;
        RequestRecompose();
        return true;
    }

    private bool OnClickSwitchToRead()
    {
        // Flush any pending edit BEFORE releasing the lock: the server processes messages in
        // send order, so releasing first would let the flushed edit arrive lock-less and be
        // silently rejected by ApplyEdit's lock check.
        FlushIfDirty();
        SendReleaseLockPacket();

        // EnterMode ends in a Compose call, so it shares the same mid-dispatch hazard as every
        // other button handler here (see pendingRecomposeAction's doc comment) -- this button's
        // own OnMouseUpOnElement fires it from inside GuiComposer.OnMouseUp's dispatch loop.
        // Deferred as a raw action (not via RequestRecompose, which only knows how to recompose
        // the CURRENT view) since this call also flips IsEditorMode itself.
        pendingRecomposeAction = () => EnterMode(false, null);
        return true;
    }

    // ---------------- Reorder (mouse-drag) ----------------

    private void OnRowDragMouseDown(int index, MouseEvent args)
    {
        draggedBlockIndex = index;
        hoverTargetIndex = index;
        args.Handled = true;
    }

    /// <summary>Wired to <see cref="ScribeDragHandleElement.OnDragMouseUp"/>, which only fires
    /// when the mouse-up lands within THIS row's own drag-handle bounds (checked by the
    /// element's own <c>IsPositionInside</c>, inherited from the base <c>GuiElement.OnMouseUp</c>).
    /// The dialog-level <see cref="OnMouseUp"/> below has no equivalent per-row bounds check --
    /// it only tracks <see cref="draggedBlockIndex"/>/<see cref="hoverTargetIndex"/> state, which
    /// persists regardless of exactly where the release landed -- so this can't be folded into
    /// it without duplicating that per-row hit-test. <c>ScribeDragHandleElement</c> is a minimal
    /// custom element (base <c>GuiElementStaticText</c>), not a real button/switch widget, so
    /// unlike those it does not mark <c>Handled</c> on its own; without this, releasing over the
    /// drag handle would leave the mouse-up unhandled and risk a click-through to world
    /// interaction (the same reason the title bar's own close icon explicitly sets
    /// <c>Handled = true</c> on its own hit, confirmed via decompile).</summary>
    private void OnRowDragMouseUp(int index, MouseEvent args)
    {
        args.Handled = true;
    }

    public override void OnMouseMove(MouseEvent args)
    {
        base.OnMouseMove(args);

        if (draggedBlockIndex is null || scratchDocument is null) return;

        int newTarget = HitTestRowIndex(args.Y);
        if (newTarget != hoverTargetIndex)
        {
            hoverTargetIndex = newTarget;
        }
    }

    public override void OnMouseUp(MouseEvent args)
    {
        base.OnMouseUp(args);

        if (textSizePendingRecompose)
        {
            textSizePendingRecompose = false;
            capi.StoreModConfig(clientConfig, ScribeModSystem.ClientConfigFileName);
            ComposeEditorView();
            return;
        }

        // The thumb-drag is over: base.OnMouseUp above cleared the current scrollbar's
        // mouseDownOnScrollbarHandle. Clear any not-yet-consumed drag handoff too, so a recompose
        // still queued from the drag's last frame (see OnRowListScroll) doesn't re-grab the new
        // scrollbar after the button is already up. The queued recompose still runs and bakes the
        // rows at the final scroll position; it just won't reattach a drag that has ended.
        rowListDragHandoff = null;

        if (draggedBlockIndex is null || scratchDocument is null)
        {
            draggedBlockIndex = null;
            hoverTargetIndex = null;
            return;
        }

        int from = draggedBlockIndex.Value;
        int to = hoverTargetIndex ?? from;

        draggedBlockIndex = null;
        hoverTargetIndex = null;

        if (from != to)
        {
            scratchDocument.MoveBlock(from, to);
            isDirty = true;
            ComposeEditorView();
        }
    }

    private int HitTestRowIndex(int mouseY)
    {
        if (scratchDocument is null || scratchDocument.Blocks.Count == 0) return 0;

        // Row keys are laid out in the same order as scratchDocument.Blocks; look up each
        // row's live bounds by key rather than recomputing layout math here. Task rows key a
        // GuiElementTextInput, text-section rows a GuiElementTextArea -- GetElement avoids
        // assuming either kind (GetTextInput's cast throws on a text-section row). A
        // viewport-culled-out row has no element under its key at all (see
        // rowListComposedFirstIndex/LastIndex), which this loop already tolerates via the
        // null-bounds `continue` below -- it was originally written for "not yet Compose()'d
        // this frame", but the same check happens to cover "culled out entirely" too.
        for (int i = 0; i < scratchDocument.Blocks.Count; i++)
        {
            var bounds = SingleComposer.GetElement(ScribeBlockRowCell.TextKey(i))?.Bounds;
            if (bounds is null) continue;

            double midY = bounds.absY + bounds.OuterHeight / 2;
            if (mouseY < midY) return i;
        }

        // Falls through when the mouse is below every composed row's midpoint -- e.g. dragging
        // past the bottom of the currently-culled window. rowListComposedLastIndex (the last
        // index that actually has live elements this pass) is the correct target, not
        // Blocks.Count - 1: with culling, the last block may not be the last *composed* row.
        return rowListComposedLastIndex != -1 ? rowListComposedLastIndex : 0;
    }

    // ---------------- Autosave (throttled) ----------------

    private void StartAutosaveTick()
    {
        autosaveTickListenerId = capi.Event.RegisterGameTickListener(OnAutosaveTick, 1000);
    }

    private void StopAutosaveTick()
    {
        if (autosaveTickListenerId is { } id)
        {
            capi.Event.UnregisterGameTickListener(id);
            autosaveTickListenerId = null;
        }
    }

    private void OnAutosaveTick(float deltaTime)
    {
        FlushIfDirty();
    }

    private void FlushIfDirty()
    {
        if (!isDirty || scratchDocument is null) return;

        var bytes = ScribeDocumentCodec.Serialize(scratchDocument);
        capi.Network.GetChannel(ScribeModSystem.NetworkChannelName).SendPacket(new ScribeEditDocumentMessage
        {
            PosX = lectern.Pos.X,
            PosY = lectern.Pos.Y,
            PosZ = lectern.Pos.Z,
            DocumentBytes = bytes,
        });
        isDirty = false;

        // The authoritative resync for this edit is still in flight — update the local cache
        // now so an immediate switch-to-read doesn't flash the pre-edit content in between. A
        // fresh deserialize (not the live scratchDocument reference) keeps it un-aliased, since
        // scratchDocument keeps mutating while editor mode continues.
        if (ScribeDocumentCodec.TryDeserialize(bytes, out var copy) && copy is not null)
        {
            lectern.ApplyLocalOptimisticEdit(copy);
        }
    }

    // ---------------- Lifecycle ----------------

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();

        if (IsEditorMode)
        {
            FlushIfDirty();
            SendReleaseLockPacket();
        }

        StopAutosaveTick();

#if DEBUG
        UnregisterDebugSliders();
#endif
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private void SendReleaseLockPacket()
    {
        capi.Network.GetChannel(ScribeModSystem.NetworkChannelName).SendPacket(new ScribeReleaseLockMessage
        {
            PosX = lectern.Pos.X,
            PosY = lectern.Pos.Y,
            PosZ = lectern.Pos.Z,
        });
    }
}
