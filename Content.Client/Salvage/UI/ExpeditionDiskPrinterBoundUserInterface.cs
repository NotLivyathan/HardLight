using Content.Shared.Salvage.Expeditions;
using Robust.Client.UserInterface;

namespace Content.Client.Salvage.UI;

public sealed class ExpeditionDiskPrinterBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ExpeditionDiskPrinterMenu? _menu;

    public ExpeditionDiskPrinterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ExpeditionDiskPrinterMenu>();
        _menu.OnPrintRequested += difficultyId => SendMessage(new ExpeditionDiskPrinterPrintMessage(difficultyId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ExpeditionDiskPrinterBoundUserInterfaceState msg)
            return;

        _menu?.Update(msg);
    }
}
