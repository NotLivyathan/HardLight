using System.Linq;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Components;
using Content.Server.Access.Systems;
using Content.Shared.StationRecords;
using Robust.Server.GameObjects;
using Content.Shared.Roles; // Frontier
using Robust.Shared.Prototypes; // Frontier
using Content.Shared.Access.Systems; // Frontier
using Content.Server.Station.Components; // Frontier
using Content.Server._NF.Station.Components; // Frontier
using Content.Server.Administration.Logs; // Frontier
using Content.Shared.Database; // Frontier
using Content.Shared._NF.StationRecords; // Frontier
using Content.Shared._NF.Roles.Components;
using Content.Shared._NF.Shipyard.Components;

namespace Content.Server.StationRecords.Systems;

public sealed class GeneralStationRecordConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;
    [Dependency] private readonly StationJobsSystem _stationJobsSystem = default!; // Frontier
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!; // Frontier
    [Dependency] private readonly IPrototypeManager _proto = default!; // Frontier
    [Dependency] private readonly IAdminLogManager _adminLog = default!; // Frontier

    public override void Initialize()
    {
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordModifiedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, AfterGeneralRecordCreatedEvent>(UpdateUserInterface);
        SubscribeLocalEvent<GeneralStationRecordConsoleComponent, RecordRemovedEvent>(UpdateUserInterface);

        Subs.BuiEvents<GeneralStationRecordConsoleComponent>(GeneralStationRecordConsoleKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(UpdateUserInterface);
            subs.Event<SelectStationRecord>(OnKeySelected);
            subs.Event<SetStationRecordFilter>(OnFiltersChanged);
            subs.Event<DeleteStationRecord>(OnRecordDelete);
            subs.Event<AdjustStationJobMsg>(OnAdjustJob); // Frontier
            subs.Event<SetStationAdvertisementMsg>(OnAdvertisementChanged); // Frontier
        });
    }

    private void OnRecordDelete(Entity<GeneralStationRecordConsoleComponent> ent, ref DeleteStationRecord args)
    {
        if (!ent.Comp.CanDeleteEntries)
            return;

        if (_stationRecords.TryGetAuthoritativeRecords(out var recordsStation, out _)) // HardLight
            _stationRecords.RemoveRecord(new StationRecordKey(args.Id, recordsStation));

        UpdateUserInterface(ent); // Apparently an event does not get raised for this.
    }

    private void UpdateUserInterface<T>(Entity<GeneralStationRecordConsoleComponent> ent, ref T args)
    {
        UpdateUserInterface(ent);
    }

    // TODO: instead of copy paste shitcode for each record console, have a shared records console comp they all use
    // then have this somehow play nicely with creating ui state
    // if that gets done put it in StationRecordsSystem console helpers section :)
    private void OnKeySelected(Entity<GeneralStationRecordConsoleComponent> ent, ref SelectStationRecord msg)
    {
        ent.Comp.ActiveKey = msg.SelectedKey;
        UpdateUserInterface(ent);
    }

    // Frontier: Job counts, advertisements
    private void OnAdjustJob(Entity<GeneralStationRecordConsoleComponent> ent, ref AdjustStationJobMsg msg)
    {
        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is EntityUid station)
        {
            if (!TryComp(station, out StationJobsComponent? stationJobs))
            {
                UpdateUserInterface(ent);
                return;
            }

            if (!_stationJobsSystem.IsShipCrewHiringStation(station))
            {
                UpdateUserInterface(ent);
                return;
            }

            if (!CanEditStationJobs(msg.Actor, ent.Owner, station, stationJobs))
            {
                UpdateUserInterface(ent);
                return;
            }

            if (!_stationJobsSystem.IsAdvertisedLateJoinJob(station, msg.JobProto))
            {
                UpdateUserInterface(ent);
                return;
            }

            _stationJobsSystem.TryAdjustJobCapacity(station, msg.JobProto, msg.Amount, clamp: true, stationJobs: stationJobs);

            UpdateUserInterface(ent);
        }
    }
    private void OnFiltersChanged(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationRecordFilter msg)
    {
        if (ent.Comp.Filter == null ||
            ent.Comp.Filter.Type != msg.Type || ent.Comp.Filter.Value != msg.Value)
        {
            ent.Comp.Filter = new StationRecordsFilter(msg.Type, msg.Value);
            UpdateUserInterface(ent);
        }
    }

    public void SetTransientState(Entity<GeneralStationRecordConsoleComponent> ent, uint? activeKey, StationRecordsFilter? filter)
    {
        ent.Comp.ActiveKey = activeKey;
        ent.Comp.Filter = filter;
    }

    public void ClearTransientStateOnGrid(EntityUid gridUid)
    {
        var query = EntityManager.EntityQueryEnumerator<GeneralStationRecordConsoleComponent, TransformComponent>();
        while (query.MoveNext(out _, out var console, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            console.ActiveKey = null;
            console.Filter = null;
        }
    }

    private void OnAdvertisementChanged(Entity<GeneralStationRecordConsoleComponent> ent, ref SetStationAdvertisementMsg msg)
    {
        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is EntityUid station
            && TryComp<ExtraShuttleInformationComponent>(station, out var vesselInfo))
        {
            if (!CanEditShipRecords(msg.Actor, ent.Owner))
            {
                UpdateUserInterface(ent);
                return;
            }

            vesselInfo.Advertisement = msg.Advertisement;
            _adminLog.Add(LogType.ShuttleInfoChanged, $"{ToPrettyString(msg.Actor):actor} set their shuttle {ToPrettyString(station)}'s ad text to {vesselInfo.Advertisement}");
            UpdateUserInterface(ent);
            _stationJobsSystem.UpdateJobsAvailable(); // Nasty - ideally this sends out partial information - one ship changed its advertisement.
        }
    }
    // End Frontier: job counts, advertisements

    private void UpdateUserInterface(Entity<GeneralStationRecordConsoleComponent> ent)
    {
        var (uid, console) = ent;
        var owningStation = _station.GetOwningStation(uid);

        // Frontier: jobs, advertisements
        IReadOnlyDictionary<ProtoId<JobPrototype>, int?>? jobList = null;
        string? advertisement = null;

        if (owningStation != null)
        {
            if (_stationJobsSystem.IsShipCrewHiringStation(owningStation.Value)
                && TryComp<StationJobsComponent>(owningStation.Value, out var stationJobs))
                jobList = _stationJobsSystem.GetJobs(owningStation.Value, stationJobs);

            if (TryComp<ExtraShuttleInformationComponent>(owningStation.Value, out var extraVessel))
                advertisement = extraVessel.Advertisement;
        }

        EntityUid stationUid;
        StationRecordsComponent? stationRecords;
        Dictionary<uint, string> listing;

        if (owningStation != null
            && TryComp<ExtraShuttleInformationComponent>(owningStation.Value, out _)
            && TryBuildShipRecordListing(owningStation.Value, console.Filter, out stationUid, out stationRecords, out var shipListing))
        {
            listing = shipListing;
        }
        else
        {
            if (!_stationRecords.TryGetAuthoritativeRecords(out stationUid, out stationRecords)) // HardLight: TryComp<StationRecordsComponent>(owningStation<_stationRecords.TryGetAuthoritativeRecords; added out var stationUid
            {
                _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, new GeneralStationRecordConsoleState(null, null, null, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement)); // Frontier: add as many args as we can
                return;
            }

            listing = _stationRecords.BuildListing((stationUid, stationRecords), console.Filter); // HardLight: owningStation.Value<stationUid
        }

        switch (listing.Count)
        {
            case 0:
                var consoleState = new GeneralStationRecordConsoleState(null, null, null, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement); // Frontier: add as many args as we can
                _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, consoleState);
                return;
            default:
                if (console.ActiveKey == null || !listing.ContainsKey(console.ActiveKey.Value))
                    console.ActiveKey = listing.Keys.First();
                break;
        }

        if (console.ActiveKey is not { } id)
        {
            _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, new GeneralStationRecordConsoleState(null, null, listing, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement)); // Frontier: add as many args as we can
            return;
        }

        var key = new StationRecordKey(id, stationUid); // HardLight: owningStation.Value<stationUid
        _stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record, stationRecords);

        GeneralStationRecordConsoleState newState = new(id, record, listing, jobList, console.Filter, ent.Comp.CanDeleteEntries, advertisement);
        _ui.SetUiState(uid, GeneralStationRecordConsoleKey.Key, newState);
    }

    private bool TryBuildShipRecordListing(EntityUid station, StationRecordsFilter? filter, out Dictionary<uint, string> listing)
    {
        return TryBuildShipRecordListing(station, filter, out _, out _, out listing);
    }

    private bool TryBuildShipRecordListing(
        EntityUid station,
        StationRecordsFilter? filter,
        out EntityUid recordsStation,
        out StationRecordsComponent? stationRecords,
        out Dictionary<uint, string> listing)
    {
        recordsStation = EntityUid.Invalid;
        stationRecords = null;
        listing = new Dictionary<uint, string>();

        if (!_stationRecords.TryGetAuthoritativeRecords(out recordsStation, out stationRecords))
        {
            recordsStation = station;
            if (!TryComp(station, out stationRecords))
                return false;
        }

        var includedKeys = new HashSet<uint>();

        var trackedCrew = EntityQueryEnumerator<JobTrackingComponent, StationRecordKeyStorageComponent>();
        while (trackedCrew.MoveNext(out _, out var jobTracking, out var keyStorage))
        {
            if (jobTracking.SpawnStation != station
                || !jobTracking.Active
                || keyStorage.Key is not { } key)
            {
                continue;
            }

            TryAddShipRecordListingEntry(key, filter, listing, includedKeys, stationRecords);
        }

        var deedHolders = EntityQueryEnumerator<ShuttleDeedComponent, StationRecordKeyStorageComponent>();
        while (deedHolders.MoveNext(out _, out var shuttleDeed, out var keyStorage))
        {
            if (shuttleDeed.ShuttleUid is not { } shuttleNetEntity
                || !TryGetEntity(shuttleNetEntity, out var shuttleUid)
                || _station.GetOwningStation(shuttleUid) != station
                || keyStorage.Key is not { } key)
            {
                continue;
            }

            TryAddShipRecordListingEntry(key, filter, listing, includedKeys, stationRecords);
        }

        return true;
    }

    private void TryAddShipRecordListingEntry(
        StationRecordKey key,
        StationRecordsFilter? filter,
        Dictionary<uint, string> listing,
        HashSet<uint> includedKeys,
        StationRecordsComponent stationRecords)
    {
        if (!includedKeys.Add(key.Id)
            || !_stationRecords.TryGetRecord<GeneralStationRecord>(key, out var record, stationRecords)
            || _stationRecords.IsSkipped(filter, record))
        {
            return;
        }

        listing.Add(key.Id, record.Name);
    }

    private bool CanEditStationJobs(EntityUid actor, EntityUid console, EntityUid station, StationJobsComponent stationJobs)
    {
        if (_stationJobsSystem.IsShipCrewHiringStation(station))
            return CanEditShipRecords(actor, console);

        if (stationJobs.Groups.Count == 0 && stationJobs.Tags.Count == 0)
            return true;

        var accessSources = _access.FindPotentialAccessItems(actor);
        var access = _access.FindAccessTags(actor, accessSources);

        if (stationJobs.Tags.Any(access.Contains))
            return true;

        foreach (var group in stationJobs.Groups)
        {
            if (!_proto.TryIndex(group, out var accessGroup))
                continue;

            if (accessGroup.Tags.Any(access.Contains))
                return true;
        }

        return false;
    }

    private bool CanEditShipRecords(EntityUid actor, EntityUid target)
    {
        if (!_idCard.TryFindIdCard(actor, out var idCard)
            || !TryComp(idCard, out ShuttleDeedComponent? shuttleDeed)
            || shuttleDeed.ShuttleUid == null
            || !TryGetEntity(shuttleDeed.ShuttleUid.Value, out var shuttleUid))
        {
            return false;
        }

        var shuttleStation = _station.GetOwningStation(shuttleUid);
        var targetStation = _station.GetOwningStation(target);

        if (shuttleStation != null && targetStation != null)
            return shuttleStation == targetStation;

        return TryComp(target, out TransformComponent? targetXform)
            && shuttleUid == targetXform.GridUid;
    }
}
