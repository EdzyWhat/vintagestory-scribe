using ProtoBuf;

namespace Scribe;

/// <summary>
/// Client -&gt; server: request to switch a currently-open lectern dialog between the lock-free
/// read view and the lock-holding editor view, sent by the in-GUI mode-toggle button. The
/// initial open (right-click / shift+right-click) does not use this message — it rides the
/// implicit block-interaction sync instead. Server replies via <see cref="ScribeEditDocumentMessage"/>.
/// </summary>
[ProtoContract]
public sealed class ScribeRequestAccessMessage
{
    [ProtoMember(1)]
    public int PosX { get; set; }

    [ProtoMember(2)]
    public int PosY { get; set; }

    [ProtoMember(3)]
    public int PosZ { get; set; }

    /// <summary>True to request the editor view (takes the lock); false to switch to read view.</summary>
    [ProtoMember(4)]
    public bool WantEditor { get; set; }
}
