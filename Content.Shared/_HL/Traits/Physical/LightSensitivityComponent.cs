using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Modifies shadekin light-exposure burn and slowdown thresholds.
/// For non-shadekin species (MildLightSensitivity), light exposure is computed independently.
/// Burn damage scales as (LightExposure - BurnThreshold + 1) per tick.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class LightSensitivityComponent : Component
{
    /// <summary>
    /// Minimum LightExposure level at which burning starts. Replaces the default of 4.
    /// </summary>
    [DataField]
    public int BurnThreshold = 4;

    /// <summary>
    /// Minimum LightExposure level at which movement slowing starts. Replaces the default of 4.
    /// </summary>
    [DataField]
    public int SlowdownThreshold = 4;

    /// <summary>
    /// Speed multiplier applied to both walk and sprint when above SlowdownThreshold.
    /// </summary>
    [DataField]
    public float SpeedMultiplier = 0.9f;

    /// <summary>
    /// Computed light exposure level (0–4) for non-shadekin entities. Updated by LightSensitivitySystem.
    /// </summary>
    [ViewVariables]
    public float CurrentLightExposure;

    [AutoPausedField]
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public TimeSpan UpdateCooldown = TimeSpan.FromSeconds(1f);
}
