using Vintagestory.API.Common;

namespace Scribe;

/// <summary>
/// The lectern block. Stays thin — all interaction/document logic lives on
/// <see cref="BlockEntityScribeLectern"/>, mirroring the vanilla Sign block/block-entity split.
/// </summary>
public sealed class BlockScribeLectern : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityScribeLectern lectern)
        {
            bool wantEditor = byPlayer.Entity?.Controls?.ShiftKey == true;
            lectern.OnRightClick(byPlayer, wantEditor);
        }
        return true;
    }
}
