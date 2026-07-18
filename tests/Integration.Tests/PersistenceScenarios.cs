using Atlas.Api;
using Atlas.XUnit;
using Scribe;
using Vintagestory.API.MathTools;

namespace Integration.Tests;

/// <summary>
/// Task 4.3b: does a lectern's document survive a genuine save/load round trip?
///
/// Per the wiki's own guidance, the seed must come from a fixture, not an earlier scenario
/// method in the same class (xUnit gives no execution-order guarantee within a class, so a
/// seed-then-restart pair can flip order and fail intermittently). "fixtures/lectern.vcdbs"
/// is generated once via `atlas fixture` from FixtureBuilders.BuildsLecternWithDocumentFixture
/// (see README's "Running the Atlas suite" section for the exact command). This class boots
/// straight from that pre-seeded save and only asserts -- there is no seeding scenario here
/// to race against, so RestartWorld genuinely proves persistence rather than relying on order.
/// </summary>
[AtlasWorld(SaveFile = "fixtures/lectern.vcdbs")]
public class PersistenceScenarios : AtlasScenarioBase
{
    [AtlasScenario(RestartWorld = true)]
    public async Task Lectern_document_survives_a_server_restart()
    {
        var pos = World.Spawn.Offset(2, 0, 0);

        var lectern = World.BlockEntityAt<BlockEntityScribeLectern>(pos);
        Assert.NotNull(lectern);

        var blocks = lectern!.Document.Blocks;
        Assert.Equal(2, blocks.Count);
        Assert.Equal("Find copper", blocks[0].Text);
        Assert.True(blocks[0].Done);
        Assert.Equal("Left the mine at day 3", blocks[1].Text);
    }
}
