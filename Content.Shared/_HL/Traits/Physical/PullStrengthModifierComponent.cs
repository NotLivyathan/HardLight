namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Multiplies movement speed while the entity is actively pulling something.
/// Values above 1 improve pulling mobility; values below 1 reduce it.
/// </summary>
[RegisterComponent]
public sealed partial class PullStrengthModifierComponent : Component
{
    [DataField("multiplier")]
    public float Multiplier = 1f;
}
