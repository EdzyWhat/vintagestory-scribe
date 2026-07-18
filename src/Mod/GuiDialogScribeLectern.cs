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

    private bool isReorderMode;
    private int? draggedBlockIndex;
    private int? hoverTargetIndex;

    private bool isToolPanelExpanded = true;

    /// <summary>
    /// One entry per tool-panel button. <c>IsVisible</c> is the gating hook for future
    /// context-sensitive tools (e.g. hide "Reorder" below two rows) — v1 wires every option
    /// visible unconditionally, per task 5.3.
    /// </summary>
    private readonly record struct ToolbarOption(string Key, string LangKey, System.Func<bool> IsVisible, ActionConsumable OnActivate);

    public GuiDialogScribeLectern(ICoreClientAPI capi, BlockEntityScribeLectern lectern, bool isEditorMode, byte[]? documentBytes)
        : base(Lang.Get("scribe-gui-title"), lectern.Pos, capi)
    {
        this.lectern = lectern;
        this.clientConfig = capi.LoadModConfig<ScribeClientConfig>(ScribeModSystem.ClientConfigFileName) ?? new ScribeClientConfig();

        if (IsDuplicate) return;

        EnterMode(isEditorMode, documentBytes);
    }

    private CairoFont RowFont() =>
        CairoFont.TextInput().WithFontSize((float)(GuiStyle.NormalFontSize * clientConfig.TextSizeScale));

    private int TextSizePercent => (int)System.Math.Round(clientConfig.TextSizeScale * 100);

    private bool OnTextSizeSliderChanged(int percent)
    {
        clientConfig.TextSizeScale = percent / 100f;
        capi.StoreModConfig(clientConfig, ScribeModSystem.ClientConfigFileName);
        ComposeEditorView();
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
        isReorderMode = false;
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

    // ---------------- Read view ----------------

    private void ComposeReadView()
    {
        var blocks = lectern.Document.Blocks;
        double rowSpacing = 4;
        double listWidth = 400;
        double y = 0;

        var dialogBounds = DialogBounds();
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = Vintagestory.API.Client.ElementSizing.FitToChildren;

        SingleComposer = capi.Gui.CreateCompo("scribeLectern", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("scribe-gui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds);

        foreach (var block in blocks)
        {
            double rowHeight = ScribeBlockRowCell.RowHeight(block);
            var rowBounds = ElementBounds.Fixed(0, y, listWidth, rowHeight);

            string text = block.IsTask
                ? (block.Done ? "[x] " : "[ ] ") + block.Text
                : block.Text;

            SingleComposer.AddStaticText(text, RowFont(), rowBounds);
            y += rowHeight + rowSpacing;
        }

        if (blocks.Count == 0)
        {
            SingleComposer.AddStaticText(Lang.Get("scribe-gui-edit-hint"), RowFont(), ElementBounds.Fixed(0, y, listWidth, 30));
            y += 30 + rowSpacing;
        }

        var switchBounds = ElementBounds.Fixed(0, y, listWidth, 30);
        SingleComposer.AddSmallButton(Lang.Get("scribe-gui-switch-to-editor"), OnClickSwitchToEditor, switchBounds, key: "switchModeButton");

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

    private void ComposeEditorView()
    {
        if (scratchDocument is null) return;

        double rowSpacing = 4;
        double listWidth = 460;
        double y = 0;

        var dialogBounds = DialogBounds();
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = Vintagestory.API.Client.ElementSizing.FitToChildren;

        SingleComposer = capi.Gui.CreateCompo("scribeLectern", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("scribe-gui-title"), OnTitleBarClose)
            .BeginChildElements(bgBounds);

        var blocks = scratchDocument.Blocks;
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            double rowHeight = ScribeBlockRowCell.RowHeight(block);
            var rowBounds = ElementBounds.Fixed(0, y, listWidth, rowHeight);

            ScribeBlockRowCell.Compose(
                SingleComposer,
                block,
                i,
                rowBounds,
                RowFont(),
                isReorderMode,
                OnRowToggle,
                OnRowTextChanged,
                OnRowDelete,
                OnRowDragMouseDown,
                OnRowDragMouseUp);

            y += rowHeight + rowSpacing;
        }

        var textSizeBounds = ElementBounds.Fixed(0, y, listWidth, 30);
        SingleComposer.AddStaticText(Lang.Get("scribe-gui-textsize"), CairoFont.WhiteSmallText(), ElementBounds.Fixed(0, y, 110, 30));
        SingleComposer.AddSlider(OnTextSizeSliderChanged, ElementBounds.Fixed(115, y, listWidth - 115, 30), key: "textSizeSlider");
        SingleComposer.GetSlider("textSizeSlider").SetValues(TextSizePercent, 50, 200, 10, "%");
        y += 34;

        double toolbarY = y;
        string collapseLangKey = isToolPanelExpanded ? "scribe-gui-collapse" : "scribe-gui-expand";
        SingleComposer.AddSmallButton(Lang.Get(collapseLangKey), OnClickToggleToolPanel, ElementBounds.Fixed(0, toolbarY, 110, 30), key: "toolPanelToggleButton");
        y += 34;

        if (isToolPanelExpanded)
        {
            double optionX = 0;
            foreach (var option in ToolbarOptions())
            {
                SingleComposer.AddIf(option.IsVisible());
                SingleComposer.AddSmallButton(Lang.Get(option.LangKey), option.OnActivate, ElementBounds.Fixed(optionX, y, 110, 30), key: option.Key);
                SingleComposer.EndIf();
                optionX += 115;
            }

            y += 34;
        }

        var switchBounds = ElementBounds.Fixed(0, y, listWidth, 30);
        SingleComposer.AddSmallButton(Lang.Get("scribe-gui-switch-to-read"), OnClickSwitchToRead, switchBounds, key: "switchModeButton");

        SingleComposer.EndChildElements().Compose();
    }

    private System.Collections.Generic.IEnumerable<ToolbarOption> ToolbarOptions()
    {
        yield return new ToolbarOption("addTaskButton", "scribe-gui-addtask", () => true, OnClickAddTask);
        yield return new ToolbarOption("addTextButton", "scribe-gui-addtext", () => true, OnClickAddText);
        yield return new ToolbarOption("reorderButton", "scribe-gui-reorder", () => true, OnClickToggleReorder);
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
    }

    private void OnRowDelete(int index)
    {
        scratchDocument?.DeleteBlock(index);
        isDirty = true;
        ComposeEditorView();
    }

    private bool OnClickAddTask()
    {
        scratchDocument?.AddTask(Lang.Get("scribe-gui-newtask-placeholder"));
        isDirty = true;
        ComposeEditorView();
        return true;
    }

    private bool OnClickAddText()
    {
        scratchDocument?.AddTextSection("");
        isDirty = true;
        ComposeEditorView();
        return true;
    }

    private bool OnClickToggleReorder()
    {
        isReorderMode = !isReorderMode;
        draggedBlockIndex = null;
        hoverTargetIndex = null;
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
        // row's live bounds by key rather than recomputing layout math here.
        for (int i = 0; i < scratchDocument.Blocks.Count; i++)
        {
            var textElem = SingleComposer.GetTextInput(ScribeBlockRowCell.TextKey(i));
            var bounds = textElem?.Bounds;
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
