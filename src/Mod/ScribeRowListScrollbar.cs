using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace Scribe;

/// <summary>
/// The lectern row list's scrollbar. A thin subclass of the stock
/// <see cref="GuiElementScrollbar"/> that changes only one thing: the mouse-wheel step.
///
/// The engine's default wheel step (<c>scaled(102)</c> content pixels per notch, hardcoded in
/// <c>GuiElementScrollbar.OnMouseWheel</c>) advances this list by roughly two task rows per
/// notch, which playtesting flagged as scrolling too far to land on a specific row. This
/// override scrolls by exactly <see cref="RowStep"/> content pixels per notch instead -- set by
/// the dialog to one task-row height -- so each notch moves the list a single row.
///
/// Everything else (thumb-drag, track-click, keyboard, rendering) is the base element's
/// behavior unchanged. The drag-continues-across-recompose handling lives in
/// <c>GuiDialogScribeLectern</c> (it copies the base's public <c>mouseDownOnScrollbarHandle</c>/
/// <c>mouseDownStartY</c> onto the freshly composed scrollbar), not here, since it's about the
/// dialog's recompose lifecycle rather than this element's own input.
/// </summary>
public sealed class ScribeRowListScrollbar : GuiElementScrollbar
{
    /// <summary>Content-space pixels to scroll per mouse-wheel notch. Same units as the values
    /// passed to <c>SetHeights</c> (unscaled fixed content coordinates, matching how the dialog
    /// measures row positions), so setting it to a task-row height makes one notch equal one
    /// row. Defaults to the stock task-row height; the dialog overwrites it with the current
    /// text-size-scaled row height each compose.</summary>
    public double RowStep = 30;

    public ScribeRowListScrollbar(ICoreClientAPI capi, System.Action<float> onNewScrollbarValue, ElementBounds bounds)
        : base(capi, onNewScrollbarValue, bounds)
    {
    }

    public override void OnMouseWheel(ICoreClientAPI api, MouseWheelEventArgs args)
    {
        // Mirror the base guard: ignore the wheel when the content fits (no scrollable range).
        if (Bounds.InnerHeight <= currentHandleHeight + 0.001) return;

        // Work in content units via CurrentYPosition (handlePosition * ScrollConversionFactor)
        // rather than the base's handle-space math, so RowStep reads as "content pixels per
        // notch" directly. deltaPrecise is +1 per notch up / -1 per notch down; subtracting
        // matches the base's own sign convention (wheel up scrolls toward the top).
        float maxScroll = System.Math.Max(0, totalHeight - visibleHeight);
        CurrentYPosition = GameMath.Clamp(CurrentYPosition - (float)(RowStep * args.deltaPrecise), 0, maxScroll);
        TriggerChanged();
        args.SetHandled(true);
    }
}
