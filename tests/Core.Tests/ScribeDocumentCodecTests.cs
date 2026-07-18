using Scribe.Core;

namespace Scribe.Core.Tests;

// Tests for serializing a document to bytes and back.
// The same codec is used for both world persistence and network sync,
// so the round-trip must be exact and malformed input must fail safely.
public class ScribeDocumentCodecTests
{
    [Fact]
    public void RoundTrip_PreservesTasksOrderDoneFlagsAndNote()
    {
        var original = new ScribeDocument();
        original.AddTask("Find copper");
        original.AddTask("Find tin");
        original.AddTask("Build a forge");
        original.ToggleTask(1); // mark the middle one done
        original.SetNote("Tin is rarer than copper.");

        byte[] bytes = ScribeDocumentCodec.Serialize(original);
        bool ok = ScribeDocumentCodec.TryDeserialize(bytes, out ScribeDocument? restored);

        Assert.True(ok);
        Assert.NotNull(restored);
        Assert.Equal(original.Note, restored!.Note);
        Assert.Equal(
            original.Tasks.Select(t => (t.Text, t.Done)),
            restored.Tasks.Select(t => (t.Text, t.Done)));
    }

    [Fact]
    public void RoundTrip_EmptyDocument()
    {
        var original = new ScribeDocument();

        byte[] bytes = ScribeDocumentCodec.Serialize(original);
        bool ok = ScribeDocumentCodec.TryDeserialize(bytes, out ScribeDocument? restored);

        Assert.True(ok);
        Assert.NotNull(restored);
        Assert.Empty(restored!.Tasks);
        Assert.Equal("", restored.Note);
    }

    [Fact]
    public void TryDeserialize_EmptyBytes_FailsSafely()
    {
        bool ok = ScribeDocumentCodec.TryDeserialize(Array.Empty<byte>(), out ScribeDocument? restored);

        Assert.False(ok);
        Assert.Null(restored);
    }

    [Fact]
    public void TryDeserialize_MalformedBytes_FailsSafely()
    {
        var garbage = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0x7A };

        bool ok = ScribeDocumentCodec.TryDeserialize(garbage, out ScribeDocument? restored);

        Assert.False(ok);
        Assert.Null(restored);
    }
}
