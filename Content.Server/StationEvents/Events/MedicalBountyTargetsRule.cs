using Content.Server.StationEvents.Components;
using Content.Shared._HL.Rescue.Rescue;
using Content.Shared._NF.Roles.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class MedicalBountyTargetsRule : StationEventSystem<MedicalBountyTargetsRuleComponent>
{
    [Dependency] private readonly IPlayerManager _player = default!;

    protected override void Started(EntityUid uid, MedicalBountyTargetsRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station)
            || component.Entries.Count == 0
            || !PrototypeManager.TryIndex<DepartmentPrototype>(component.DepartmentId, out var department))
        {
            return;
        }

        var beacons = GetStationRescueBeacons(station.Value);
        if (beacons.Count == 0)
            return;

        var medicalWorkers = CountDepartmentWorkers(station.Value, department);
        if (medicalWorkers <= 0)
            return;

        var variation = RobustRandom.NextFloat(-component.Variance, component.Variance);
        var spawnCount = Math.Max(1, (int) MathF.Round(medicalWorkers * (1f + variation)));

        for (var i = 0; i < spawnCount; i++)
        {
            var beacon = RobustRandom.Pick(beacons);
            var entry = RobustRandom.Pick(component.Entries);
            Spawn(entry.PrototypeId, beacon);
        }
    }

    private int CountDepartmentWorkers(EntityUid station, DepartmentPrototype department)
    {
        var roles = department.Roles;
        var count = 0;

        var query = EntityQueryEnumerator<JobTrackingComponent, MindContainerComponent>();
        while (query.MoveNext(out _, out var tracking, out var mindContainer))
        {
            if (!tracking.Active
                || tracking.SpawnStation != station
                || tracking.Job is not { } jobId
                || !roles.Contains(jobId))
            {
                continue;
            }

            if (!mindContainer.HasMind
                || !TryComp<MindComponent>(mindContainer.Mind, out var mind)
                || mind.UserId is not { } userId
                || !_player.TryGetSessionById(userId, out var session)
                || session.Status != SessionStatus.InGame)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private List<EntityCoordinates> GetStationRescueBeacons(EntityUid station)
    {
        var beacons = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<RescueBeaconComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var transform))
        {
            if (CompOrNull<StationMemberComponent>(transform.GridUid)?.Station != station)
                continue;

            beacons.Add(transform.Coordinates);
        }

        return beacons;
    }
}
