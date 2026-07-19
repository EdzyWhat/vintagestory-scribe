using Atlas.Api;
using Atlas.XUnit;
using Scribe;
using Scribe.Core;

namespace Integration.Tests;

/// <summary>
/// skeuomorphic-lectern-gui task 8.1: a pinned task, applied through the real production
/// path (OnRightClick to acquire the lock, then ApplyEdit -- mirrors
/// ServerAuthoritativeEditScenarios's own pattern), is server-observable via
/// Document.Blocks[i].Pinned. RollbackWorld is enough here since this only asserts
/// in-memory state right after the edit, not a save/load round trip (that's
/// PersistenceScenarios).
/// </summary>
public class PinScenarios : AtlasScenarioBase
{
    [AtlasScenario(RollbackWorld = true)]
    public async Task Pinning_a_task_through_ApplyEdit_is_observable()
    {
        var pos = World.Spawn.Offset(5, 0, 0);
        World.SetBlock("scribe:scribelectern", pos);
        await World.Ticks(2);

        var player = await World.JoinPlayer("PinTester1");
        var lectern = World.BlockEntityAt<BlockEntityScribeLectern>(pos);
        Assert.NotNull(lectern);

        lectern!.OnRightClick(player.Player, wantEditor: true);

        var doc = new ScribeDocument();
        doc.AddTask("Find copper");
        doc.TogglePinned(0);
        Assert.True(lectern.ApplyEdit(player.Player, ScribeDocumentCodec.Serialize(doc)));

        Assert.True(lectern.Document.Blocks[0].Pinned);
    }

    [AtlasScenario(RollbackWorld = true)]
    public async Task Unpinning_a_task_through_ApplyEdit_is_observable()
    {
        var pos = World.Spawn.Offset(5, 0, 0);
        World.SetBlock("scribe:scribelectern", pos);
        await World.Ticks(2);

        var player = await World.JoinPlayer("PinTester2");
        var lectern = World.BlockEntityAt<BlockEntityScribeLectern>(pos);
        Assert.NotNull(lectern);

        lectern!.OnRightClick(player.Player, wantEditor: true);

        var doc = new ScribeDocument();
        doc.AddTask("Find copper");
        doc.TogglePinned(0);
        doc.TogglePinned(0); // pin then unpin, same as the GUI's toggle affordance would
        Assert.True(lectern.ApplyEdit(player.Player, ScribeDocumentCodec.Serialize(doc)));

        Assert.False(lectern.Document.Blocks[0].Pinned);
    }
}
