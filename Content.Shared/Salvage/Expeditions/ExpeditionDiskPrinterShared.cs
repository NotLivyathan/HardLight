using Robust.Shared.Serialization;

namespace Content.Shared.Salvage.Expeditions;

[Serializable, NetSerializable]
public enum ExpeditionDiskPrinterUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ExpeditionDiskPrinterBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<ExpeditionDiskPrinterDifficultyEntry> Difficulties;

    public ExpeditionDiskPrinterBoundUserInterfaceState(List<ExpeditionDiskPrinterDifficultyEntry> difficulties)
    {
        Difficulties = difficulties;
    }
}

[Serializable, NetSerializable]
public sealed record ExpeditionDiskPrinterDifficultyEntry(string DifficultyId, int Number, int Cost);

[Serializable, NetSerializable]
public sealed class ExpeditionDiskPrinterPrintMessage : BoundUserInterfaceMessage
{
    public readonly string DifficultyId;

    public ExpeditionDiskPrinterPrintMessage(string difficultyId)
    {
        DifficultyId = difficultyId;
    }
}
