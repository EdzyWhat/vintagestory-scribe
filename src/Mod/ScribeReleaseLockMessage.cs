using ProtoBuf;

namespace Scribe;

/// <summary>Client -&gt; server: release the single-editor lock on a lectern (sent on GUI close).</summary>
[ProtoContract]
public sealed class ScribeReleaseLockMessage
{
    [ProtoMember(1)]
    public int PosX { get; set; }

    [ProtoMember(2)]
    public int PosY { get; set; }

    [ProtoMember(3)]
    public int PosZ { get; set; }
}
