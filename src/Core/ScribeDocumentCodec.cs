using System.Text;

namespace Scribe.Core;

/// <summary>
/// Serializes a <see cref="ScribeDocument"/> to a byte array and back. The same bytes are
/// used for both world persistence and network sync, so the round-trip is exact and any
/// malformed input fails safely (returns false) rather than throwing.
///
/// Format (little-endian via <see cref="BinaryWriter"/>):
///   [4 bytes magic "SCRB"][1 byte version][int blockCount]
///   [per block: byte kind, bool done, int depth, string text]
/// A hand-rolled format keeps Core free of any external dependency. The version byte lets
/// us evolve the format later while still reading older saves.
/// </summary>
public static class ScribeDocumentCodec
{
    private static readonly byte[] Magic = "SCRB"u8.ToArray();
    private const byte Version = 2; // v1 was flat tasks + a single note; v2 is ordered blocks.

    public static byte[] Serialize(ScribeDocument doc)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Magic);
            w.Write(Version);
            w.Write(doc.Blocks.Count);
            foreach (var block in doc.Blocks)
            {
                w.Write((byte)block.Kind);
                w.Write(block.Done);
                w.Write(block.Depth);
                w.Write(block.Text);
            }
        }
        return ms.ToArray();
    }

    public static bool TryDeserialize(byte[]? bytes, out ScribeDocument? document)
    {
        document = null;
        if (bytes is null || bytes.Length < Magic.Length + 1) return false;

        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            using var r = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

            var magic = r.ReadBytes(Magic.Length);
            if (!magic.AsSpan().SequenceEqual(Magic)) return false;

            byte version = r.ReadByte();
            if (version != Version) return false;

            int blockCount = r.ReadInt32();
            if (blockCount < 0 || blockCount > bytes.Length) return false; // sanity bound

            var blocks = new List<ScribeBlock>(blockCount);
            for (int i = 0; i < blockCount; i++)
            {
                var kind = (ScribeBlockKind)r.ReadByte();
                bool done = r.ReadBoolean();
                int depth = r.ReadInt32();
                string text = r.ReadString();
                blocks.Add(new ScribeBlock(kind, text, done, depth));
            }

            var doc = new ScribeDocument();
            doc.SetBlocks(blocks);
            document = doc;
            return true;
        }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or FormatException)
        {
            // Truncated or malformed input — fail safely.
            document = null;
            return false;
        }
    }
}
