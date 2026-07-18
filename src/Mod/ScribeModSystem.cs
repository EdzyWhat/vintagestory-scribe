using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Scribe;

/// <summary>
/// Mod entry point. Registers the lectern's block/block-entity classes and the network
/// channel used for server-authoritative document edits. Per-side setup (hotkeys, GUI,
/// lock bookkeeping) happens in <see cref="StartClientSide"/>/<see cref="StartServerSide"/>.
/// </summary>
public sealed class ScribeModSystem : ModSystem
{
    public const string NetworkChannelName = "scribe";

    private ICoreClientAPI? capi;
    private ICoreServerAPI? sapi;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.RegisterBlockClass("BlockScribeLectern", typeof(BlockScribeLectern));
        api.RegisterBlockEntityClass("ScribeLectern", typeof(BlockEntityScribeLectern));

        // Both message types must be registered in this same order on both sides.
        api.Network.RegisterChannel(NetworkChannelName)
            .RegisterMessageType<ScribeEditDocumentMessage>()
            .RegisterMessageType<ScribeReleaseLockMessage>();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;

        api.Network.GetChannel(NetworkChannelName)
            .SetMessageHandler<ScribeEditDocumentMessage>(OnClientReceivedEditReply);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        sapi = api;

        var channel = api.Network.GetChannel(NetworkChannelName);
        channel.SetMessageHandler<ScribeEditDocumentMessage>(OnServerReceivedEdit);
        channel.SetMessageHandler<ScribeReleaseLockMessage>(OnServerReceivedReleaseLock);
    }

    private void OnClientReceivedEditReply(ScribeEditDocumentMessage message)
    {
        var pos = new BlockPos(message.PosX, message.PosY, message.PosZ);
        if (capi?.World.BlockAccessor.GetBlockEntity<BlockEntityScribeLectern>(pos) is { } lectern)
        {
            lectern.HandleServerReply(message);
        }
    }

    private void OnServerReceivedEdit(IServerPlayer fromPlayer, ScribeEditDocumentMessage message)
    {
        var pos = new BlockPos(message.PosX, message.PosY, message.PosZ);
        if (sapi?.World.BlockAccessor.GetBlockEntity<BlockEntityScribeLectern>(pos) is { } lectern)
        {
            lectern.ApplyEdit(fromPlayer, message.DocumentBytes);
        }
    }

    private void OnServerReceivedReleaseLock(IServerPlayer fromPlayer, ScribeReleaseLockMessage message)
    {
        var pos = new BlockPos(message.PosX, message.PosY, message.PosZ);
        if (sapi?.World.BlockAccessor.GetBlockEntity<BlockEntityScribeLectern>(pos) is { } lectern)
        {
            lectern.ReleaseLock(fromPlayer.PlayerUID);
        }
    }
}
