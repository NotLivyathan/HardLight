using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class OmniHandComponent : Component
    {
        /// <summary>
        ///    The action to add to the entity.
        ///   </summary>
        [DataField("actionproto"), AutoNetworkedField]
        public string ActionPrototype = "ActionToggleOmniHand";

        /// <summary>
        ///    What sword/item is spawned?
        ///   </summary>
        [DataField("swordproto"), AutoNetworkedField]
        public string SwordPrototype = "OmniHand";

        [DataField, AutoNetworkedField]
        public EntityUid? Action;

        public Dictionary<string, EntityUid?> Equipment = new();

    }
}
public sealed partial class OmniHandToggledEvent : InstantActionEvent
{

}
