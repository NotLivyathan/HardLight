using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Conditions;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryPartRemovedConditionComponent : Component
{
    /// <summary>
    ///     Requires that the parent part can attach a new part to this slot.
    /// </summary>
    [DataField(required: true)]
    public string Connection = string.Empty;

    [DataField]
    public ProtoId<OrganCategoryPrototype> Category;
}
