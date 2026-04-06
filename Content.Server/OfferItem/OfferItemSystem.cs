using Content.Server.Popups;
using Content.Shared.Hands.Components;
using Content.Shared.Alert;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.OfferItem;
using Content.Shared.IdentityManagement;
using Robust.Shared.Player;

namespace Content.Server.OfferItem;

public sealed class OfferItemSystem : SharedOfferItemSystem
{
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!; // HardLight

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OfferItemComponent>();
        while (query.MoveNext(out var uid, out var offerItem))
        {
            if (!TryComp<HandsComponent>(uid, out var hands) || _handsSystem.GetActiveHand((uid, hands)) == null) // HardLight
                continue;

            if (offerItem.Hand != null &&
                !_handsSystem.TryGetHeldItem((uid, hands), offerItem.Hand, out _)) // HardLight
            {
                if (offerItem.Target != null)
                {
                    UnReceive(offerItem.Target.Value, offerItem: offerItem);
                    offerItem.IsInOfferMode = false;
                    Dirty(uid, offerItem);
                }
                else
                    UnOffer(uid, offerItem);
            }

            if (!offerItem.IsInReceiveMode)
            {
                _alertsSystem.ClearAlert(uid, offerItem.OfferAlert);
                continue;
            }

            _alertsSystem.ShowAlert(uid, offerItem.OfferAlert);
        }
    }

}
