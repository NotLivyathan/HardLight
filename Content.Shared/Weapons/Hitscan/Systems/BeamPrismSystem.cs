// SPDX-FileCopyrightText: 2026 HardLight contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class BeamPrismSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeamPrismComponent, HitScanReflectAttemptEvent>(OnBeamPrismHit);
    }

    private void OnBeamPrismHit(EntityUid uid, BeamPrismComponent component, ref HitScanReflectAttemptEvent args)
    {
        if (args.Reflected)
            return;

        if (!TryComp<TransformComponent>(uid, out var xform))
            return;

        args.Direction = _transform.GetWorldRotation(xform).ToWorldVec();
        args.Reflected = true;

        var state = GetBeamPrismState(args.SourceItem);
        _appearance.SetData(uid, BeamPrismVisuals.FiringState, state);
        _appearance.SetData(uid, BeamPrismVisuals.FiringToken, ++component.NextFiringToken);

        component.FiringOverlayExpireTime = _timing.CurTime + TimeSpan.FromMilliseconds(300);
        Timer.Spawn(TimeSpan.FromMilliseconds(300), () =>
        {
            if (!TryComp<BeamPrismComponent>(uid, out var latest) || _timing.CurTime < latest.FiringOverlayExpireTime)
                return;

            _appearance.RemoveData(uid, BeamPrismVisuals.FiringState);
            _appearance.RemoveData(uid, BeamPrismVisuals.FiringToken);
        });
    }

    private string GetBeamPrismState(EntityUid sourceItem)
    {
        if (!TryComp<HitscanBatteryAmmoProviderComponent>(sourceItem, out var hitscan))
            return "artillerygemfiring";

        var protoId = hitscan.Prototype.ToLowerInvariant();

        return protoId switch
        {
            "redlaser" => "artillerygemfiringred",
            "redlaserpractice" => "artillerygemfiringred",
            "redmediumlaser" => "artillerygemfiringred",
            "redlightlaser" => "artillerygemfiringred",
            "xraylaser" => "artillerygemfiringomni",
            "redheavylaser" => "artillerygemfiringred",
            "pulse" => "artillerygemfiringpulse",
            "redshuttlelaser" => "artillerygemfiringred",
            "lasersight" => "artillerygemfiringgreen",
            "wisplash" => "artillerygemfiringomni",
            "nfredlightlaser" => "artillerygemfiringred",
            "nfredmediumlaser" => "artillerygemfiringred",
            "nfredheavylaser" => "artillerygemfiringred",
            "nfredspeciallaser" => "artillerygemfiringyellow",
            "nfxraylaser" => "artillerygemfiringomni",
            "nfpulse" => "artillerygemfiringpulse",
            "sotekbeam" => "artillerygemfiringsotek",
            "nfredlaserpractice" => "artillerygemfiringred",
            "railgunlaser" => "artillerygemfiringyellow",
            "smallxraylaser" => "artillerygemfiringomni",
            "smalloverchargedlaser" => "artillerygemfiringyellow",
            "mediumionlaser" => "artillerygemfiringpulse",
            "mediumxraylaser" => "artillerygemfiringomni",
            "mediumoverchargedlaser" => "artillerygemfiringyellow",
            "abysslaser" => "artillerygemfiringabyss",
            "bloodcultlaser" => "artillerygemfiringred",
            "phalanxlaser" => "artillerygemfiringred",
            "apollolaser" => "artillerygemfiringred",
            "prometheuslaser" => "artillerygemfiringred",
            "tachyonlaser" => "artillerygemfiringgreen",
            "sunderlaser" => "artillerygemfiringproton",
            "gaussround" => "artillerygemfiring",
            "shredderround" => "artillerygemfiring",
            "explosivelaser" => "artillerygemfiringred",
            "shipglassingbeamplasmaprojectile" => "artillerygemfiringgreen",
            _ => "artillerygemfiring",
        };
    }
}
