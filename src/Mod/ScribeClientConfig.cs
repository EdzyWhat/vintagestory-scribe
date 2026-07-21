namespace Scribe;

/// <summary>
/// Client-only display preferences and GUI layout-tuning knobs for the lectern dialog. Stored
/// per-side via <c>ICoreAPICommon.StoreModConfig</c>/<c>LoadModConfig</c>, which are never
/// synced between client and server -- this must never be written into a
/// <c>Scribe.Core.ScribeDocument</c>.
///
/// Every field below used to be a hardcoded constant in <see cref="GuiDialogScribeLectern"/>/
/// <see cref="ScribeBlockRowCell"/>. Moved here so the GUI's spacing/sizing can be re-tuned by
/// editing this file's on-disk JSON (<c>ScribeModSystem.ClientConfigFileName</c>, under the
/// game's mod-config folder) and relaunching, instead of editing source and rebuilding --
/// useful while visually refining the custom-drawn dialog. See ROADMAP.md's parked ConfigLib
/// idea for eventually exposing these as a live in-game settings panel instead of a relaunch.
/// </summary>
public sealed class ScribeClientConfig
{
    // ---------------- Text size ----------------

    /// <summary>Current font-size multiplier, 1.0 = 100%. Player-adjustable in-GUI via the
    /// text-size slider (see <c>GuiDialogScribeLectern.OnTextSizeSliderChanged</c>) -- unlike
    /// every other field below, this one is meant to change routinely during normal play, not
    /// just while tuning layout.</summary>
    public float TextSizeScale = 1f;

    /// <summary>Lower bound for the text-size slider, in percent. Mirrors
    /// <see cref="MaxTextSizePercent"/> so the low end is tunable rather than hardcoded (the
    /// slider's floor and the constructor's clamp both read this).</summary>
    public int MinTextSizePercent = 20;

    /// <summary>Upper bound for the text-size slider, in percent. A loose sanity bound now that
    /// the row list scrolls to handle overflow, not a tight guard against it.</summary>
    public int MaxTextSizePercent = 120;

    // ---------------- Row-list viewport ----------------

    /// <summary>Fixed viewport height (unscaled) for the scrollable row-list region, shared by
    /// both views.</summary>
    public double VisibleListHeight = 400;

    /// <summary>Vertical gap between rows in the scrollable list, shared by both views. A thin
    /// divider (<see cref="RowDividerThickness"/>) is centered in this gap.</summary>
    public double RowSpacing = 14;

    /// <summary>Vertical gap between the title bar and the first row, shared by both views.</summary>
    public double TopContentGap = 20;

    // ---------------- Row divider ----------------

    /// <summary>Thickness, in pixels, of the embossed-inset divider line drawn below each row.</summary>
    public double RowDividerThickness = 2;

    /// <summary>Brightness (0-1) of the embossed-inset divider line -- see the engine's own
    /// <c>AddInset</c> helper's <c>brightness</c> parameter.</summary>
    public float RowDividerBrightness = 0.85f;

    // ---------------- Row-list width ----------------

    /// <summary>Read-view row-list width.</summary>
    public double ReadListWidth = 300;

    /// <summary>Editor-view row-list width. Wider than the read view to leave room for the drag
    /// handle/checkbox/pin/delete icon gutters that the read view's plain static text doesn't
    /// need.</summary>
    public double EditorListWidth = 340;

    // ---------------- Row cell dimensions (ScribeBlockRowCell) ----------------

    /// <summary>Base (unscaled) height of a task row, before <see cref="TextSizeScale"/>.</summary>
    public double TaskRowHeight = 30;

    /// <summary>Base (unscaled) height of a text-section row, before <see cref="TextSizeScale"/>.</summary>
    public double TextSectionRowHeight = 70;

    /// <summary>Base (unscaled) width of a task row's checkbox column -- scales with
    /// <see cref="TextSizeScale"/> at the call site so the checkbox stays in step with row
    /// text/height rather than staying a fixed pixel size.</summary>
    public double ToggleWidth = 28;

    /// <summary>Width of a row's delete-icon column.</summary>
    public double DeleteWidth = 32;

    /// <summary>Width of a row's drag-handle column (editor view only).</summary>
    public double DragHandleWidth = 24;

    /// <summary>Width of a task row's pin-icon column.</summary>
    public double PinWidth = 32;

    // ---------------- Editor toolbar (controls below the row list) ----------------

    /// <summary>Shared height for the text-size label/slider row, the collapse-toggle button,
    /// and the switch-mode button.</summary>
    public double ControlRowHeight = 30;

    /// <summary>Vertical gap between successive control rows below the row list (text-size row,
    /// collapse-toggle row, icon-toolbar row).</summary>
    public double ControlRowGap = 38;

    /// <summary>Gap between the row list's own bottom edge and the first control row below it
    /// (editor view only -- the read view has no stacked control rows below its list, just its
    /// own single switch-mode button spaced by <see cref="RowSpacing"/>).</summary>
    public double ListToControlsGap = 6;

    /// <summary>Width of the "Text Size" label preceding the text-size slider.</summary>
    public double TextSizeLabelWidth = 110;

    /// <summary>Horizontal gap between the "Text Size" label and the slider that follows it.</summary>
    public double TextSizeLabelToSliderGap = 5;

    /// <summary>Width of the collapse/expand tool-panel toggle button.</summary>
    public double ToolPanelToggleWidth = 140;

    /// <summary>Width of one icon-toolbar button (e.g. Add Task).</summary>
    public double ToolbarIconWidth = 36;

    /// <summary>Height of one icon-toolbar button.</summary>
    public double ToolbarIconHeight = 32;

    /// <summary>Horizontal spacing between successive icon-toolbar buttons.</summary>
    public double ToolbarIconSpacing = 42;

    /// <summary>Width of the editor view's "Switch to Read" mode-switch button (the read view's
    /// own "Switch to Editor" button instead spans the full row-list width, so it has no
    /// separate width knob here).</summary>
    public double SwitchButtonWidth = 180;

    /// <summary>Width of the tooltip box shown on hover over a row's pin/delete icon.</summary>
    public double HoverTextWidth = 150;
}
