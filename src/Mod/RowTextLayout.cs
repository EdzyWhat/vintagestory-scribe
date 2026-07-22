using Vintagestory.API.Client;

namespace Scribe;

/// <summary>
/// The single source of truth for where a row's pieces sit horizontally and which font its text
/// uses -- computed once from the row's width + the current text-size scale, and read by BOTH the
/// row's own drawing (<see cref="ScribeRowElement"/>) and, in a later stage (S2), the placement of
/// the one live edit field that floats onto the focused row. Deriving the label draw and the edit
/// input from the same metric (rather than measuring one and matching the other) is what keeps a
/// row's text from visibly jumping the moment it gains an edit field (row-list-rework design.md
/// Decision 5).
///
/// All offsets are in UNSCALED fixed units -- the same coordinate space as <c>ElementBounds.Fixed</c>
/// and every <see cref="ScribeClientConfig"/> layout knob. Callers drawing onto a scaled Cairo
/// surface (or hit-testing against a scaled <c>Bounds.absX/absY</c>) apply <c>GuiElement.scaled(...)</c>
/// to these values at the point of use, exactly as the rest of the dialog does.
///
/// Layout, left to right: <c>[ checkbox column (tasks only) ][ text ]</c>. This mirrors the editor
/// row's checkbox+text ordering (minus the editor-only drag handle / pin / delete gutters) so the
/// read and editor views read as the same list (a stated goal of the rework).
/// </summary>
public readonly struct RowTextLayout
{
    /// <summary>Left edge (unscaled) of the checkbox column. Tasks only; 0 for a note.</summary>
    public double CheckboxX { get; }

    /// <summary>Width/height (unscaled, square) of the checkbox glyph column. 0 for a note. Scales
    /// with the text-size preference so the checkbox stays in step with row text -- mirrors
    /// <see cref="ScribeBlockRowCell"/>'s <c>ToggleWidth * TextSizeScale</c> so the read-view and
    /// editor-view checkbox columns line up.</summary>
    public double CheckboxSize { get; }

    /// <summary>Left edge (unscaled) where the row's text begins -- after the checkbox column plus
    /// a small <see cref="ScribeClientConfig.CheckboxTextGap"/> for a task, or the row's left edge
    /// for a note.</summary>
    public double TextX { get; }

    /// <summary>Available text width (unscaled) from <see cref="TextX"/> to the row's right edge.</summary>
    public double TextWidth { get; }

    /// <summary>The font the row's text is drawn in (already sized for the current text-size
    /// preference by the caller). The same instance S2 will hand to the floating edit field.</summary>
    public CairoFont Font { get; }

    private RowTextLayout(double checkboxX, double checkboxSize, double textX, double textWidth, CairoFont font)
    {
        CheckboxX = checkboxX;
        CheckboxSize = checkboxSize;
        TextX = textX;
        TextWidth = textWidth;
        Font = font;
    }

    /// <summary>
    /// Computes the layout for one row of unscaled width <paramref name="rowWidth"/>. <paramref
    /// name="font"/> must be the same text-size-scaled font the row is measured and drawn with, and
    /// <paramref name="config"/> the same instance used elsewhere for the row, so the reserved
    /// checkbox column matches the measured text width (a mismatch would clip the glyph or overlap
    /// the text -- the same coupling <see cref="ScribeBlockRowCell.TextWidth"/> documents).
    /// </summary>
    public static RowTextLayout For(double rowWidth, bool isTask, CairoFont font, ScribeClientConfig config)
    {
        double checkboxSize = isTask ? config.ToggleWidth * config.TextSizeScale : 0;
        // Gap between the checkbox and the text so the label/input isn't flush against the box
        // (tasks only -- a note has no checkbox, so its text starts at the row's left edge).
        double checkboxTextGap = isTask ? config.CheckboxTextGap * config.TextSizeScale : 0;
        double textX = checkboxSize + checkboxTextGap;
        double textWidth = rowWidth - textX;
        return new RowTextLayout(0, checkboxSize, textX, textWidth, font);
    }
}
