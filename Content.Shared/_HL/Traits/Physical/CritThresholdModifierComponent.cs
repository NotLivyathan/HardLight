namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Applies a flat modifier to the Critical damage threshold.
/// Positive values make crit harder to reach, negative values make it easier.
/// </summary>
[RegisterComponent]
public sealed partial class CritThresholdModifierComponent : Component
{
    [DataField]
    public int CritThresholdDelta = 0;
}
