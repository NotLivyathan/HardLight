using Content.Shared.Popups;
using Content.Shared.Procedural;
using Content.Shared.Salvage;
using Content.Shared.Salvage.Expeditions;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server._NF.Bank;

namespace Content.Server.Salvage.Expeditions;

public sealed class ExpeditionDiskPrinterSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ExpeditionDiskPrinterComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ExpeditionDiskPrinterComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ExpeditionDiskPrinterComponent, ExpeditionDiskPrinterPrintMessage>(OnPrint);
    }

    private void OnInit(EntityUid uid, ExpeditionDiskPrinterComponent component, ComponentInit args)
    {
        UpdateUserInterface(uid, component);
    }

    private void OnBeforeUiOpen(EntityUid uid, ExpeditionDiskPrinterComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUserInterface(uid, component);
    }

    private void UpdateUserInterface(EntityUid uid, ExpeditionDiskPrinterComponent component)
    {
        var difficulties = new List<ExpeditionDiskPrinterDifficultyEntry>();
        for (var i = 0; i < component.Difficulties.Count; i++)
        {
            var number = i + 1;
            var cost = number * 1000;
            difficulties.Add(new ExpeditionDiskPrinterDifficultyEntry(component.Difficulties[i], number, cost));
        }

        var state = new ExpeditionDiskPrinterBoundUserInterfaceState(difficulties);
        _ui.SetUiState(uid, ExpeditionDiskPrinterUiKey.Key, state);
    }

    private void OnPrint(EntityUid uid, ExpeditionDiskPrinterComponent component, ExpeditionDiskPrinterPrintMessage args)
    {
        if (!_prototypeManager.TryIndex<Content.Shared.Procedural.SalvageDifficultyPrototype>(args.DifficultyId, out var _))
        {
            _popupSystem.PopupEntity(Loc.GetString("expedition-disk-printer-invalid"), uid, PopupType.MediumCaution);
            return;
        }

        var difficultyIndex = component.Difficulties.IndexOf(args.DifficultyId);
        if (difficultyIndex < 0)
        {
            _popupSystem.PopupEntity(Loc.GetString("expedition-disk-printer-invalid"), uid, PopupType.MediumCaution);
            return;
        }

        var actor = args.Actor;
        if (!Exists(actor))
            return;

        if (!TryComp<BankAccountComponent>(actor, out _))
        {
            _popupSystem.PopupEntity(Loc.GetString("expedition-disk-printer-no-bank"), uid, actor);
            return;
        }

        var cost = (difficultyIndex + 1) * 1000;
        if (!_bank.TryBankWithdraw(actor, cost))
        {
            _popupSystem.PopupEntity(Loc.GetString("expedition-disk-printer-insufficient-funds", ("cost", BankSystemExtensions.ToSpesoString(cost))), uid, actor);
            return;
        }

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var disk = Spawn(component.DiskPrototype, xform.Coordinates);
        if (TryComp(disk, out ExpeditionDiskComponent? diskComp))
        {
            diskComp.Difficulty = args.DifficultyId;
            diskComp.DifficultyNumber = difficultyIndex + 1;
            diskComp.Seed = _random.Next();
            diskComp.MissionType = (Content.Shared.Salvage.SalvageMissionType)_random.NextByte((byte)Content.Shared.Salvage.SalvageMissionType.Max);
            diskComp.CooldownEnd = TimeSpan.Zero;
        }

        _audio.PlayPvs(_audio.ResolveSound(component.PrintSound), uid);
        _popupSystem.PopupEntity(Loc.GetString("expedition-disk-printer-printed"), uid, PopupType.Small);
    }
}
