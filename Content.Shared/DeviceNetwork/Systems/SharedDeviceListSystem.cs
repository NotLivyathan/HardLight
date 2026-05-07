using System.Linq;
using Content.Shared.DeviceNetwork.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.DeviceNetwork.Systems;

public abstract class SharedDeviceListSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeviceListComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<DeviceListComponent, ComponentHandleState>(OnHandleState);
    }

    public IEnumerable<EntityUid> GetAllDevices(EntityUid uid, DeviceListComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return new EntityUid[] { };
        }
        return component.Devices;
    }

    private void OnGetState(EntityUid uid, DeviceListComponent component, ref ComponentGetState args)
    {
        args.State = new DeviceListComponentState(GetDeviceNetEntities(uid, component), component.IsAllowList, component.HandleIncomingPackets);
    }

    private void OnHandleState(EntityUid uid, DeviceListComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not DeviceListComponentState state)
            return;

        component.Devices.Clear();
        foreach (var device in state.Devices)
        {
            component.Devices.Add(GetEntity(device));
        }

        component.IsAllowList = state.IsAllowList;
        component.HandleIncomingPackets = state.HandleIncomingPackets;
    }

    private HashSet<NetEntity> GetDeviceNetEntities(EntityUid uid, DeviceListComponent component)
    {
        var removed = false;
        var netEntities = new HashSet<NetEntity>(component.Devices.Count);

        foreach (var device in component.Devices.ToArray())
        {
            if (device.IsValid()
                && !TerminatingOrDeleted(device)
                && HasComp<MetaDataComponent>(device)
                && TryGetNetEntity(device, out var netEntity)
                && netEntity != null)
            {
                netEntities.Add(netEntity.Value);
                continue;
            }

            component.Devices.Remove(device);
            removed = true;
        }

        if (removed)
            Dirty(uid, component);

        return netEntities;
    }
}

public sealed class DeviceListUpdateEvent : EntityEventArgs
{
    public DeviceListUpdateEvent(List<EntityUid> oldDevices, List<EntityUid> devices)
    {
        OldDevices = oldDevices;
        Devices = devices;
    }

    public List<EntityUid> OldDevices { get; }
    public List<EntityUid> Devices { get; }
}

public enum DeviceListUpdateResult : byte
{
    NoComponent,
    TooManyDevices,
    UpdateOk
}
