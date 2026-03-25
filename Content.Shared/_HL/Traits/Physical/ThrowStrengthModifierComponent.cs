namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Multiplies throw distance for throws performed by this entity.
/// </summary>
[RegisterComponent]
public sealed partial class ThrowStrengthModifierComponent : Component
{
    [DataField("multiplier")]
    public float Multiplier = 1f;
}
