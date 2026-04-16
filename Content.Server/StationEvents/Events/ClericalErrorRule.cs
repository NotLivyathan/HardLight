using Content.Server.GameTicking.Rules.Components;
using Content.Server.StationEvents.Components;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.StationRecords;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class ClericalErrorRule : StationEventSystem<ClericalErrorRuleComponent>
{
    [Dependency] private readonly StationRecordsSystem _stationRecords = default!;

    protected override void Started(EntityUid uid, ClericalErrorRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var chosenStation))
            return;

        if (!_stationRecords.TryGetAuthoritativeRecords(out var station, out _)) // HardLight: Editted
            return;

        // HardLight start
        var allIds = new List<uint>();
        foreach (var (id, _) in _stationRecords.GetRecordsOfType<GeneralStationRecord>(station))
        {
            allIds.Add(id);
        }

        var recordCount = allIds.Count;
        // HardLight end

        if (recordCount == 0)
            return;

        var min = (int) Math.Max(1, Math.Round(component.MinToRemove * recordCount));
        var max = (int) Math.Max(min, Math.Round(component.MaxToRemove * recordCount));
        var toRemove = RobustRandom.Next(min, max);
        var keys = new List<uint>();
        for (var i = 0; i < toRemove; i++)
        {
            keys.Add(RobustRandom.Pick(allIds)); // HardLight: stationRecords.Records.Keys<allIds
        }

        foreach (var id in keys)
        {
            var key = new StationRecordKey(id, station); // HardLight: chosenStation.Value<stationRecords
            _stationRecords.RemoveRecord(key); // HardLight: Removed stationRecords
        }
    }
}
