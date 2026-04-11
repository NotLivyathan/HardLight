using Content.Shared.GameTicking;
using Content.Shared._NF.Shipyard.Components;
using System;
using System.Collections.Generic;

namespace Content.Server._NF.Shipyard.Systems;

/// <summary>
/// Applies shipyard restrictions to configured job types when they spawn.
/// </summary>
public sealed class ShipyardJobRestrictionSystem : EntitySystem
{
    // Add future restricted jobs here.
    private static readonly HashSet<string> RestrictedJobIds = new(StringComparer.Ordinal)
    {
        "Passenger",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !RestrictedJobIds.Contains(ev.JobId))
            return;

        EnsureComp<ShipyardJobRestrictedComponent>(ev.Mob);
    }
}
