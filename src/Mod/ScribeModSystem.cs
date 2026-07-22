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
    public const string ClientConfigFileName = "scribe-client-config.json";

    private ICoreClientAPI? capi;
    private ICoreServerAPI? sapi;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.RegisterBlockClass("BlockScribeLectern", typeof(BlockScribeLectern));
        api.RegisterBlockEntityClass("ScribeLectern", typeof(BlockEntityScribeLectern));

        // All message types must be registered in this same order on both sides.
        api.Network.RegisterChannel(NetworkChannelName)
            .RegisterMessageType<ScribeEditDocumentMessage>()
            .RegisterMessageType<ScribeReleaseLockMessage>()
            .RegisterMessageType<ScribeRequestAccessMessage>()
            .RegisterMessageType<ScribeToggleTaskMessage>();
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;

        RegisterCustomIcons(api);

        api.Network.GetChannel(NetworkChannelName)
            .SetMessageHandler<ScribeEditDocumentMessage>(OnClientReceivedEditReply);
    }

    /// <summary>
    /// Registers the mod's custom SVG glyphs into the client's icon table so they can be drawn by
    /// code string like any built-in icon. This is REQUIRED, not optional: <c>IconUtil.DrawIcon</c>
    /// looks a code up in <c>CustomIcons</c> first, then falls through a switch of hardcoded built-in
    /// names -- with NO default case, so an unregistered code (e.g. "scribepin") silently draws
    /// nothing (see VSAPI-NOTES.md "Icon-button glyphs"). <c>SvgIconSource</c> wraps an asset path
    /// as a renderer that flood-recolors the whole SVG to the button's Font.Color at draw time, so
    /// each glyph is authored as a single flat black shape (assets/scribe/textures/icons/*.svg).
    ///
    /// The files MUST live under the <c>textures/</c> category: VS only scans assets under its 16
    /// hardcoded <c>AssetCategory</c> codes (blocktypes, textures, sounds, ... -- there is no
    /// "icons" category), so a file under a bare <c>icons/</c> folder is never loaded and TryGet
    /// returns null. Vanilla stores every SVG icon at <c>textures/icons/</c> (e.g.
    /// game:textures/icons/copy.svg) -- we match that. (Learned the hard way; see VSAPI-NOTES.md.)
    /// </summary>
    private static void RegisterCustomIcons(ICoreClientAPI api)
    {
        RegisterSvgIcon(api, "scribepin", new AssetLocation("scribe", "textures/icons/pin.svg"));
        RegisterSvgIcon(api, "scribegrip", new AssetLocation("scribe", "textures/icons/grip.svg"));
        RegisterSvgIcon(api, "scribeclose", new AssetLocation("scribe", "textures/icons/close.svg"));
        RegisterSvgIcon(api, "scribeedit", new AssetLocation("scribe", "textures/icons/edit.svg"));
    }

    /// <summary>
    /// Registers one SVG asset under a <c>CustomIcons</c> code, re-resolving the asset on every draw
    /// instead of capturing it once. This is REQUIRED: the obvious <c>CustomIcons[code] =
    /// api.Gui.Icons.SvgIconSource(asset)</c> captures the <see cref="IAsset"/> and re-reads its
    /// <c>.Data</c> at draw time -- but VS calls <c>AssetManager.UnloadAssets()</c> after startup,
    /// which sets <c>Data = null</c> on every non-patched asset (confirmed by decompile), so the
    /// captured asset's bytes are gone by the first draw and <c>rasterizeSvg</c> throws
    /// <c>ArgumentNullException("Asset Data is null. Is the asset loaded?")</c>, crashing the client
    /// mid-compose. <c>AssetManager.TryGet(loc, loadAsset: true)</c> re-loads an unloaded asset on
    /// demand (<c>if (!value.IsLoaded() &amp;&amp; loadAsset) value.Origin.TryLoadAsset(value)</c>),
    /// so re-resolving by <see cref="AssetLocation"/> inside the delegate self-heals. Compose is
    /// infrequent (not per-frame), so the re-fetch cost is negligible. See VSAPI-NOTES.md.
    /// </summary>
    private static void RegisterSvgIcon(ICoreClientAPI api, string code, AssetLocation loc)
    {
        api.Gui.Icons.CustomIcons[code] = (ctx, x, y, w, h, rgba) =>
        {
            var asset = api.Assets.TryGet(loc, loadAsset: true);
            if (asset?.Data is null)
            {
                // Missing/unloadable asset: draw nothing rather than throw. Logged once-ish so a
                // packaging mistake is visible without spamming (compose runs on open/recompose).
                api.Logger.Warning("[scribe] icon '{0}' asset {1} not loadable ({2}); drawing nothing",
                    code, loc, asset is null ? "not found" : "Data null");
                return;
            }
            api.Gui.Icons.SvgIconSource(asset)(ctx, x, y, w, h, rgba);
        };
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        sapi = api;

        var channel = api.Network.GetChannel(NetworkChannelName);
        channel.SetMessageHandler<ScribeEditDocumentMessage>(OnServerReceivedEdit);
        channel.SetMessageHandler<ScribeReleaseLockMessage>(OnServerReceivedReleaseLock);
        channel.SetMessageHandler<ScribeRequestAccessMessage>(OnServerReceivedRequestAccess);
        channel.SetMessageHandler<ScribeToggleTaskMessage>(OnServerReceivedToggleTask);
    }

    private void OnClientReceivedEditReply(ScribeEditDocumentMessage message)
    {
        if (capi is null) return;
        if (TryGetLectern(capi.World, message.PosX, message.PosY, message.PosZ) is { } lectern)
        {
            lectern.HandleServerReply(message);
        }
    }

    private void OnServerReceivedEdit(IServerPlayer fromPlayer, ScribeEditDocumentMessage message)
    {
        if (sapi is null) return;
        if (TryGetLectern(sapi.World, message.PosX, message.PosY, message.PosZ) is { } lectern)
        {
            if (!lectern.ApplyEdit(fromPlayer, message.DocumentBytes))
            {
                lectern.SendSaveFailedAck(sapi, fromPlayer);
            }
        }
    }

    private void OnServerReceivedReleaseLock(IServerPlayer fromPlayer, ScribeReleaseLockMessage message)
    {
        if (sapi is null) return;
        if (TryGetLectern(sapi.World, message.PosX, message.PosY, message.PosZ) is { } lectern)
        {
            lectern.ReleaseLock(fromPlayer.PlayerUID);
        }
    }

    private void OnServerReceivedRequestAccess(IServerPlayer fromPlayer, ScribeRequestAccessMessage message)
    {
        if (sapi is null) return;
        if (TryGetLectern(sapi.World, message.PosX, message.PosY, message.PosZ) is { } lectern)
        {
            lectern.OnRequestAccess(fromPlayer, message.WantEditor);
        }
    }

    private void OnServerReceivedToggleTask(IServerPlayer fromPlayer, ScribeToggleTaskMessage message)
    {
        if (sapi is null) return;
        if (TryGetLectern(sapi.World, message.PosX, message.PosY, message.PosZ) is { } lectern)
        {
            // Lock-free by design: any viewer may tick a task off, so unlike OnServerReceivedEdit
            // there is no lock check here (see ScribeToggleTaskMessage / BlockEntity.ToggleTaskFromReader).
            lectern.ToggleTaskFromReader(message.BlockIndex);
        }
    }

    private static BlockEntityScribeLectern? TryGetLectern(IWorldAccessor world, int x, int y, int z)
    {
        var pos = new BlockPos(x, y, z);
        return world.BlockAccessor.GetBlockEntity<BlockEntityScribeLectern>(pos);
    }
}
