using System;
using Cairo;
using Vintagestory.API.Client;

namespace Scribe;

/// <summary>
/// A single-line <see cref="GuiElementTextInput"/> for the editor's edit-in-place row, subclassed
/// to add the desktop caret conventions the base element is missing on macOS, plus cross-row
/// keyboard navigation.
///
/// The spike (recorded in VSAPI-NOTES.md "Text-input caret / selection conventions") found that
/// <c>GuiElementEditableTextBase</c> already implements selection, word-skip, clipboard, and
/// line-jump -- but its caret navigation is gated on <c>CtrlPressed</c> only (never
/// <c>CommandPressed</c>), and its <c>OnKeyDownInternal</c> hard-returns on any <c>AltPressed</c>.
/// So on a Mac, Cmd+Arrow (line ends) and Alt/Option+Arrow (word-skip) do nothing. Rather than
/// reimplement caret/selection logic, this override <b>translates the Mac modifier combos into the
/// Windows-keyed combos the base already handles</b>, then delegates to <c>base.OnKeyDown</c>:
/// <list type="bullet">
///   <item>Cmd+Left / Cmd+Right -> rewritten to Home / End (base moves to line start/end).</item>
///   <item>Alt/Option+Left / Right -> Alt cleared and Ctrl set, so the base's word-skip
///     (<c>MoveCursor(dir, wholeWord: true)</c>) runs instead of the Alt early-return.</item>
/// </list>
/// <c>ShiftPressed</c> is left untouched on the rewritten event, so the base's shift-extend-select
/// keeps working for every one of these. Enter / Shift+Tab are intercepted <i>before</i> delegating
/// and surfaced to the dialog via callbacks (row navigation is inherently cross-element, so it
/// belongs to the dialog, not this element). <b>Esc is deliberately NOT intercepted</b> -- it falls
/// through to the base, whose <c>OnKeyDownInternal</c> leaves KeyCode 50 unhandled, so it bubbles up
/// and closes the whole dialog. That is the wanted "panic-close" behavior (decided 2026-07-21 after
/// playtest: a fast exit matters more than an in-place revert). Blur-commit fires on close, so the
/// focused row's pending edit is saved on the way out -- Esc is a commit-and-close, not a discard.
///
/// This subclass also <b>drops the base input's embossed border</b> (<see cref="ComposeTextElements"/>):
/// the row is drawn by <see cref="ScribeRowElement"/>'s lined-paper look, and a boxed input border
/// over one row read as the text "jumping" on focus (playtest 2026-07-21). Only the subtle focused
/// highlight background remains.
/// </summary>
public sealed class ScribeRowTextInput : GuiElementTextInput
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

    /// <summary>Enter (or keypad Enter) pressed while this row is focused: commit + advance to the
    /// next row. Return true if the dialog handled it.</summary>
    private readonly Func<bool> onCommitAndAdvance;

    /// <summary>Shift+Tab pressed: commit + retreat to the previous row.</summary>
    private readonly Func<bool> onCommitAndRetreat;

    /// <summary>Focus lost without an Enter/Shift+Tab (e.g. the player clicked away): commit
    /// the row's edit. May be null when the dialog does not need a blur hook.</summary>
    private readonly Action? onBlur;

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
    }

    /// <summary>
    /// Skips the base <see cref="GuiElementTextInput.ComposeTextElements"/>' embossed border + dark
    /// fill so the floating input has no visible box -- only the focused-highlight background (drawn
    /// separately in <c>RenderInteractiveElements</c> from <c>highlightTexture</c>) remains. The base
    /// builds the highlight texture here and ends by calling the internal <c>RecomposeText</c>; we
    /// can't call that internal from this assembly, but the dialog always calls <c>SetValue</c> on
    /// this input right after Compose (GuiDialogScribeLectern compose tail), and <c>SetValue</c> ->
    /// <c>LoadValue</c> -> <c>TextChanged</c> -> <c>RecomposeText</c> rebuilds the text texture -- so
    /// the glyphs still render. We reproduce only the highlight-texture build from the base.
    /// </summary>
    public override void ComposeTextElements(Context ctx, ImageSurface surface)
    {
        // Build the focused-highlight texture exactly as the base does (a faint translucent white
        // fill shown only while HasFocus), but omit the base's EmbossRoundRectangleElement + the
        // dark ElementRoundRectangle fill that together drew the boxed border.
        var highlightSurface = new ImageSurface(Format.Argb32, (int)Bounds.OuterWidth, (int)Bounds.OuterHeight);
        Context highlightCtx = genContext(highlightSurface);
        highlightCtx.SetSourceRGBA(1.0, 1.0, 1.0, 0.2);
        highlightCtx.Paint();
        generateTexture(highlightSurface, ref highlightTexture);
        highlightCtx.Dispose();
        highlightSurface.Dispose();

        highlightBounds = Bounds.CopyOffsetedSibling().WithFixedPadding(0.0, 0.0)
            .FixedGrow(2.0 * Bounds.absPaddingX, 2.0 * Bounds.absPaddingY);
        highlightBounds.CalcWorldBounds();
        // NOTE: base ends with RecomposeText() here; we rely on the dialog's post-Compose SetValue
        // to trigger it (see summary). If that call is ever removed, the input would render blank.
    }

    /// <summary>
    /// Re-asserts the dialog's clip after the base input renders. The base
    /// <see cref="GuiElementTextInput.RenderInteractiveElements"/> ends with
    /// <c>GlScissorFlag(false)</c>, which is a GLOBAL <c>GL.Disable(GL_SCISSOR_TEST)</c> in
    /// <c>ClientPlatformWindows</c> -- it does NOT restore the enclosing <c>BeginClip</c> scissor
    /// on the render API's <c>ScissorStack</c>. So without this, everything drawn after this input
    /// in the frame (sibling rows, rulings, controls below) renders UNCLIPPED and bleeds past the
    /// dialog frame (VSAPI-NOTES.md "text input ... bleeds out unclipped"). Push-then-pop the clip
    /// bounds still on the stack top: <c>PopScissor</c> re-issues that scissor + re-enables the
    /// flag, restoring the dialog's clip for later elements. No-op when this input isn't inside a
    /// clip region (<c>InsideClipBounds</c> null).
    /// </summary>
    public override void RenderInteractiveElements(float deltaTime)
    {
        base.RenderInteractiveElements(deltaTime);

        if (InsideClipBounds != null)
        {
            api.Render.PushScissor(InsideClipBounds);
            api.Render.PopScissor();
        }
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

        // ---- Cross-row navigation / revert: handle before the base sees the key ----

        // Shift+Tab retreats. (Plain Tab is intentionally left to the base -- which leaves it
        // unhandled for a single-line input -- so it doesn't fight normal focus traversal.)
        if (args.KeyCode == KeyTab && args.ShiftPressed)
        {
            if (onCommitAndRetreat())
            {
                args.Handled = true;
                return;
            }
        }

        // Enter (main or keypad) commits and advances. The base would otherwise defer Enter to the
        // caller for a single-line input (handled = false), so intercepting it here is safe.
        if (args.KeyCode == KeyEnter || args.KeyCode == KeyKeypadEnter)
        {
            if (onCommitAndAdvance())
            {
                args.Handled = true;
                return;
            }
        }

        // Escape is deliberately NOT handled here: the base leaves KeyCode 50 unhandled, so it
        // bubbles up and closes the dialog (the wanted panic-close -- decided 2026-07-21). Blur on
        // close still commits the pending edit, so nothing typed is lost.

        // ---- Mac caret-convention translation, then delegate to the base ----
        base.OnKeyDown(api, TranslateMacCaretModifiers(args));
    }

    /// <summary>
    /// Rewrites a key event so the base's Windows-keyed caret navigation responds to the Mac
    /// modifier idioms. Only touches Left/Right arrow events carrying Cmd or Alt; every other event
    /// is returned unchanged. Shift is always preserved so selection-extend still works.
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
