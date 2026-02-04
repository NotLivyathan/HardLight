using System;
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.StationEvents.Events;

[UsedImplicitly]
public sealed class DynamicJobAllocationRule : StationEventSystem<DynamicJobAllocationRuleComponent>
{
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    protected override void Started(EntityUid uid, DynamicJobAllocationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);
        AdjustJobSlots(uid, component);
    }

    protected override void ActiveTick(EntityUid uid, DynamicJobAllocationRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        component.TimeSinceLastCheck += frameTime;

        if (component.TimeSinceLastCheck >= component.CheckInterval)
        {
            component.TimeSinceLastCheck = 0f;
            AdjustJobSlots(uid, component);
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        UpdateActiveRules();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected || e.NewStatus == SessionStatus.InGame)
            UpdateActiveRules();
    }

    private void UpdateActiveRules()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var component, out _))
        {
            AdjustJobSlots(uid, component);
        }
    }

    private void AdjustJobSlots(EntityUid uid, DynamicJobAllocationRuleComponent component)
    {
        if (!TryGetRandomStation(out var chosenStation, HasComp<StationJobsComponent>))
            return;

        // Count players who are actually in the game (not in lobby)
        var playerCount = _playerManager.NetworkedSessions.Count(x => x.AttachedEntity != null);

        // At 0-9 players, set all jobs to 0
        if (playerCount < 10)
        {
            _stationJobs.TrySetJobSlot(chosenStation.Value, component.MercenaryJob, 0);
            return;
        }

        // Calculate slots based on percentages (rounded up)
        var mercenarySlots = (int)Math.Ceiling(playerCount * component.MercenaryPercentage);

        // Apply caps
        mercenarySlots = Math.Min(mercenarySlots, component.MercenaryCap);

        // Set the job slots
        _stationJobs.TrySetJobSlot(chosenStation.Value, component.MercenaryJob, mercenarySlots);
    }
}