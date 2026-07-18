using System.Text;

namespace Scribe.Core;

/// <summary>
/// Serializes a <see cref="ScribeDocument"/> to a byte array and back. The same bytes are
/// used for both world persistence and network sync, so the round-trip is exact and any
/// malformed input fails safely (returns false) rather than throwing.
///
/// Format (little-endian via <see cref="BinaryWriter"/>):
///   [4 bytes magic "SCRB"][1 byte version][int taskCount][per task: bool done, string text][string note]
/// A hand-rolled format keeps Core free of any external dependency.
/// </summary>
public static class ScribeDocumentCodec
{
    private static readonly byte[] Magic = "SCRB"u8.ToArray();
    private const byte Version = 1;

    public static byte[] Serialize(ScribeDocument doc)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Magic);
            w.Write(Version);
            w.Write(doc.Tasks.Count);
            foreach (var task in doc.Tasks)
            {
                w.Write(task.Done);
                w.Write(task.Text);
            }
            w.Write(doc.Note);
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

            int taskCount = r.ReadInt32();
            if (taskCount < 0 || taskCount > bytes.Length) return false; // sanity bound

            var doc = new ScribeDocument();
            var tasks = new List<ScribeTask>(taskCount);
            for (int i = 0; i < taskCount; i++)
            {
                bool done = r.ReadBoolean();
                string text = r.ReadString();
                tasks.Add(new ScribeTask(text, done));
            }
            doc.SetTasks(tasks);
            doc.SetNote(r.ReadString());

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
