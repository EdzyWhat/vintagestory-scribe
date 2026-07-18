using ProtoBuf;

namespace Scribe;

/// <summary>
/// Sent between client and server over the "scribe" channel. Client -&gt; server: submits an
/// edited document for a lectern to apply (server-authoritative). Server -&gt; client: the
/// authoritative result — either the current document (open granted) or a refusal reason
/// (e.g. the single-editor lock is held by someone else).
/// </summary>
[ProtoContract]
public sealed class ScribeEditDocumentMessage
{
    /// <summary>World position of the lectern's block entity, packed as x/y/z.</summary>
    [ProtoMember(1)]
    public int PosX { get; set; }

    [ProtoMember(2)]
    public int PosY { get; set; }

    [ProtoMember(3)]
    public int PosZ { get; set; }

    /// <summary>Core-serialized <c>ScribeDocument</c> bytes. Null/empty for a pure open request.</summary>
    [ProtoMember(4)]
    public byte[]? DocumentBytes { get; set; }

    /// <summary>Server -&gt; client only: whether the request was granted.</summary>
    [ProtoMember(5)]
    public bool Granted { get; set; } = true;

    /// <summary>Server -&gt; client only: shown to the player when <see cref="Granted"/> is false.</summary>
    [ProtoMember(6)]
    public string? RefusalReason { get; set; }
}
