using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartConditionComponent : Component
{
    [DataField]
    public ProtoId<OrganCategoryPrototype> Category;

    [DataField]
    public bool Inverse;
}
