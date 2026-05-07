using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class ShuttleBoundUserInterfaceState : BoundUserInterfaceState
{
    public NavInterfaceState NavState;
    public ShuttleMapInterfaceState MapState;
    public DockingInterfaceState DockState;
    public ExpeditionDiskInterfaceState ExpeditionDiskState;

    // <HL>
    public bool WepActive;
    public TimeSpan WepCooldownExpiry;
    // </HL>

    public ShuttleBoundUserInterfaceState(NavInterfaceState navState, ShuttleMapInterfaceState mapState, DockingInterfaceState dockState, ExpeditionDiskInterfaceState expeditionDiskState, bool wepActive = false, TimeSpan wepCooldownExpiry = default) // HL
    {
        NavState = navState;
        MapState = mapState;
        DockState = dockState;
        ExpeditionDiskState = expeditionDiskState;
        WepActive = wepActive; // HL
        WepCooldownExpiry = wepCooldownExpiry; // HL
    }
}
