using System.Linq;
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
/// A <c>GuiDialogBlockEntity</c> rather than a plain <c>GuiDialog</c>, so the engine's own
/// per-block-position dialog dedup and walk-away auto-close apply for free.
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

        // Clamp a pre-existing saved value down to the current cap -- a config saved before
        // MaxTextSizePercent was introduced (or before it was lowered) could exceed it.
        clientConfig.TextSizeScale = System.Math.Clamp(clientConfig.TextSizeScale, 0.5f, MaxTextSizePercent / 100f);

        if (IsDuplicate) return;

        EnterMode(isEditorMode, documentBytes);
    }

    private CairoFont RowFont() =>
        CairoFont.TextInput().WithFontSize((float)(GuiStyle.NormalFontSize * clientConfig.TextSizeScale));

    private int TextSizePercent => (int)System.Math.Round(clientConfig.TextSizeScale * 100);

    /// <summary>Upper bound for the text-size slider. There's no scrollable/clipped region in
    /// this dialog -- rows are stacked by absolute Y with no scrollbar -- so past a certain
    /// scale + block count, content renders below the screen with no way to reach it (confirmed
    /// live at the slider's old 200% max). Capping here is a stopgap; real scrolling support is
    /// tracked as a follow-up (tasks.md 8.14).</summary>
    private const int MaxTextSizePercent = 150;

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

    /// <summary>Vertical gap between the title bar and the first row -- the title bar takes no
    /// space of its own within <c>BeginChildElements</c>, so content starting at y=0 renders
    /// flush against it. Shared by both views, so a single bump here fixes the gap everywhere
    /// the row stack starts.</summary>
    private const double TopContentGap = 20;

    // ---------------- Read view ----------------

    private void ComposeReadView()
    {
        var blocks = lectern.Document.Blocks;
        double rowSpacing = 6;
        double listWidth = 480;
        double y = TopContentGap;

        var dialogBounds = DialogBounds();
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = Vintagestory.API.Client.ElementSizing.FitToChildren;

        SingleComposer = capi.Gui.CreateCompo("scribeLectern", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("scribe:scribe-gui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds);

        foreach (var block in blocks)
        {
            string text = block.IsTask
                ? (block.Done ? "[x] " : "[ ] ") + block.Text
                : block.Text;

            double minHeight = ScribeBlockRowCell.RowHeight(block, clientConfig.TextSizeScale);
            double rowHeight = ScribeBlockRowCell.MeasureWrappedHeight(capi, text, RowFont(), listWidth, minHeight);
            var rowBounds = ElementBounds.Fixed(0, y, listWidth, rowHeight);

            SingleComposer.AddStaticText(text, RowFont(), rowBounds);
            y += rowHeight + rowSpacing;
        }

        if (blocks.Count == 0)
        {
            string hintText = Lang.Get("scribe:scribe-gui-edit-hint");
            double hintHeight = ScribeBlockRowCell.MeasureWrappedHeight(capi, hintText, RowFont(), listWidth, 30);
            SingleComposer.AddStaticText(hintText, RowFont(), ElementBounds.Fixed(0, y, listWidth, hintHeight));
            y += hintHeight + rowSpacing;
        }

        var switchBounds = ElementBounds.Fixed(0, y, listWidth, 30);
        SingleComposer.AddSmallButton(Lang.Get("scribe:scribe-gui-switch-to-editor"), OnClickSwitchToEditor, switchBounds, key: "switchModeButton");

        SingleComposer.EndChildElements().Compose();
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

    private const double EditorListWidth = 540;

    private void ComposeEditorView()
    {
        if (scratchDocument is null) return;

        double rowSpacing = 6;
        double listWidth = EditorListWidth;
        double y = TopContentGap;

        var dialogBounds = DialogBounds();
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = Vintagestory.API.Client.ElementSizing.FitToChildren;

        SingleComposer = capi.Gui.CreateCompo("scribeLectern", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("scribe:scribe-gui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds);

        var blocks = scratchDocument.Blocks;
        composedNoteRowHeights.Clear();
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            double minHeight = ScribeBlockRowCell.RowHeight(block, clientConfig.TextSizeScale);

            // Task rows (GuiElementTextInput) are single-line by design and never wrap, so the
            // fixed height is correct as-is. Text-section rows (GuiElementTextArea) DO wrap and
            // grow past this height the moment ApplyValues seeds their text below -- measure
            // ahead so later rows lay out at the height the text area will actually end up
            // being, not the pre-growth constant (confirmed live: an unmeasured long note
            // overlaps "Text Size"/"Collapse" below it). Recorded per-index so OnRowTextChanged
            // can tell whether a live edit has changed the wrapped height enough to need a
            // recompose (see OnRowTextChanged).
            double rowHeight = minHeight;
            if (!block.IsTask)
            {
                double textWidth = ScribeBlockRowCell.TextWidth(listWidth, isTask: false, showDragHandle: true);
                rowHeight = ScribeBlockRowCell.MeasureWrappedHeight(capi, block.Text, RowFont(), textWidth, minHeight);
                composedNoteRowHeights[i] = rowHeight;
            }

            var rowBounds = ElementBounds.Fixed(0, y, listWidth, rowHeight);

            ScribeBlockRowCell.Compose(
                SingleComposer,
                block,
                i,
                rowBounds,
                RowFont(),
                showDragHandle: true,
                OnRowToggle,
                OnRowTextChanged,
                OnRowDelete,
                OnRowDragMouseDown,
                OnRowDragMouseUp);

            y += rowHeight + rowSpacing;
        }

        y += 6;
        SingleComposer.AddStaticText(Lang.Get("scribe:scribe-gui-textsize"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, 110, 30));
        SingleComposer.AddSlider(OnTextSizeSliderChanged, ElementBounds.Fixed(115, y, listWidth - 115, 30), key: "textSizeSlider");
        SingleComposer.GetSlider("textSizeSlider").SetValues(TextSizePercent, 50, MaxTextSizePercent, 10, "%");
        y += 38;

        var toolPanelToggleBounds = ElementBounds.Fixed(0, y, 140, 30);
        string collapseLangKey = isToolPanelExpanded ? "scribe:scribe-gui-collapse" : "scribe:scribe-gui-expand";
        SingleComposer.AddSmallButton(Lang.Get(collapseLangKey), OnClickToggleToolPanel, toolPanelToggleBounds, key: "toolPanelToggleButton");
        y += 38;

        if (isToolPanelExpanded)
        {
            double optionX = 0;
            foreach (var option in ToolbarOptions())
            {
                var optionBounds = ElementBounds.Fixed(optionX, y, 36, 32);
                SingleComposer.AddIf(option.IsVisible());
                SingleComposer.AddIconButton(option.Icon, _ => option.OnActivate(), optionBounds, key: option.Key);
                SingleComposer.AddHoverText(Lang.Get(option.LangKey), CairoFont.WhiteSmallText(), 150, optionBounds.FlatCopy());
                SingleComposer.EndIf();
                optionX += 42;
            }

            y += 38;
        }

        var switchBounds = ElementBounds.Fixed(0, y, 180, 30);
        SingleComposer.AddSmallButton(Lang.Get("scribe:scribe-gui-switch-to-read"), OnClickSwitchToRead, switchBounds, key: "switchModeButton");

        SingleComposer.EndChildElements().Compose();

        // Seed row values (toggle state, text) only after Compose() has calculated real bounds --
        // see the doc comment on ScribeBlockRowCell.Compose for why doing this earlier corrupts
        // the text elements' auto-height calc and, transitively, the whole dialog's outer size.
        for (int i = 0; i < blocks.Count; i++)
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
        ComposeEditorView();
        return true;
    }

    private void OnRowToggle(int index)
    {
        scratchDocument?.ToggleTask(index);
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

        double minHeight = ScribeBlockRowCell.RowHeight(block, clientConfig.TextSizeScale);
        double textWidth = ScribeBlockRowCell.TextWidth(EditorListWidth, isTask: false, showDragHandle: true);
        double newHeight = ScribeBlockRowCell.MeasureWrappedHeight(capi, text, RowFont(), textWidth, minHeight);

        if (composedNoteRowHeights.TryGetValue(index, out double composedHeight) && newHeight != composedHeight)
        {
            RecomposeEditorViewPreservingFocus();
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
        ComposeEditorView();
    }

    private bool OnClickAddTask()
    {
        scratchDocument?.AddTask(Lang.Get("scribe:scribe-gui-newtask-placeholder"));
        isDirty = true;
        ComposeEditorView();
        return true;
    }

    private bool OnClickSwitchToRead()
    {
        // Flush any pending edit BEFORE releasing the lock: the server processes messages in
        // send order, so releasing first would let the flushed edit arrive lock-less and be
        // silently rejected by ApplyEdit's lock check.
        FlushIfDirty();
        SendReleaseLockPacket();
        EnterMode(false, null);
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
        // assuming either kind (GetTextInput's cast throws on a text-section row).
        for (int i = 0; i < scratchDocument.Blocks.Count; i++)
        {
            var bounds = SingleComposer.GetElement(ScribeBlockRowCell.TextKey(i))?.Bounds;
            if (bounds is null) continue;

            double midY = bounds.absY + bounds.OuterHeight / 2;
            if (mouseY < midY) return i;
        }

        return scratchDocument.Blocks.Count - 1;
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
