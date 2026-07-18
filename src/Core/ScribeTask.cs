namespace Scribe.Core;

/// <summary>
/// A single to-do item: some text plus whether it's been completed.
/// </summary>
public sealed class ScribeTask
{
    public string Text { get; set; }
    public bool Done { get; set; }

    public ScribeTask(string text, bool done = false)
    {
        Text = text;
        Done = done;
    }
}
