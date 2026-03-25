using Robust.Shared.GameStates;

namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Applies a constant movement speed multiplier for trait-driven baseline speed changes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TraitMovementSpeedModifierComponent : Component
{
    [DataField("walkMultiplier"), AutoNetworkedField]
    public float WalkMultiplier = 1f;

    [DataField("sprintMultiplier"), AutoNetworkedField]
    public float SprintMultiplier = 1f;
}
