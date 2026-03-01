using Content.Shared.Body;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._HL.Body;

[RegisterComponent, NetworkedComponent]
public sealed partial class BionicPartReplacerComponent : Component
{
    [DataField(required: true)]
    public ProtoId<OrganCategoryPrototype> TargetCategory;

    [DataField(required: true)]
    public EntProtoId ReplacementProto;

    [DataField]
    public bool ReplaceIfPresent = true;
}
