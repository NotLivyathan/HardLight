namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Multiplies baseline eye zoom distance for this entity.
/// Values above 1 increase view distance, values below 1 decrease it.
/// </summary>
[RegisterComponent]
public sealed partial class TraitZoomModifierComponent : Component
{
    [DataField("multiplier")]
    public float Multiplier = 1f;

    /// <summary>
    /// Tracks whether the multiplier has already been applied to ContentEye.
    /// </summary>
    public bool Applied;
}
