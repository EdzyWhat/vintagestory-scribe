using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Scribe.Core;

namespace Scribe;

/// <summary>
/// A small draggable glyph for a row's reorder handle. Plain static text
/// (<c>AddStaticText</c>) is rendered but never dispatched mouse events by the composer, so
/// reorder mode needs a minimal interactive element instead.
/// </summary>
public sealed class ScribeDragHandleElement : GuiElementStaticText
{
    public System.Action<MouseEvent>? OnDragMouseDown;
    public System.Action<MouseEvent>? OnDragMouseUp;

    public ScribeDragHandleElement(ICoreClientAPI capi, string text, CairoFont font, ElementBounds bounds)
        : base(capi, text, EnumTextOrientation.Center, bounds, font)
    {
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseDownOnElement(api, args);
        OnDragMouseDown?.Invoke(args);
    }

    public override void OnMouseUpOnElement(ICoreClientAPI api, MouseEvent args)
    {
        base.OnMouseUpOnElement(api, args);
        OnDragMouseUp?.Invoke(args);
    }
}

/// <summary>
/// Composes one editable row (a task or a text section) directly onto a <see cref="GuiComposer"/>.
///
/// This is NOT an <see cref="IGuiElementCell"/>/<c>AddCellList</c> row: that list widget only
/// forwards mouse events to its cells and never registers them with the composer's keyboard/
/// focus system, so a live, typable text field cannot live inside one (confirmed against the
/// game's own <c>GuiElementCellList</c>/<c>GuiElementTextInput</c> source — every shipped
/// cell-list row is a static, click-only Cairo-rendered texture). Rows are instead composed as
/// ordinary top-level interactive elements with per-row keys, stacked by hand inside a clipped,
/// scrollable region — the same approach <c>GuiDialogTrader</c> uses for its scrollbar, minus
/// the cell list.
/// </summary>
public static class ScribeBlockRowCell
{
    public const double TaskRowHeight = 30;
    public const double TextSectionRowHeight = 70;

    private const double ToggleWidth = 24;
    private const double DeleteWidth = 24;
    private const double DragHandleWidth = 20;

    public static double RowHeight(ScribeBlock block) =>
        block.IsTask ? TaskRowHeight : TextSectionRowHeight;

    public static string ToggleKey(int index) => $"scribeRow{index}Toggle";
    public static string TextKey(int index) => $"scribeRow{index}Text";
    public static string DeleteKey(int index) => $"scribeRow{index}Delete";
    public static string DragHandleKey(int index) => $"scribeRow{index}DragHandle";

    /// <summary>
    /// Composes the row at <paramref name="index"/> within <paramref name="rowBounds"/>
    /// (already positioned by the caller). <paramref name="showDragHandle"/> reserves space for
    /// and adds the drag handle used by reorder mode.
    /// </summary>
    public static void Compose(
        GuiComposer composer,
        ScribeBlock block,
        int index,
        ElementBounds rowBounds,
        CairoFont font,
        bool showDragHandle,
        System.Action<int> onToggle,
        System.Action<int, string> onTextChanged,
        System.Action<int> onDelete,
        System.Action<int, MouseEvent>? onDragMouseDown = null,
        System.Action<int, MouseEvent>? onDragMouseUp = null)
    {
        double x = rowBounds.fixedX;
        double dragHandleWidth = showDragHandle ? DragHandleWidth : 0;

        if (showDragHandle)
        {
            var dragBounds = ElementBounds.Fixed(x, rowBounds.fixedY, DragHandleWidth, rowBounds.fixedHeight);
            var dragHandle = new ScribeDragHandleElement(composer.Api, "::", font, dragBounds)
            {
                OnDragMouseDown = args => onDragMouseDown?.Invoke(index, args),
                OnDragMouseUp = args => onDragMouseUp?.Invoke(index, args),
            };
            composer.AddInteractiveElement(dragHandle, DragHandleKey(index));
            x += DragHandleWidth;
        }

        double toggleWidth = block.IsTask ? ToggleWidth : 0;
        if (block.IsTask)
        {
            var toggleBounds = ElementBounds.Fixed(x, rowBounds.fixedY, ToggleWidth, rowBounds.fixedHeight);
            composer.AddSwitch(on => onToggle(index), toggleBounds, ToggleKey(index));
            composer.GetSwitch(ToggleKey(index)).On = block.Done;
            x += ToggleWidth;
        }

        double textWidth = rowBounds.fixedWidth - dragHandleWidth - toggleWidth - DeleteWidth;
        var textBounds = ElementBounds.Fixed(x, rowBounds.fixedY, textWidth, rowBounds.fixedHeight);

        if (block.IsTask)
        {
            composer.AddTextInput(textBounds, text => onTextChanged(index, text), font, TextKey(index));
            composer.GetTextInput(TextKey(index)).SetValue(block.Text);
        }
        else
        {
            composer.AddTextArea(textBounds, text => onTextChanged(index, text), font, TextKey(index));
            composer.GetTextArea(TextKey(index)).SetValue(block.Text);
        }

        x += textWidth;
        var deleteBounds = ElementBounds.Fixed(x, rowBounds.fixedY, DeleteWidth, rowBounds.fixedHeight);
        composer.AddSmallButton(Lang.Get("scribe-gui-delete"), () => { onDelete(index); return true; }, deleteBounds);
    }
}
