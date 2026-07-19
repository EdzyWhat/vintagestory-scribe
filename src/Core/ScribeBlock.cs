namespace Scribe.Core;

/// <summary>
/// The kind of a <see cref="ScribeBlock"/>. Persisted as a byte, so values are explicit
/// and MUST remain stable across versions (append new kinds; never renumber).
/// </summary>
public enum ScribeBlockKind : byte
{
    /// <summary>A checkbox to-do item (has a Done flag).</summary>
    Task = 0,

    /// <summary>A freeform text section with no checkbox.</summary>
    Text = 1,
}

/// <summary>
/// One element of a <see cref="ScribeDocument"/>. A document is an ordered sequence of
/// these, so tasks and free-text sections can be interspersed and reordered freely.
///
/// A block is either a Task (checkbox + text) or a Text section (text only). <see cref="Done"/>
/// is only meaningful for Task blocks. <see cref="Depth"/> is reserved for a future
/// sub-item hierarchy (0 = top level today); it is carried through persistence now so
/// enabling nesting later needs no format change.
/// </summary>
public sealed class ScribeBlock
{
    public ScribeBlockKind Kind { get; set; }
    public string Text { get; set; }

    /// <summary>Completed flag. Only meaningful when <see cref="Kind"/> is Task.</summary>
    public bool Done { get; set; }

    /// <summary>Indent/nesting level. Reserved for future hierarchy; always 0 for now.</summary>
    public int Depth { get; set; }

    /// <summary>Pinned-to-HUD flag. Only meaningful when <see cref="Kind"/> is Task. v1 only
    /// stores/toggles this; on-screen HUD rendering is a future tier's job.</summary>
    public bool Pinned { get; set; }

    /// <summary>Reserved for a future assignment capability (player/group UID). Unset by
    /// default; no mutation method exists yet and nothing in this codebase reads it.</summary>
    public string? AssignedToUid { get; set; }

    public ScribeBlock(ScribeBlockKind kind, string text, bool done = false, int depth = 0, bool pinned = false, string? assignedToUid = null)
    {
        Kind = kind;
        Text = text;
        Done = done;
        Depth = depth;
        Pinned = pinned;
        AssignedToUid = assignedToUid;
    }

    public bool IsTask => Kind == ScribeBlockKind.Task;
}
