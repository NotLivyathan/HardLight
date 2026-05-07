// SPDX-FileCopyrightText: 2025 Ilya246
//
// SPDX-License-Identifier: MPL-2.0

using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._Mono.Detection;
using Content.Shared._Mono.Shuttle.FTL;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map.Components;
using System;
using System.Collections.Generic;

namespace Content.Server._Mono.Detection;

/// <summary>
///     Handles the logic for thermal signatures.
/// </summary>
public sealed class ThermalSignatureSystem : EntitySystem
{
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    private TimeSpan _updateInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _updateAccumulator = TimeSpan.FromSeconds(0);
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<ThermalSignatureComponent> _sigQuery;
    private EntityQuery<GunComponent> _gunQuery;
    private readonly Dictionary<EntityUid, float> _gridHeatAccumulator = new();
    private readonly HashSet<EntityUid> _dirtyGrids = new();
    // Last value we actually networked per grid. Used to skip Dirty() when the
    // per-tick change is below an audible threshold for radar UI, since the
    // half-second cadence × dozens of grids was generating large amounts of
    // network churn from negligible heat-decay deltas.
    private readonly Dictionary<EntityUid, float> _lastDirtiedHeat = new();
    private const float DirtyHeatEpsilon = 0.5f;

    public override void Initialize()
    {
        base.Initialize();

        // some of this could also be handled in shared but there's no point since PVS is a thing
        SubscribeLocalEvent<MachineThermalSignatureComponent, GetThermalSignatureEvent>(OnMachineGetSignature);
        SubscribeLocalEvent<PassiveThermalSignatureComponent, GetThermalSignatureEvent>(OnPassiveGetSignature);

        SubscribeLocalEvent<ThermalSignatureComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<PowerSupplierComponent, GetThermalSignatureEvent>(OnPowerGetSignature);
        SubscribeLocalEvent<ThrusterComponent, GetThermalSignatureEvent>(OnThrusterGetSignature);
        SubscribeLocalEvent<FTLDriveComponent, GetThermalSignatureEvent>(OnFTLGetSignature);

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _sigQuery = GetEntityQuery<ThermalSignatureComponent>();
        _gunQuery = GetEntityQuery<GunComponent>();
    }

    private void OnGunShot(Entity<ThermalSignatureComponent> ent, ref GunShotEvent args)
    {
        if (_gunQuery.TryComp(ent, out _))
            ent.Comp.StoredHeat += args.Ammo.Count;
    }

    private void OnMachineGetSignature(Entity<MachineThermalSignatureComponent> ent, ref GetThermalSignatureEvent args)
    {
        if (_power.IsPowered(ent.Owner))
            args.Signature += ent.Comp.Signature;
    }

    private void OnPassiveGetSignature(Entity<PassiveThermalSignatureComponent> ent, ref GetThermalSignatureEvent args)
    {
        args.Signature += ent.Comp.Signature;
    }

    private void OnPowerGetSignature(Entity<PowerSupplierComponent> ent, ref GetThermalSignatureEvent args)
    {
        args.Signature += ent.Comp.CurrentSupply * ent.Comp.HeatSignatureRatio;
    }

    private void OnThrusterGetSignature(Entity<ThrusterComponent> ent, ref GetThermalSignatureEvent args)
    {
        if (ent.Comp.Firing)
            args.Signature += ent.Comp.Thrust * ent.Comp.HeatSignatureRatio;
    }

    private void OnFTLGetSignature(Entity<FTLDriveComponent> ent, ref GetThermalSignatureEvent args)
    {
        var xform = Transform(ent);
        if (!TryComp<FTLComponent>(xform.GridUid, out var ftl))
            return;

        if (ftl.State == FTLState.Starting || ftl.State == FTLState.Cooldown)
            args.Signature += ent.Comp.ThermalSignature;
    }

    public override void Update(float frameTime)
    {
        _updateAccumulator += TimeSpan.FromSeconds(frameTime);
        if (_updateAccumulator < _updateInterval)
            return;
        _updateAccumulator -= _updateInterval;

        var interval = (float)_updateInterval.TotalSeconds;
        _gridHeatAccumulator.Clear();
        _dirtyGrids.Clear();

        var gridQuery = EntityQueryEnumerator<MapGridComponent, ThermalSignatureComponent>();
        while (gridQuery.MoveNext(out var uid, out _, out var sigComp))
        {
            if (sigComp.TotalHeat != 0f)
                _dirtyGrids.Add(uid);

            sigComp.TotalHeat = 0f;
        }

        var query = EntityQueryEnumerator<ThermalSignatureComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sigComp, out var xform))
        {
            var ev = new GetThermalSignatureEvent(interval);
            RaiseLocalEvent(uid, ref ev);
            sigComp.StoredHeat += ev.Signature * interval;
            sigComp.StoredHeat *= MathF.Pow(sigComp.HeatDissipation, interval);
            if (_gridQuery.HasComp(uid))
            {
                if (sigComp.StoredHeat == 0f)
                    continue;

                if (_gridHeatAccumulator.TryGetValue(uid, out var accumulatedGridHeat))
                    _gridHeatAccumulator[uid] = accumulatedGridHeat + sigComp.StoredHeat;
                else
                    _gridHeatAccumulator[uid] = sigComp.StoredHeat;

                continue;
            }

            sigComp.TotalHeat = sigComp.StoredHeat;

            if (sigComp.StoredHeat == 0f || xform.GridUid == null || !_gridQuery.TryComp(xform.GridUid.Value, out _))
                continue;

            var gridUid = xform.GridUid.Value;

            if (_gridHeatAccumulator.TryGetValue(gridUid, out var accumulated))
                _gridHeatAccumulator[gridUid] = accumulated + sigComp.StoredHeat;
            else
                _gridHeatAccumulator[gridUid] = sigComp.StoredHeat;
        }

        foreach (var (gridUid, totalHeat) in _gridHeatAccumulator)
        {
            if (!_gridQuery.TryComp(gridUid, out _))
                continue;

            var sigComp = EnsureComp<ThermalSignatureComponent>(gridUid);
            sigComp.TotalHeat += totalHeat;
            _dirtyGrids.Add(gridUid);
        }

        foreach (var gridUid in _dirtyGrids)
        {
            if (!_sigQuery.TryComp(gridUid, out var sigComp))
            {
                _lastDirtiedHeat.Remove(gridUid);
                continue;
            }

            var current = sigComp.TotalHeat;
            var last = _lastDirtiedHeat.TryGetValue(gridUid, out var prev) ? prev : 0f;

            // Always Dirty when crossing the zero boundary so clients reliably get the on/off
            // transition; otherwise only Dirty when the change is meaningful for the UI.
            if (MathF.Abs(current - last) < DirtyHeatEpsilon
                && (current == 0f) == (last == 0f))
            {
                continue;
            }

            _lastDirtiedHeat[gridUid] = current;
            Dirty(gridUid, sigComp); // sync to client
        }
    }
}
