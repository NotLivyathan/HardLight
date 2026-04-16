using Content.Server._NF.CryoSleep;
using Content.Server._HL.ColComm; // HardLight
using Content.Server.Afk;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._NF.Roles.Components;
using Content.Shared._NF.Roles.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network; // HardLight
using Robust.Shared.Prototypes;

namespace Content.Server._NF.Roles.Systems;

/// HardLight start: Rewritten
/// <summary>
/// Handles job slot open/close lifecycle for tracked station jobs.
/// All slot operations are routed through the persistent
/// <see cref="ColcommJobRegistryComponent"/> on the ColComm grid entity,
/// which survives round transitions and avoids stale EntityUid issues.
/// </summary>
// HardLight end
public sealed class JobTrackingSystem : SharedJobTrackingSystem
{
    [Dependency] private readonly IAfkManager _afk = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly ColcommJobSystem _colcommJobs = default!; // HardLight

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JobTrackingComponent, CryosleepBeforeMindRemovedEvent>(OnJobBeforeCryoEntered);
        SubscribeLocalEvent<JobTrackingComponent, MindAddedMessage>(OnJobMindAdded);
        SubscribeLocalEvent<JobTrackingComponent, MindRemovedMessage>(OnJobMindRemoved);
        SubscribeLocalEvent<ColcommRegistryRoundStartEvent>(OnColcommRegistryRoundStart); // HardLight
    }

    /// <summary>
    /// HardLight: After the ColComm registry resets to defaults, deduct slots for all crew
    /// that persisted from the previous round (Active = true in their JobTrackingComponent).
    /// </summary>
    private void OnColcommRegistryRoundStart(ColcommRegistryRoundStartEvent ev)
    {
        var activeCounts = new Dictionary<ProtoId<JobPrototype>, int>();

        var jobQuery = AllEntityQuery<JobTrackingComponent>();
        while (jobQuery.MoveNext(out _, out var job))
        {
            if (!job.Active || job.Job is not { } jobId)
                continue;

            activeCounts.TryGetValue(jobId, out var existing);
            activeCounts[jobId] = existing + 1;
        }

        if (activeCounts.Count > 0)
            _colcommJobs.DeductActiveRoles(ev.Colcomm, activeCounts);
    }

    // HardLight: If a player returns to their body (or an admin forces a mind in), consume a
    // ColComm slot unless we already track them.
    private void OnJobMindAdded(Entity<JobTrackingComponent> ent, ref MindAddedMessage ev)
    {
        // If the job is null, don't do anything.
        if (ent.Comp.Job is not { } job)
            return;

        if (!ent.Comp.Active)
        {
            ent.Comp.Active = true;
            RaiseLocalEvent(new JobTrackingStateChangedEvent());
        }

        if (!JobShouldBeReopened(job))
            return;

        if (!_player.TryGetSessionByEntity(ent, out var session))
            return;

        CloseJob(ent, session.UserId);
    }

    private void OnJobMindRemoved(Entity<JobTrackingComponent> ent, ref MindRemovedMessage ev)
    {
        if (ent.Comp.Job == null || !ent.Comp.Active || !JobShouldBeReopened(ent.Comp.Job.Value))
            return;

        OpenJob(ent, ev.Mind.Comp.UserId); // HardLight: Added ev.Mind.Comp.UserId
    }

    private void OnJobBeforeCryoEntered(Entity<JobTrackingComponent> ent, ref CryosleepBeforeMindRemovedEvent ev)
    {
        if (ent.Comp.Job == null || !ent.Comp.Active || !JobShouldBeReopened(ent.Comp.Job.Value))
            return;

        OpenJob(ent, ev.User); // HardLight: Added ev.User
        ev.DeleteEntity = true;
    }

    public void OpenJob(Entity<JobTrackingComponent> ent, NetUserId? userId = null) // HardLight: Added NetUserId? userId = null
    {
        if (ent.Comp.Job is not { } job)
            return;

        if (!_colcommJobs.TryGetColcommRegistry(out var colcomm)) // HardLight: TryComp<StationJobsComponent>(ent.Comp.SpawnStation, out var stationJobs)<_colcommJobs.TryGetColcommRegistry(out var colcomm)
            return;

        ent.Comp.Active = false;
        RaiseLocalEvent(new JobTrackingStateChangedEvent()); // HardLight

        if (!_colcommJobs.TryGetJobSlot(colcomm, job, out var slots) || slots == null) // HardLight
            return;

        // HardLight start
        // Only reopen if total occupancy (others still active) + current slots
        // hasn't already reached the configured cap.
        var occupiedJobs = GetNumberOfActiveRoles(job, includeAfk: true, exclude: ent, includeOutsideDefaultMap: true);
        var midRoundMax = colcomm.Comp.MidRoundMaxSlots.GetValueOrDefault(job, 0);

        if (slots + occupiedJobs >= midRoundMax)
            return;

        _colcommJobs.TryAdjustJobSlot(colcomm, job, 1);

        // Mirror the slot open to the physical station display (best-effort).
        if (TryComp<StationJobsComponent>(ent.Comp.SpawnStation, out var stationJobs))
            _stationJobs.TryAdjustJobSlot(ent.Comp.SpawnStation, job, 1, stationJobs: stationJobs);

        NetUserId? trackedUserId = userId;
        if (trackedUserId == null && _player.TryGetSessionByEntity(ent, out var session))
            trackedUserId = session.UserId;

        if (trackedUserId != null)
        {
            _colcommJobs.TryUntrackPlayerJob(colcomm, trackedUserId.Value, job);
            if (stationJobs != null)
                _stationJobs.TryUntrackPlayerJob(ent.Comp.SpawnStation, trackedUserId.Value, job, stationJobs);
        }
        // HardqLight end
    }

    // HardLight: CloseJob consumes a reopened slot and re-tracks the player in ColComm/station job registries.
    private void CloseJob(Entity<JobTrackingComponent> ent, NetUserId userId)
    {
        if (ent.Comp.Job is not { } job)
            return;

        if (!ent.Comp.Active)
        {
            ent.Comp.Active = true;
            RaiseLocalEvent(new JobTrackingStateChangedEvent());
        }

        if (!JobShouldBeReopened(job))
            return;

        if (!_colcommJobs.TryGetColcommRegistry(out var colcomm))
            return;

        if (_colcommJobs.IsPlayerJobTracked(colcomm, userId, job))
            return;

        if (!_colcommJobs.TryGetJobSlot(colcomm, job, out var slots) || slots == null)
            return;

        if (slots > 0)
        {
            _colcommJobs.TryAdjustJobSlot(colcomm, job, -1, clamp: true);

            if (!_stationJobs.IsPlayerJobTracked(ent.Comp.SpawnStation, userId, job)
                && TryComp<StationJobsComponent>(ent.Comp.SpawnStation, out var stationJobs))
            {
                _stationJobs.TryAdjustJobSlot(ent.Comp.SpawnStation, job, -1, clamp: true, stationJobs: stationJobs);
                _stationJobs.TryTrackPlayerJob(ent.Comp.SpawnStation, userId, job, stationJobs);
            }
        }

        _colcommJobs.TryTrackPlayerJob(colcomm, userId, job);
    }

    /// <summary>
    /// Returns the number of active players who match the requested Job Prototype Id.
    /// </summary>
    // HardLight start
    public int GetNumberOfActiveRoles(
        ProtoId<JobPrototype> jobProtoId,
        bool includeAfk = true,
        EntityUid? exclude = null,
        bool includeOutsideDefaultMap = false)
    // HardLight end
    {
        var activeJobCount = 0;
        var jobQuery = AllEntityQuery<JobTrackingComponent, MindContainerComponent, TransformComponent>();
        while (jobQuery.MoveNext(out var uid, out var job, out _, out var xform)) // HardLight: out var mindContainer<out _
        {
            if (exclude == uid)
                continue;

            if (!job.Active
                || job.Job != jobProtoId
                || (!includeOutsideDefaultMap && xform.MapID != _gameTicker.DefaultMap)) // Skip if they're in cryo or on expedition, // HardLight: Added !includeOutsideDefaultMap
                continue;

            if (_player.TryGetSessionByEntity(uid, out var session))
            {
                if (session.State.Status != SessionStatus.InGame)
                    continue;

                if (!includeAfk && _afk.IsAfk(session))
                    continue;
            }

            activeJobCount++;
        }
        return activeJobCount;
    }
}

// HardLight: An event raised when a job tracking component's active state changes, used for dynamic job allocation rules.
public sealed class JobTrackingStateChangedEvent : EntityEventArgs
{
}
