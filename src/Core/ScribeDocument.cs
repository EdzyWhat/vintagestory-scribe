namespace Scribe.Core;

/// <summary>
/// The game-agnostic model of a Scribe document: an ordered list of tasks plus a single
/// freeform note. All mutation methods return <c>true</c> on success and <c>false</c> for
/// invalid input (blank text, out-of-range index), never throwing to the caller.
/// This type has no dependency on the Vintage Story API.
/// </summary>
public sealed class ScribeDocument
{
    private readonly List<ScribeTask> _tasks = new();

    /// <summary>The tasks, in order. Read-only to callers; mutate via the methods below.</summary>
    public IReadOnlyList<ScribeTask> Tasks => _tasks;

    /// <summary>The freeform note. May be empty, never null.</summary>
    public string Note { get; private set; } = "";

    /// <summary>Adds a task to the end of the list. Text is trimmed; blank text is rejected.</summary>
    public bool AddTask(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        _tasks.Add(new ScribeTask(text.Trim()));
        return true;
    }

    /// <summary>Changes the text of the task at <paramref name="index"/>. Blank text is rejected.</summary>
    public bool RenameTask(int index, string text)
    {
        if (!IsValidIndex(index)) return false;
        if (string.IsNullOrWhiteSpace(text)) return false;
        _tasks[index].Text = text.Trim();
        return true;
    }

    /// <summary>Flips the completed flag of the task at <paramref name="index"/>.</summary>
    public bool ToggleTask(int index)
    {
        if (!IsValidIndex(index)) return false;
        _tasks[index].Done = !_tasks[index].Done;
        return true;
    }

    /// <summary>Removes the task at <paramref name="index"/>, preserving the order of the rest.</summary>
    public bool DeleteTask(int index)
    {
        if (!IsValidIndex(index)) return false;
        _tasks.RemoveAt(index);
        return true;
    }

    /// <summary>Replaces the note. Null is treated as empty.</summary>
    public bool SetNote(string? text)
    {
        Note = text ?? "";
        return true;
    }

    /// <summary>
    /// Replaces all tasks in one shot (used by the codec when rebuilding a document).
    /// </summary>
    internal void SetTasks(IEnumerable<ScribeTask> tasks)
    {
        _tasks.Clear();
        _tasks.AddRange(tasks);
    }

    private bool IsValidIndex(int index) => index >= 0 && index < _tasks.Count;
}
