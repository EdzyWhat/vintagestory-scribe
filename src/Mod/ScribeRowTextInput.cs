using System;
using Cairo;
using Vintagestory.API.Client;

namespace Scribe;

/// <summary>
/// A multi-line editor row input, subclassing <see cref="GuiElementTextArea"/> so a long row
/// WRAPS (and grows in height) the same way its static <see cref="ScribeRowElement"/> label does,
/// instead of the single-line horizontal scroll a <c>GuiElementTextInput</c> gave (which collapsed
/// a wrapped row to one line on focus). <c>GuiElementTextArea</c> sets <c>multilineMode = true</c>
/// and ships an <c>Autoheight</c> mechanism (<c>TextChanged</c> re-measures <c>Bounds.fixedHeight</c>
/// via <c>GetMultilineTextHeight</c> on every keystroke); the dialog wires that height change back
/// into the row list so the focused row grows and the rows below it shift (see
/// <c>GuiDialogScribeLectern.OnEditInputTextChanged</c>).
///
/// Keyboard model (Enter/Shift+Enter/Tab), all handled BEFORE delegating to the base:
/// <list type="bullet">
///   <item><b>Plain Enter / keypad Enter</b> -> commit + advance to the next row. ALWAYS consumed,
///     never delegated -- in <c>multilineMode</c> the base's <c>OnKeyEnter()</c> would otherwise
///     insert a newline. Enter is a commit gesture here, never a text key.</item>
///   <item><b>Shift+Enter</b> -> deliberately NOT consumed: it falls through to the base so
///     <c>OnKeyEnter()</c> inserts a hard line break, and the auto-height wiring grows the row.</item>
///   <item><b>Shift+Tab</b> -> commit + retreat to the previous row (always consumed).</item>
///   <item><b>Plain Tab</b> -> consumed as a no-op (a tab glyph inside a task line is unwanted, and
///     multiline mode would otherwise insert one).</item>
///   <item><b>Esc</b> -> deliberately NOT intercepted: the base leaves KeyCode 50 unhandled, so it
///     bubbles up and closes the dialog (the wanted panic-close, decided 2026-07-21). Blur on close
///     still commits the pending edit, so nothing typed is lost.</item>
/// </list>
///
/// The spike (VSAPI-NOTES "Text-input caret / selection conventions") found the shared base
/// <c>GuiElementEditableTextBase</c> already implements selection, word-skip, clipboard, and
/// line-jump -- but its caret nav is gated on <c>CtrlPressed</c> only (never <c>CommandPressed</c>)
/// and its <c>OnKeyDownInternal</c> hard-returns on any <c>AltPressed</c>. So on a Mac, Cmd+Arrow
/// (line ends) and Alt/Option+Arrow (word-skip) do nothing. Rather than reimplement caret logic,
/// this override <b>translates the Mac modifier combos into the Windows-keyed combos the base
/// already handles</b> (see <see cref="TranslateMacCaretModifiers"/>), then delegates.
///
/// This subclass also <b>drops the base's embossed border + dark fill box</b> (see
/// <see cref="ComposeTextElements"/>): the row is drawn by <see cref="ScribeRowElement"/>'s
/// lined-paper look, and a boxed input border over one row read as the text "jumping" on focus
/// (playtest 2026-07-21). Only a subtle focused-highlight background remains -- built here as this
/// element's OWN texture because <c>GuiElementTextArea</c>'s highlight members are private to it.
/// </summary>
public sealed class ScribeRowTextInput : GuiElementTextArea
{
    // GlKeys codes (confirmed via decompile of GlKeys): Up=45, Down=46, Left=47, Right=48,
    // Enter=49, Escape=50, Tab=52, Home=58, End=59, KeypadEnter=82.
    private const int KeyLeft = 47;
    private const int KeyRight = 48;
    private const int KeyEnter = 49;
    // Escape (50) is intentionally not referenced: we let it fall through to the base so it
    // bubbles up and closes the dialog (panic-close, decided 2026-07-21).
    private const int KeyTab = 52;
    private const int KeyHome = 58;
    private const int KeyEnd = 59;
    private const int KeyKeypadEnter = 82;

    /// <summary>Plain Enter (or keypad Enter) pressed while this row is focused: commit + advance to
    /// the next row.</summary>
    private readonly Func<bool> onCommitAndAdvance;

    /// <summary>Shift+Tab pressed: commit + retreat to the previous row.</summary>
    private readonly Func<bool> onCommitAndRetreat;

    /// <summary>Focus lost without an Enter/Shift+Tab (e.g. the player clicked away): commit
    /// the row's edit. May be null when the dialog does not need a blur hook.</summary>
    private readonly Action? onBlur;

    /// <summary>This element's own faint focused-highlight texture. The base
    /// <see cref="GuiElementTextArea"/> has an equivalent but it's private, so we build our own
    /// (and skip the base's emboss+fill box) in <see cref="ComposeTextElements"/>.</summary>
    private LoadedTexture focusHighlightTexture;
    private ElementBounds? focusHighlightBounds;

    public ScribeRowTextInput(
        ICoreClientAPI capi,
        ElementBounds bounds,
        Action<string> onTextChanged,
        CairoFont font,
        Func<bool> onCommitAndAdvance,
        Func<bool> onCommitAndRetreat,
        Action? onBlur = null)
        : base(capi, bounds, onTextChanged, font)
    {
        this.onCommitAndAdvance = onCommitAndAdvance;
        this.onCommitAndRetreat = onCommitAndRetreat;
        this.onBlur = onBlur;
        focusHighlightTexture = new LoadedTexture(capi);
    }

    /// <summary>
    /// Builds only the subtle focused-highlight texture (a faint translucent white shown while
    /// <c>HasFocus</c>), skipping <see cref="GuiElementTextArea"/>'s <c>EmbossRoundRectangleElement</c>
    /// + dark <c>0.2</c> fill that together drew the boxed border. We build our own texture because
    /// the base's <c>highlightTexture</c>/<c>GenerateHighlight</c> are private to
    /// <c>GuiElementTextArea</c>. The base ends with the internal <c>RecomposeText()</c> (which builds
    /// the text texture); we can't call it from this assembly, but the dialog always calls
    /// <c>SetValue</c> on this input right after Compose (see the compose tail in
    /// <c>GuiDialogScribeLectern.ComposeEditorView</c>), and <c>SetValue</c> -> <c>LoadValue</c> ->
    /// <c>TextChanged</c> -> <c>RecomposeText</c> rebuilds the text texture -- so the glyphs render.
    /// </summary>
    public override void ComposeTextElements(Context ctx, ImageSurface surface)
    {
        var hlSurface = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        Context hlCtx = genContext(hlSurface);
        hlCtx.SetSourceRGBA(1.0, 1.0, 1.0, 0.2);
        hlCtx.Paint();
        generateTexture(hlSurface, ref focusHighlightTexture);
        hlCtx.Dispose();
        hlSurface.Dispose();

        focusHighlightBounds = Bounds.FlatCopy();
        focusHighlightBounds.CalcWorldBounds();
        // NOTE: base ends with RecomposeText() here; we rely on the dialog's post-Compose SetValue
        // to trigger it (see summary). If that call is ever removed, the input would render blank.
    }

    /// <summary>
    /// Renders the focused-highlight (when focused) then the base text/selection/caret.
    ///
    /// <para><b>Off-screen skip.</b> When this row is scrolled fully outside the enclosing
    /// <c>BeginClip</c> window, draw nothing. Unlike <c>GuiElementTextInput</c> (whose
    /// <c>GlScissorFlag(false)</c> globally disabled the scissor and caused a "new task drawn below
    /// the box" bleed, playtest 2026-07-21T20-58-36), <c>GuiElementTextArea</c> does NOT touch the
    /// scissor -- so the ambient dialog clip already clips this input's text. The skip is therefore
    /// defensive rather than load-bearing here, but it's cheap and focus-safe (the input stays
    /// focusable/typable and reappears when its row scrolls back in). Uses <c>renderY</c>, which
    /// tracks the parent's scroll <c>fixedY</c> shift; vertical overlap only (the list scrolls only
    /// vertically). (The old scissor re-assert correction that <c>GuiElementTextInput</c> needed is
    /// dropped: the base no longer disables the scissor, so there's nothing to restore.)</para>
    /// </summary>
    public override void RenderInteractiveElements(float deltaTime)
    {
        if (InsideClipBounds != null)
        {
            double top = Bounds.renderY;
            double bottom = top + Bounds.InnerHeight;
            double clipTop = InsideClipBounds.renderY;
            double clipBottom = clipTop + InsideClipBounds.InnerHeight;
            // Fully above or fully below the visible window -> don't draw.
            if (bottom <= clipTop || top >= clipBottom) return;
        }

        if (HasFocus && focusHighlightTexture.TextureId != 0 && focusHighlightBounds != null)
        {
            api.Render.GlToggleBlend(true);
            api.Render.Render2DTexture(focusHighlightTexture.TextureId, focusHighlightBounds);
        }

        base.RenderInteractiveElements(deltaTime);
    }

    public override void OnFocusLost()
    {
        base.OnFocusLost();
        // Blur commits the pending edit (task 4.3). The dialog guards against a recompose-driven
        // blur double-committing; a real click-away commit is idempotent anyway (FlushIfDirty is a
        // no-op when nothing changed).
        onBlur?.Invoke();
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (!HasFocus) return;

        // ---- Cross-row navigation / newline / Tab: handle before the base sees the key ----

        // Shift+Tab retreats. Always consume it: in multiline mode the base treats Tab (52) as an
        // insertable character, so falling through would type a tab glyph.
        if (args.KeyCode == KeyTab && args.ShiftPressed)
        {
            onCommitAndRetreat();
            args.Handled = true;
            return;
        }

        // Plain Tab: consume as a no-op (don't insert a tab glyph; we don't use Tab for traversal).
        if (args.KeyCode == KeyTab)
        {
            args.Handled = true;
            return;
        }

        // Plain Enter (main or keypad) commits and advances. ALWAYS consume -- if we let it fall
        // through, the base's multiline OnKeyEnter() would insert a newline. Enter is never a text
        // key here.
        if ((args.KeyCode == KeyEnter || args.KeyCode == KeyKeypadEnter) && !args.ShiftPressed)
        {
            onCommitAndAdvance();
            args.Handled = true;
            return;
        }

        // Shift+Enter: deliberately NOT intercepted -- it falls through to the base so OnKeyEnter()
        // inserts a hard line break. The dialog's auto-height wiring then grows the row.

        // Escape is deliberately NOT handled here either: the base leaves KeyCode 50 unhandled, so
        // it bubbles up and closes the dialog (panic-close). Blur on close still commits the edit.

        // ---- Mac caret-convention translation, then delegate to the base ----
        base.OnKeyDown(api, TranslateMacCaretModifiers(args));
    }

    public override void Dispose()
    {
        base.Dispose();
        focusHighlightTexture?.Dispose();
    }

    /// <summary>
    /// Rewrites a key event so the base's Windows-keyed caret navigation responds to the Mac
    /// modifier idioms. Only touches Left/Right arrow events carrying Cmd or Alt; every other event
    /// (including a plain Shift+Enter) is returned unchanged. Shift is always preserved so
    /// selection-extend still works.
    /// </summary>
    private static KeyEvent TranslateMacCaretModifiers(KeyEvent args)
    {
        bool isArrow = args.KeyCode == KeyLeft || args.KeyCode == KeyRight;
        if (!isArrow) return args;

        // Cmd+Arrow -> jump to line start/end. The base moves to line ends on Home/End, so rewrite
        // the key code and drop the Cmd flag (leave Ctrl off so it's the current-line Home/End, not
        // the Ctrl+Home/End whole-document jump).
        if (args.CommandPressed)
        {
            return new KeyEvent
            {
                KeyCode = args.KeyCode == KeyLeft ? KeyHome : KeyEnd,
                KeyChar = args.KeyChar,
                ShiftPressed = args.ShiftPressed,
                CtrlPressed = false,
                CommandPressed = false,
                AltPressed = false,
            };
        }

        // Alt/Option+Arrow -> word-skip. The base runs MoveCursor(dir, wholeWord: args.CtrlPressed)
        // for a plain arrow, but ONLY after an `if (args.AltPressed) return;` early-out. So clear
        // Alt and set Ctrl: the early-out is skipped and the arrow does a whole-word move.
        if (args.AltPressed)
        {
            return new KeyEvent
            {
                KeyCode = args.KeyCode,
                KeyChar = args.KeyChar,
                ShiftPressed = args.ShiftPressed,
                CtrlPressed = true,
                CommandPressed = false,
                AltPressed = false,
            };
        }

        return args;
    }
}
