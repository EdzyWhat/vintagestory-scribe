using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace Scribe;

/// <summary>
/// The lectern's editing GUI. This is a minimal placeholder (title bar only) wired up
/// end-to-end with persistence/networking/the lock in group 4; the real block-rendering
/// content (task/text rows, the collapsible tool panel, reorder, etc.) is built in group 5.
/// </summary>
public sealed class GuiDialogScribeLectern : GuiDialog
{
    private readonly BlockEntityScribeLectern lectern;

    public override string ToggleKeyCombinationCode => "scribelecterngui";

    public GuiDialogScribeLectern(ICoreClientAPI capi, BlockEntityScribeLectern lectern) : base(capi)
    {
        this.lectern = lectern;
        ComposeDialog();
    }

    private void ComposeDialog()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
        bgBounds.BothSizing = ElementSizing.FitToChildren;

        SingleComposer = capi.Gui.CreateCompo("scribeLectern", dialogBounds)
            .AddShadedDialogBG(bgBounds)
            .AddDialogTitleBar(Lang.Get("scribe-gui-title"), () => TryClose())
            .Compose();
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        SendReleaseLockPacket();
    }

    private void SendReleaseLockPacket()
    {
        capi.Network.GetChannel(ScribeModSystem.NetworkChannelName).SendPacket(new ScribeReleaseLockMessage
        {
            PosX = lectern.Pos.X,
            PosY = lectern.Pos.Y,
            PosZ = lectern.Pos.Z,
        });
    }
}
