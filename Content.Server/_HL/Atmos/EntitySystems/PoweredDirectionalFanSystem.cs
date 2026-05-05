using Content.Server._HL.Atmos.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Power;
using Robust.Shared.GameObjects;

namespace Content.Server._HL.Atmos.EntitySystems;

public sealed class PoweredDirectionalFanSystem : EntitySystem
{
    [Dependency] private readonly AirtightSystem _airtight = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PoweredDirectionalFanComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<PoweredDirectionalFanComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<PoweredDirectionalFanComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<PoweredDirectionalFanComponent, ReAnchorEvent>(OnReAnchor);
    }

    private void OnInit(Entity<PoweredDirectionalFanComponent> ent, ref ComponentInit args)
    {
        UpdateAirtight(ent.Owner);
    }

    private void OnPowerChanged(Entity<PoweredDirectionalFanComponent> ent, ref PowerChangedEvent args)
    {
        UpdateAirtight(ent.Owner, args.Powered);
    }

    private void OnAnchorChanged(Entity<PoweredDirectionalFanComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateAirtight(ent.Owner);
    }

    private void OnReAnchor(Entity<PoweredDirectionalFanComponent> ent, ref ReAnchorEvent args)
    {
        UpdateAirtight(ent.Owner);
    }

    private void UpdateAirtight(EntityUid uid, bool? powered = null)
    {
        if (!TryComp<AirtightComponent>(uid, out var airtight))
            return;

        if (powered == null)
            powered = TryComp<Content.Server.Power.Components.ApcPowerReceiverComponent>(uid, out var receiver) && receiver.Powered;

        var anchored = Transform(uid).Anchored;
        _airtight.SetAirblocked((uid, airtight), powered.Value && anchored);
    }
}
