using Content.Server._NF.RoundNotifications.Events;
using Content.Server.Station.Components;
using Content.Server.StationEvents.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._NF.Shipyard.Systems;

public sealed partial class ShipyardSystem
{
    private void InitializeShuttleLifecycleCleanup()
    {
        // Every shipyard-purchased grid gets LinkedLifecycleGridParentComponent at purchase time
        // (see ShipyardSystem.Consoles.cs purchase paths). This makes it the most reliable marker
        // for "this is a player-owned shuttle grid we created a station wrapper for".
        SubscribeLocalEvent<LinkedLifecycleGridParentComponent, EntityTerminatingEvent>(OnPurchasedShuttleTerminating);

        // After round persistence has rehydrated records on round start, drop any whose grid
        // was not restored. Otherwise old records sit forever in the shuttle-records console.
        SubscribeLocalEvent<RoundStartedEvent>(OnRoundStartedPruneRecords);
    }

    private void OnPurchasedShuttleTerminating(EntityUid uid, LinkedLifecycleGridParentComponent component, ref EntityTerminatingEvent args)
    {
        // Only act on actual grids - the marker can in principle exist on other entities.
        if (!HasComp<MapGridComponent>(uid))
            return;

        // 1. Drop the shuttle record for this grid (sell path commented these out at every call site).
        var netUid = GetNetEntity(uid);
        _shuttleRecordsSystem.TryRemoveRecord(netUid);

        // 2. If this grid was the only grid attached to a shipyard-created station wrapper,
        //    delete the wrapper so it stops appearing in station-list UIs as a duplicate / empty husk.
        //    Real persistent stations have multiple grids and never become empty from a single ship sale.
        if (!TryComp<StationMemberComponent>(uid, out var member))
            return;

        var stationUid = member.Station;
        if (!TryComp<StationDataComponent>(stationUid, out var stationData))
            return;

        // Remove this grid from the station first so the empty-check below sees the post-removal state.
        _station.RemoveGridFromStation(stationUid, uid, stationData: stationData);

        if (stationData.Grids.Count == 0)
            _station.DeleteStation(stationUid, stationData);
    }

    private void OnRoundStartedPruneRecords(RoundStartedEvent ev)
    {
        var pruned = _shuttleRecordsSystem.PruneOrphanedRecords();
        if (pruned > 0)
            _sawmill.Info($"Pruned {pruned} orphaned shuttle record(s) at round start.");
    }
}
