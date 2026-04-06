using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Shared._Goobstation.Factory.Slots;

/// <summary>
/// Abstraction over a specific hand of the machine.
/// </summary>
public sealed partial class AutomatedHand : AutomationSlot
{
    /// <summary>
    /// The name of the hand to use
    /// </summary>
    [DataField(required: true)]
    public string HandName = string.Empty;

    private SharedHandsSystem _hands;

    [ViewVariables]
    public bool HasHand => _hands.TryGetHand(Owner, HandName, out _); // HardLight

    public override void Initialize()
    {
        base.Initialize();

        _hands = EntMan.System<SharedHandsSystem>();
    }

    public override bool Insert(EntityUid item)
    {
        return HasHand // HardLight
            && base.Insert(item)
            && _hands.TryPickup(Owner, item, HandName); // HardLight: hand<HandName
    }

    public override bool CanInsert(EntityUid item)
    {
        return HasHand // HardLight
            && base.CanInsert(item)
            && _hands.CanPickupToHand(Owner, item, HandName); // HardLight: hand<HandName
    }

    public override EntityUid? GetItem(EntityUid? filter)
    {
        // HardLight start
        var item = _hands.GetHeldItem(Owner, HandName);
        if (item is not EntityUid heldItem
            || _filter.IsBlocked(filter, heldItem))
            return null;

        return heldItem;
        // HardLight end
    }
}
