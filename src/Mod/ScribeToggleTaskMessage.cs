using ProtoBuf;

namespace Scribe;

/// <summary>
/// Client -&gt; server: toggle a single task's done state on a lectern, from the read view.
///
/// Unlike <see cref="ScribeEditDocumentMessage"/> (the editor-view autosave, which the server
/// applies only for the current editor-lock holder), this action is deliberately <b>lock-free</b>:
/// the read view holds no editor lock, and ticking a task off is treated as an always-allowed
/// action any viewer can perform, even while another player is editing the document. The server
/// applies it via <see cref="BlockEntityScribeLectern.ToggleTaskFromReader"/> and re-syncs to
/// everyone through the usual <c>MarkDirty</c> path. Carries only a block index (not a whole
/// document), so it cannot clobber concurrent text edits beyond the toggled flag itself.
/// </summary>
[ProtoContract]
public sealed class ScribeToggleTaskMessage
{
    [ProtoMember(1)]
    public int PosX { get; set; }

    [ProtoMember(2)]
    public int PosY { get; set; }

    [ProtoMember(3)]
    public int PosZ { get; set; }

    /// <summary>Index of the task block to toggle, in <c>ScribeDocument.Blocks</c> order. A bad
    /// or non-task index is a server-side no-op (see <c>ScribeDocument.ToggleTask</c>).</summary>
    [ProtoMember(4)]
    public int BlockIndex { get; set; }
}
