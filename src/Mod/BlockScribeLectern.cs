using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Scribe;

/// <summary>
/// The lectern block. Stays thin — all interaction/document logic lives on
/// <see cref="BlockEntityScribeLectern"/>, mirroring the vanilla Sign block/block-entity split.
/// </summary>
public sealed class BlockScribeLectern : Block
{
    private WorldInteraction[] interactions = System.Array.Empty<WorldInteraction>();

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api.Side != EnumAppSide.Client) return;

        // Two hints, one per view mode -- matches the tooltip pattern vanilla containers use
        // (e.g. BlockLabeledChest's "shift+right-click: write" hint) so looking at a lectern
        // explains both interactions, not just "Lectern".
        interactions = ObjectCacheUtil.GetOrCreate(api, "scribeLecternBlockInteractions", () => new WorldInteraction[]
        {
            new WorldInteraction
            {
                ActionLangCode = "scribe:blockhelp-scribelectern-open",
                MouseButton = EnumMouseButton.Right,
            },
            new WorldInteraction
            {
                ActionLangCode = "scribe:blockhelp-scribelectern-edit",
                HotKeyCode = "shift",
                MouseButton = EnumMouseButton.Right,
            },
        });
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }

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
