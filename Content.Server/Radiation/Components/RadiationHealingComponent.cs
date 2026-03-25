using Robust.Shared.GameStates;

namespace Content.Server.Radiation.Components;

[RegisterComponent]
public sealed partial class RadiationHealingComponent : Component
{
    [DataField("healPerRad")]
    public float HealPerRad = 1f;
}