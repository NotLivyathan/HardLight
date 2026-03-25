using Content.Shared._HL.Traits.Physical;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;

namespace Content.Server._HL.Traits.Physical;

/// <summary>
/// Applies trait-driven pull speed modifiers while actively pulling an entity.
/// </summary>
public sealed class PullStrengthModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PullStrengthModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(EntityUid uid, PullStrengthModifierComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<PullerComponent>(uid, out var puller) || puller.Pulling == null)
            return;

        args.ModifySpeed(args.WalkSpeedModifier * comp.Multiplier,
            args.SprintSpeedModifier * comp.Multiplier);
    }
}
