using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._NF.SectorServices;
using Content.Server._NF.ShuttleRecords.Components;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._NF.ShuttleRecords;
using Content.Shared.Access.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;

// Suppress naming rule for _NF namespace prefix (modding convention)
#pragma warning disable IDE1006
namespace Content.Server._NF.ShuttleRecords;

public sealed partial class ShuttleRecordsSystem : SharedShuttleRecordsSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;


    public override void Initialize()
    {
        base.Initialize();
        InitializeShuttleRecords();
    }

    /**
     * Adds a record to the shuttle records list.
     * <param name="record">The record to add.</param>
     */
    public void AddRecord(ShuttleRecord record)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return;

        record.TimeOfPurchase = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
        component.ShuttleRecords[record.EntityUid] = record;
        RefreshStateForAll();
    }

    /**
     * Edits an existing record if one exists for the entity given in the Record
     * <param name="record">The record to update.</param>
     */
    public void TryUpdateRecord(ShuttleRecord record)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return;

        component.ShuttleRecords[record.EntityUid] = record;
        RefreshStateForAll();
    }

    /**
     * Removes a record for the given grid NetEntity, if one exists.
     * <param name="uid">NetEntity of the shuttle grid whose record should be removed.</param>
     * <returns>True if a record was removed.</returns>
     */
    public bool TryRemoveRecord(NetEntity uid)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return false;

        if (!component.ShuttleRecords.Remove(uid))
            return false;

        RefreshStateForAll();
        return true;
    }

    /**
     * Drops every record whose grid no longer resolves on the server.
     * Intended to clean up records carried forward by round persistence whose
     * underlying grid was destroyed or never restored.
     * <returns>The number of records pruned.</returns>
     */
    public int PruneOrphanedRecords()
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return 0;

        var toRemove = new List<NetEntity>();
        foreach (var (netUid, _) in component.ShuttleRecords)
        {
            if (!_entityManager.TryGetEntity(netUid, out var ent)
                || !_entityManager.EntityExists(ent.Value)
                || Terminating(ent.Value))
            {
                toRemove.Add(netUid);
            }
        }

        if (toRemove.Count == 0)
            return 0;

        foreach (var netUid in toRemove)
            component.ShuttleRecords.Remove(netUid);

        RefreshStateForAll();
        return toRemove.Count;
    }

    /**
     * Edits an existing record if one exists for the given entity
     * <param name="record">The record to add.</param>
     */
    public bool TryGetRecord(NetEntity uid, [NotNullWhen(true)] out ShuttleRecord? record)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component) ||
            !component.ShuttleRecords.ContainsKey(uid))
        {
            record = null;
            return false;
        }

        record = component.ShuttleRecords[uid];
        return true;
    }

    /**
     * Gets all shuttle records.
     * <returns>List of all shuttle records.</returns>
     */
    public List<ShuttleRecord> GetAllShuttleRecords()
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return new List<ShuttleRecord>();

        return component.ShuttleRecords.Values.ToList();
    }

    /**
     * Restores shuttle records from a list (used by persistence system).
     * <param name="records">The records to restore.</param>
     */
    public void RestoreShuttleRecords(List<ShuttleRecord> records)
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return;

        // Clear existing records
        component.ShuttleRecords.Clear();

        // Add all restored records
        foreach (var record in records)
        {
            component.ShuttleRecords[record.EntityUid] = record;
        }

        RefreshStateForAll();
    }

    /**
     * Clears all shuttle records (used for testing or maintenance).
     */
    public void ClearAllRecords()
    {
        if (!TryGetShuttleRecordsDataComponent(out var component))
            return;

        component.ShuttleRecords.Clear();
        RefreshStateForAll();
    }

    private bool TryGetShuttleRecordsDataComponent([NotNullWhen(true)] out SectorShuttleRecordsComponent? component)
    {
        var service = _sectorService.GetServiceEntity();
        if (service == EntityUid.Invalid)
        {
            component = null;
            return false;
        }

        if (!EntityManager.EntityExists(service) || Terminating(service))
        {
            component = null;
            return false;
        }

        if (_entityManager.EnsureComponent<SectorShuttleRecordsComponent>(
                uid: service,
                out var shuttleRecordsComponent))
        {
            component = shuttleRecordsComponent;
            return true;
        }

        component = null;
        return false;
    }
}
