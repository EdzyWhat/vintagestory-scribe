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

    private const double ToggleWidth = 28;
    private const double DeleteWidth = 32;
    private const double DragHandleWidth = 24;

    /// <summary>Row height scales with <paramref name="textSizeScale"/> so a row can always fit
    /// its text at the current font size -- the text input/text area elements size themselves
    /// to their bounds, not to their font, so without this larger fonts get clipped.</summary>
    public static double RowHeight(ScribeBlock block, double textSizeScale = 1.0) =>
        (block.IsTask ? TaskRowHeight : TextSectionRowHeight) * textSizeScale;

    /// <summary>The text element's own width within a row of <paramref name="rowWidth"/> --
    /// the same math <see cref="Compose"/> uses internally, exposed so callers can measure
    /// wrapped text height against the real width before laying out the row.</summary>
    public static double TextWidth(double rowWidth, bool isTask, bool showDragHandle)
    {
        double dragHandleWidth = showDragHandle ? DragHandleWidth : 0;
        double toggleWidth = isTask ? ToggleWidth : 0;
        return rowWidth - dragHandleWidth - toggleWidth - DeleteWidth;
    }

    /// <summary>Measures how tall <paramref name="text"/> actually renders when wrapped to
    /// <paramref name="textWidth"/>, floored at <paramref name="minHeight"/> so short content
    /// keeps the usual comfortable minimum row height and only grows for content that wraps
    /// past it. Uses the engine's own wrap-aware measurement (<c>TextDrawUtil.GetMultilineTextHeight</c>,
    /// the same mechanism <c>GuiElementTextArea.TextChanged()</c> uses internally) instead of a
    /// fixed constant, so rows never overlap the row below regardless of text length.</summary>
    public static double MeasureWrappedHeight(ICoreClientAPI capi, string text, CairoFont font, double textWidth, double minHeight) =>
        System.Math.Max(minHeight, capi.Gui.Text.GetMultilineTextHeight(font, text, textWidth));

    public static string ToggleKey(int index) => $"scribeRow{index}Toggle";
    public static string TextKey(int index) => $"scribeRow{index}Text";
    public static string DeleteKey(int index) => $"scribeRow{index}Delete";
    public static string DragHandleKey(int index) => $"scribeRow{index}DragHandle";

    /// <summary>
    /// Composes the row at <paramref name="index"/> within <paramref name="rowBounds"/>
    /// (already positioned by the caller). <paramref name="showDragHandle"/> reserves space for
    /// and adds the drag handle used by reorder mode.
    ///
    /// Adds elements only -- does NOT seed their values. A text input/area's <c>Bounds</c> isn't
    /// calculated until the whole composer's <c>Compose()</c> runs (that's what turns a fixed
    /// width into a real <c>Bounds.InnerWidth</c>); calling <c>SetValue</c> before then makes the
    /// text-wrapping math run against a bounds tree that still has <c>InnerWidth == 0</c>,
    /// corrupting the auto-height calc and, transitively, the whole dialog's outer size (this was
    /// the root cause of a zero-size-surface crash on recompose). Call <see cref="ApplyValues"/>
    /// after the composer's own <c>.Compose()</c> instead.
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
            x += ToggleWidth;
        }

        double textWidth = TextWidth(rowBounds.fixedWidth, block.IsTask, showDragHandle);
        var textBounds = ElementBounds.Fixed(x, rowBounds.fixedY, textWidth, rowBounds.fixedHeight);

        if (block.IsTask)
        {
            composer.AddTextInput(textBounds, text => onTextChanged(index, text), font, TextKey(index));
        }
        else
        {
            composer.AddTextArea(textBounds, text => onTextChanged(index, text), font, TextKey(index));
        }

        x += textWidth;
        var deleteBounds = ElementBounds.Fixed(x, rowBounds.fixedY, DeleteWidth, rowBounds.fixedHeight);
        composer.AddIconButton("eraser", _ => onDelete(index), deleteBounds, key: DeleteKey(index));
        composer.AddHoverText(Lang.Get("scribe:scribe-gui-delete"), CairoFont.WhiteSmallText(), 150, deleteBounds.FlatCopy());
    }

    /// <summary>
    /// Seeds the row's live values (toggle state, text content) -- call once per row after the
    /// composer's own <c>.Compose()</c> has run, so the text elements' bounds are real. See the
    /// note on <see cref="Compose"/> for why this must not happen earlier.
    /// </summary>
    public static void ApplyValues(GuiComposer composer, ScribeBlock block, int index)
    {
        if (block.IsTask)
        {
            composer.GetSwitch(ToggleKey(index)).On = block.Done;
            composer.GetTextInput(TextKey(index)).SetValue(block.Text);
        }
        else
        {
            composer.GetTextArea(TextKey(index)).SetValue(block.Text);
        }
    }
}
