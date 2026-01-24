using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Actions;

/// <summary>
/// Grants actions on MapInit and removes them on shutdown
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(ActionGrantSystem))]
public sealed partial class ActionGrantComponent : Component
{
    [DataField(required: true), AutoNetworkedField, AlwaysPushInheritance]
    public List<EntProtoId> Actions = new();

    [DataField, AutoNetworkedField]
    public List<EntityUid> ActionEntities = new();

    /// <summary>
    /// Actions will only be available if the item is in the clothing slot.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ActiveIfWorn;
}
