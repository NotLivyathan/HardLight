namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Applies a power draw multiplier only when this entity is a cyborg chassis.
/// </summary>
[RegisterComponent]
public sealed partial class TraitCyborgPowerDrawModifierComponent : Component
{
    [DataField("multiplier")]
    public float Multiplier = 1f;
}
