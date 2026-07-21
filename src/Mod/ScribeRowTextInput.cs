using System;
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
/// keeps working for every one of these. Enter / Shift+Tab / Esc are intercepted <i>before</i>
/// delegating and surfaced to the dialog via callbacks (row navigation and revert are inherently
/// cross-element, so they belong to the dialog, not this element).
/// </summary>
public sealed class ScribeRowTextInput : GuiElementTextInput
{
    // GlKeys codes (confirmed via decompile of GlKeys): Up=45, Down=46, Left=47, Right=48,
    // Enter=49, Escape=50, Tab=52, Home=58, End=59, KeypadEnter=82.
    private const int KeyLeft = 47;
    private const int KeyRight = 48;
    private const int KeyEnter = 49;
    private const int KeyEscape = 50;
    private const int KeyTab = 52;
    private const int KeyHome = 58;
    private const int KeyEnd = 59;
    private const int KeyKeypadEnter = 82;

    /// <summary>Enter (or keypad Enter) pressed while this row is focused: commit + advance to the
    /// next row. Return true if the dialog handled it.</summary>
    private readonly Func<bool> onCommitAndAdvance;

    /// <summary>Shift+Tab pressed: commit + retreat to the previous row.</summary>
    private readonly Func<bool> onCommitAndRetreat;

    /// <summary>Escape pressed: revert this row to its last stored text (no commit).</summary>
    private readonly Func<bool> onRevert;

    /// <summary>Focus lost without an Enter/Shift+Tab/Esc (e.g. the player clicked away): commit
    /// the row's edit. May be null when the dialog does not need a blur hook.</summary>
    private readonly Action? onBlur;

    public ScribeRowTextInput(
        ICoreClientAPI capi,
        ElementBounds bounds,
        Action<string> onTextChanged,
        CairoFont font,
        Func<bool> onCommitAndAdvance,
        Func<bool> onCommitAndRetreat,
        Func<bool> onRevert,
        Action? onBlur = null)
        : base(capi, bounds, onTextChanged, font)
    {
        this.onCommitAndAdvance = onCommitAndAdvance;
        this.onCommitAndRetreat = onCommitAndRetreat;
        this.onRevert = onRevert;
        this.onBlur = onBlur;
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

        // Escape reverts. (Left unhandled by default, which would bubble up and close the dialog.)
        if (args.KeyCode == KeyEscape)
        {
            if (onRevert())
            {
                args.Handled = true;
                return;
            }
        }

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
