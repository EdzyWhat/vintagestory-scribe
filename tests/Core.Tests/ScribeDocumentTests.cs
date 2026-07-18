using Scribe.Core;

namespace Scribe.Core.Tests;

// Tests for the document structure and task mutations.
// Each test maps to a WHEN/THEN scenario in the task-note-document spec.
public class ScribeDocumentTests
{
    // --- Document structure ---

    [Fact]
    public void NewDocument_IsEmpty()
    {
        var doc = new ScribeDocument();

        Assert.Empty(doc.Tasks);
        Assert.Equal("", doc.Note);
    }

    [Fact]
    public void Tasks_PreserveInsertionOrder()
    {
        var doc = new ScribeDocument();

        doc.AddTask("First");
        doc.AddTask("Second");
        doc.AddTask("Third");

        Assert.Equal(new[] { "First", "Second", "Third" }, doc.Tasks.Select(t => t.Text));
    }

    // --- Add a task ---

    [Fact]
    public void AddTask_AddsAnIncompleteTask()
    {
        var doc = new ScribeDocument();

        bool ok = doc.AddTask("Find copper");

        Assert.True(ok);
        var task = Assert.Single(doc.Tasks);
        Assert.Equal("Find copper", task.Text);
        Assert.False(task.Done);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void AddTask_RejectsBlankText(string blank)
    {
        var doc = new ScribeDocument();

        bool ok = doc.AddTask(blank);

        Assert.False(ok);
        Assert.Empty(doc.Tasks);
    }

    [Fact]
    public void AddTask_TrimsSurroundingWhitespace()
    {
        var doc = new ScribeDocument();

        doc.AddTask("  Find copper  ");

        Assert.Equal("Find copper", doc.Tasks[0].Text);
    }

    // --- Rename a task ---

    [Fact]
    public void RenameTask_ChangesTextAndKeepsDoneFlag()
    {
        var doc = new ScribeDocument();
        doc.AddTask("Find copper");
        doc.ToggleTask(0); // now done

        bool ok = doc.RenameTask(0, "Find tin");

        Assert.True(ok);
        Assert.Equal("Find tin", doc.Tasks[0].Text);
        Assert.True(doc.Tasks[0].Done);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RenameTask_RejectsBlankText(string blank)
    {
        var doc = new ScribeDocument();
        doc.AddTask("Find copper");

        bool ok = doc.RenameTask(0, blank);

        Assert.False(ok);
        Assert.Equal("Find copper", doc.Tasks[0].Text);
    }

    // --- Toggle completion ---

    [Fact]
    public void ToggleTask_IncompleteBecomesComplete()
    {
        var doc = new ScribeDocument();
        doc.AddTask("Build a forge");

        bool ok = doc.ToggleTask(0);

        Assert.True(ok);
        Assert.True(doc.Tasks[0].Done);
    }

    [Fact]
    public void ToggleTask_CompleteBecomesIncomplete()
    {
        var doc = new ScribeDocument();
        doc.AddTask("Build a forge");
        doc.ToggleTask(0);

        doc.ToggleTask(0);

        Assert.False(doc.Tasks[0].Done);
    }

    // --- Delete a task ---

    [Fact]
    public void DeleteTask_RemovesByIndexAndPreservesOrder()
    {
        var doc = new ScribeDocument();
        doc.AddTask("A");
        doc.AddTask("B");
        doc.AddTask("C");

        bool ok = doc.DeleteTask(1);

        Assert.True(ok);
        Assert.Equal(new[] { "A", "C" }, doc.Tasks.Select(t => t.Text));
    }

    // --- Out-of-range safety ---

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]  // empty document: index 0 is already out of range
    [InlineData(5)]
    public void Operations_OnInvalidIndex_FailSafely(int badIndex)
    {
        var doc = new ScribeDocument();
        // document is empty, so any index is invalid

        Assert.False(doc.RenameTask(badIndex, "x"));
        Assert.False(doc.ToggleTask(badIndex));
        Assert.False(doc.DeleteTask(badIndex));
        Assert.Empty(doc.Tasks);
    }

    // --- Edit the note ---

    [Fact]
    public void SetNote_StoresText()
    {
        var doc = new ScribeDocument();

        doc.SetNote("Copper is south of the ridge");

        Assert.Equal("Copper is south of the ridge", doc.Note);
    }

    [Fact]
    public void SetNote_CanClearToEmpty()
    {
        var doc = new ScribeDocument();
        doc.SetNote("something");

        doc.SetNote("");

        Assert.Equal("", doc.Note);
    }
}
