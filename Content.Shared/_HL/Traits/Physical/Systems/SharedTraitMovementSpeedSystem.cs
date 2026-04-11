using Content.Shared.Movement.Systems;

namespace Content.Shared._HL.Traits.Physical.Systems;

/// <summary>
/// Applies trait-based baseline movement speed multipliers.
/// </summary>
public sealed class SharedTraitMovementSpeedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TraitMovementSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovement);
    }

    private void OnRefreshMovement(EntityUid uid, TraitMovementSpeedModifierComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.WalkMultiplier, component.SprintMultiplier);
    }
}
