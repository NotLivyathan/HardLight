using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._HL.Traits.Physical;

/// <summary>
/// Adds flat damage to outgoing melee attacks made by this entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeDamageModifierComponent : Component
{
    [DataField("flatBonus"), AutoNetworkedField]
    public FixedPoint2 FlatBonus = 2;

    [DataField("damageType"), AutoNetworkedField]
    public string DamageType = "Blunt";
}
