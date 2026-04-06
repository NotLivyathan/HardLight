using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Cybernetics
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class SyndieOmniHandComponent : Component
    {
        /// <summary>
        ///    The action to add to the entity.
        ///   </summary>
        [DataField("actionproto"), AutoNetworkedField]
        public string ActionPrototype = "ActionToggleSyndieOmniHand";

        /// <summary>
        ///    What sword/item is spawned?
        ///   </summary>
        [DataField("swordproto"), AutoNetworkedField]
        public string SwordPrototype = "SyndieOmniHand";

        [DataField, AutoNetworkedField]
        public EntityUid? Action;

        public Dictionary<string, EntityUid?> Equipment = new();

    }
}
public sealed partial class SyndieOmniHandToggledEvent : InstantActionEvent
{

}
