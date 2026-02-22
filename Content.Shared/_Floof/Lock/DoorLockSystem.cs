using Content.Shared.Doors;
using Content.Shared.Emag.Components;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Robust.Shared.Network;


namespace Content.Shared._Floof.Lock;


/// <summary>
///     Prevents locked doors from being opened.
/// </summary>
public sealed class DoorLockSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LockComponent, BeforeDoorOpenedEvent>(OnDoorOpenAttempt);
    }

    private void OnDoorOpenAttempt(Entity<LockComponent> ent, ref BeforeDoorOpenedEvent args)
    {
        if (!ent.Comp.Locked || ent.Comp.BreakOnAccessBreaker && HasComp<EmaggedComponent>(ent)) // HardLight: Merged with upstream; BreakOnEmag<BreakOnAccessBreaker
            return;

        args.Cancel();
        // HardLight: Floof's ID lock already provides a specific denial popup,
        // so this suppresses the generic lock popup to avoid duplicate messages.
        var idLockEngaged = TryComp<IdLockComponent>(ent, out var idLock) && idLock.Enabled && idLock.State == IdLockComponent.LockState.Engaged;

        if (args.User is {} user && _net.IsServer && !idLockEngaged) // HardLight: Added && !idLockEngaged
            _popup.PopupClient(Loc.GetString("entity-storage-component-locked-message"), ent, user); // HardLight: PopupCursor<PopupClient; added ent
    }
}
