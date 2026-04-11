using System.Numerics;
using Content.Shared.Physics;
using Content.Shared.Mobs;
using Content.Shared.Standing;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.Shared._DV.Abilities;

public sealed partial class CrawlUnderObjectsSystem
{
    private void EnsureBaselineInflation(Entity<CrawlUnderObjectsComponent> ent)
    {
        if (ent.Comp.BaselineInflationApplied
            || ent.Comp.Enabled
            || MathHelper.CloseTo(ent.Comp.UnsqueezedRadiusScale, 1f)
            || !TryComp(ent, out Robust.Shared.Physics.FixturesComponent? fixtures))
            return;

        foreach (var (key, fixture) in fixtures.Fixtures)
        {
            if (fixture.Shape is not PhysShapeCircle circle)
                continue;

            _physics.SetPositionRadius(ent.Owner, key, fixture, circle, circle.Position, circle.Radius * ent.Comp.UnsqueezedRadiusScale, fixtures);
        }

        ent.Comp.BaselineInflationApplied = true;
    }

    private void OnDowned(Entity<CrawlUnderObjectsComponent> ent, ref Content.Shared.Standing.DownedEvent args)
    {
        // Drop crouch mode when transitioning to downed so lying state stays dedicated to furniture crawl visibility.
        if (ent.Comp.Enabled)
            SetEnabled(ent, false);

        if (ent.Comp.DownedScaleApplied
            || !TryComp(ent, out Robust.Shared.Physics.FixturesComponent? fixtures))
        {
            return;
        }

        var squeezeScale = GetSqueezeScale(ent.Comp);
        if (MathHelper.CloseTo(squeezeScale, 1f))
            return;

        CaptureCurrentCircles((ent.Owner, fixtures), ent.Comp.DownedCircles);
        ApplyCircles((ent.Owner, fixtures), ent.Comp.DownedCircles, squeezeScale);
        ent.Comp.DownedScaleApplied = true;
    }

    private void OnStood(Entity<CrawlUnderObjectsComponent> ent, ref Content.Shared.Standing.StoodEvent args)
    {
        if (!ent.Comp.DownedScaleApplied
            || !TryComp(ent, out Robust.Shared.Physics.FixturesComponent? fixtures))
        {
            return;
        }

        RestoreCircles((ent.Owner, fixtures), ent.Comp.DownedCircles);
        ent.Comp.DownedCircles.Clear();
        ent.Comp.DownedScaleApplied = false;
    }

    private void OnCrawlingUpdated(Entity<Robust.Shared.Physics.FixturesComponent> ent, ref CrawlingUpdatedEvent args)
    {
        var squeezeScale = GetSqueezeScale(args.Comp);
        var modifyGeometry = !MathHelper.CloseTo(squeezeScale, 1f);

        if (args.Enabled)
        {
            if (modifyGeometry)
            {
                CaptureCurrentCircles(ent, args.Comp.ChangedCircles);
                ApplyCircles(ent, args.Comp.ChangedCircles, squeezeScale);
            }
            else
            {
                args.Comp.ChangedCircles.Clear();
            }
        }
        else
        {
            if (modifyGeometry)
                RestoreCircles(ent, args.Comp.ChangedCircles);
            args.Comp.ChangedCircles.Clear();
        }
    }

    private void CaptureCurrentCircles(Entity<Robust.Shared.Physics.FixturesComponent> ent, List<(string key, Vector2 position, float radius)> output)
    {
        output.Clear();
        output.Capacity = Math.Max(output.Capacity, ent.Comp.Fixtures.Count);

        foreach (var (key, fixture) in ent.Comp.Fixtures)
        {
            if (fixture.Shape is not PhysShapeCircle circle)
                continue;

            output.Add((key, circle.Position, circle.Radius));
        }
    }

    private static float GetSqueezeScale(CrawlUnderObjectsComponent comp)
    {
        if (MathHelper.CloseTo(comp.UnsqueezedRadiusScale, 0f))
            return comp.SqueezeRadiusScale;

        return comp.SqueezeRadiusScale / comp.UnsqueezedRadiusScale;
    }

    private void ApplyCircles(Entity<Robust.Shared.Physics.FixturesComponent> ent, List<(string key, Vector2 position, float radius)> circles, float scale)
    {
        foreach (var (key, position, radius) in circles)
        {
            if (!ent.Comp.Fixtures.TryGetValue(key, out var fixture)
                || fixture.Shape is not PhysShapeCircle circle)
            {
                continue;
            }

            _physics.SetPositionRadius(ent.Owner, key, fixture, circle, position, radius * scale, ent.Comp);
        }
    }

    private void RestoreCircles(Entity<Robust.Shared.Physics.FixturesComponent> ent, List<(string key, Vector2 position, float radius)> circles)
    {
        foreach (var (key, position, radius) in circles)
        {
            if (!ent.Comp.Fixtures.TryGetValue(key, out var fixture)
                || fixture.Shape is not PhysShapeCircle circle)
            {
                continue;
            }

            _physics.SetPositionRadius(ent.Owner, key, fixture, circle, position, radius, ent.Comp);
        }
    }
}
