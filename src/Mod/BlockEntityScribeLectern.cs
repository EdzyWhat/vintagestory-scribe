using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Scribe.Core;

namespace Scribe;

/// <summary>
/// Holds this lectern's <see cref="ScribeDocument"/>, keyed by block position. Persistence
/// and sync go through <see cref="ToTreeAttributes"/>/<see cref="FromTreeAttributes"/> (the
/// vanilla Sign pattern). Edits are server-authoritative: the client never mutates its
/// local document directly — it sends a request, the server applies it and calls
/// <see cref="BlockEntity.MarkDirty"/> to persist and re-sync. Only one player may hold the
/// lectern's single-editor lock at a time (server-tracked, released on close/disconnect).
/// </summary>
public sealed class BlockEntityScribeLectern : BlockEntity
{
    private const string DocumentAttributeKey = "scribeDocument";

    public ScribeDocument Document { get; private set; } = new();

    /// <summary>Server-side only: the UID of the player currently editing, if any.</summary>
    private string? lockHolderUid;

    private GuiDialogScribeLectern? clientDialog;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);

        if (api is ICoreServerAPI sapi)
        {
            sapi.Event.PlayerDisconnect += OnPlayerDisconnect;
        }
    }

    public override void OnBlockRemoved()
    {
        base.OnBlockRemoved();

        if (Api is ICoreServerAPI sapi)
        {
            sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetBytes(DocumentAttributeKey, ScribeDocumentCodec.Serialize(Document));
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        var bytes = tree.GetBytes(DocumentAttributeKey);
        Document = ScribeDocumentCodec.TryDeserialize(bytes, out var doc) && doc is not null
            ? doc
            : new ScribeDocument();
    }

    /// <summary>
    /// Called from <see cref="BlockScribeLectern.OnBlockInteractStart"/> on whichever side is
    /// running (client immediately for responsiveness, server via the synced interaction).
    /// We only act server-side: request/grant the lock and reply to the requesting player.
    /// </summary>
    public void OnRightClick(IPlayer byPlayer)
    {
        if (Api is not ICoreServerAPI sapi || byPlayer is not IServerPlayer serverPlayer)
        {
            return;
        }

        if (lockHolderUid is not null && lockHolderUid != byPlayer.PlayerUID)
        {
            SendReply(sapi, serverPlayer, granted: false, "scribe-gui-locked");
            return;
        }

        lockHolderUid = byPlayer.PlayerUID;
        SendReply(sapi, serverPlayer, granted: true, refusalReason: null);
    }

    /// <summary>Server-side: applies a client-submitted document edit and re-syncs everyone.</summary>
    public void ApplyEdit(IServerPlayer fromPlayer, byte[]? documentBytes)
    {
        if (fromPlayer.PlayerUID != lockHolderUid)
        {
            return; // not the current editor; ignore silently (no lock held, no effect)
        }

        if (documentBytes is not null && ScribeDocumentCodec.TryDeserialize(documentBytes, out var doc) && doc is not null)
        {
            Document = doc;
            MarkDirty(redrawOnClient: true);
        }
    }

    /// <summary>Server-side: releases the lock, e.g. when the editing player closes the GUI.</summary>
    public void ReleaseLock(string playerUid)
    {
        if (lockHolderUid == playerUid)
        {
            lockHolderUid = null;
        }
    }

    private void OnPlayerDisconnect(IServerPlayer player)
    {
        ReleaseLock(player.PlayerUID);
    }

    private void SendReply(ICoreServerAPI sapi, IServerPlayer toPlayer, bool granted, string? refusalReason)
    {
        var reply = new ScribeEditDocumentMessage
        {
            PosX = Pos.X,
            PosY = Pos.Y,
            PosZ = Pos.Z,
            Granted = granted,
            RefusalReason = refusalReason,
            DocumentBytes = granted ? ScribeDocumentCodec.Serialize(Document) : null,
        };

        sapi.Network.GetChannel(ScribeModSystem.NetworkChannelName).SendPacket(reply, toPlayer);
    }

    /// <summary>Client-side: handles the server's reply to an open request.</summary>
    public void HandleServerReply(ScribeEditDocumentMessage message)
    {
        if (Api is not ICoreClientAPI capi)
        {
            return;
        }

        if (!message.Granted)
        {
            capi.TriggerIngameError(this, "scribe-lectern-locked", Lang.Get(message.RefusalReason ?? "scribe-gui-locked"));
            return;
        }

        if (ScribeDocumentCodec.TryDeserialize(message.DocumentBytes, out var doc) && doc is not null)
        {
            Document = doc;
        }

        clientDialog ??= new GuiDialogScribeLectern(capi, this);
        clientDialog.TryOpen();
    }
}
