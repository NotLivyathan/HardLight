#nullable enable
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._DV.Abilities;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;

namespace Content.IntegrationTests.Tests._DV;

[TestFixture]
public sealed class CrawlUnderObjectsRegressionTest
{
    private const float Epsilon = 0.0001f;

    [Test]
    public async Task ToggleSqueezeRestoresFixtureState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var systems = server.ResolveDependency<IEntitySystemManager>();
        var crawl = systems.GetEntitySystem<CrawlUnderObjectsSystem>();
        var standing = systems.GetEntitySystem<StandingStateSystem>();

        EntityUid mob = default;
        Dictionary<string, FixtureState> baseline = new();
        Dictionary<string, FixtureState> squeezed = new();

        await server.WaitPost(() =>
        {
            mob = entMan.SpawnEntity("MobHuman", map.GridCoords);

            var fixtures = entMan.GetComponent<FixturesComponent>(mob);
            baseline = Snapshot(fixtures);

            var comp = entMan.GetComponent<CrawlUnderObjectsComponent>(mob);
            var shouldInflateBaseline = !MathHelper.CloseTo(comp.UnsqueezedRadiusScale, 1f);
            Assert.That(comp.BaselineInflationApplied, Is.EqualTo(shouldInflateBaseline),
                "Baseline inflation flag should match whether unsqueezed scaling is configured.");
        });

        await server.WaitPost(() =>
        {
            var comp = entMan.GetComponent<CrawlUnderObjectsComponent>(mob);
            Assert.That(crawl.TrySetEnabled((mob, comp), true));
            Assert.That(comp.Enabled, Is.True);

            var fixtures = entMan.GetComponent<FixturesComponent>(mob);
            squeezed = Snapshot(fixtures);
        });

        // Ensure squeeze actually changed at least one tracked fixture shape or mask.
        Assert.That(AnyFixtureChanged(baseline, squeezed), Is.True);

        await server.WaitPost(() =>
        {
            var comp = entMan.GetComponent<CrawlUnderObjectsComponent>(mob);
            Assert.That(crawl.TrySetEnabled((mob, comp), false));
            Assert.That(comp.Enabled, Is.False);
        });

        await server.WaitAssertion(() =>
        {
            var fixtures = entMan.GetComponent<FixturesComponent>(mob);
            var restored = Snapshot(fixtures);

            Assert.That(restored.Count, Is.EqualTo(baseline.Count));
            foreach (var (key, expected) in baseline)
            {
                Assert.That(restored.TryGetValue(key, out var actual), Is.True, $"Missing fixture '{key}' after restore.");
                AssertFixtureEqual(expected, actual, key);
            }
        });

        // Downed state should use squeeze-equivalent radius and restore when standing.
        await server.WaitPost(() =>
        {
            Assert.That(standing.Down(mob), Is.True, "Failed to enter downed state.");
        });

        await server.WaitAssertion(() =>
        {
            var fixtures = entMan.GetComponent<FixturesComponent>(mob);
            var downed = Snapshot(fixtures);
            Assert.That(AnyFixtureChanged(baseline, downed), Is.True,
                "Downed scaling should change at least one fixture compared to baseline.");
        });

        // Enabling squeeze while standing should be cleared when going down.
        await server.WaitPost(() =>
        {
            Assert.That(standing.Stand(mob, force: true), Is.True, "Failed to stand before downed reset check.");
            var comp = entMan.GetComponent<CrawlUnderObjectsComponent>(mob);
            Assert.That(crawl.TrySetEnabled((mob, comp), true), Is.True, "Failed to enable squeeze before downed reset check.");
            Assert.That(standing.Down(mob), Is.True, "Failed to enter downed state for squeeze reset check.");
        });

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<CrawlUnderObjectsComponent>(mob);
            Assert.That(comp.Enabled, Is.False, "Squeeze mode should auto-disable when downed.");
        });

        await server.WaitPost(() =>
        {
            Assert.That(standing.Stand(mob, force: true), Is.True, "Failed to stand back up.");
        });

        await server.WaitAssertion(() =>
        {
            var fixtures = entMan.GetComponent<FixturesComponent>(mob);
            var restored = Snapshot(fixtures);

            Assert.That(restored.Count, Is.EqualTo(baseline.Count));
            foreach (var (key, expected) in baseline)
            {
                Assert.That(restored.TryGetValue(key, out var actual), Is.True, $"Missing fixture '{key}' after downed cycle.");
                AssertFixtureEqual(expected, actual, key);
            }
        });

        // Repeated toggles should not cause any cumulative growth/drift.
        await server.WaitPost(() =>
        {
            var comp = entMan.GetComponent<CrawlUnderObjectsComponent>(mob);
            for (var i = 0; i < 10; i++)
            {
                Assert.That(crawl.TrySetEnabled((mob, comp), true), Is.True, $"Failed to enable squeeze at iteration {i}.");
                Assert.That(crawl.TrySetEnabled((mob, comp), false), Is.True, $"Failed to disable squeeze at iteration {i}.");
            }
        });

        await server.WaitAssertion(() =>
        {
            var fixtures = entMan.GetComponent<FixturesComponent>(mob);
            var restored = Snapshot(fixtures);

            Assert.That(restored.Count, Is.EqualTo(baseline.Count));
            foreach (var (key, expected) in baseline)
            {
                Assert.That(restored.TryGetValue(key, out var actual), Is.True, $"Missing fixture '{key}' after repeated toggles.");
                AssertFixtureEqual(expected, actual, key);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static Dictionary<string, FixtureState> Snapshot(FixturesComponent fixtures)
    {
        var result = new Dictionary<string, FixtureState>(fixtures.Fixtures.Count);

        foreach (var (key, fixture) in fixtures.Fixtures)
        {
            if (fixture.Shape is PhysShapeCircle circle)
            {
                result[key] = new FixtureState(true, circle.Position, circle.Radius, fixture.CollisionMask);
                continue;
            }

            result[key] = new FixtureState(false, Vector2.Zero, 0f, fixture.CollisionMask);
        }

        return result;
    }

    private static bool AnyFixtureChanged(Dictionary<string, FixtureState> baseline, Dictionary<string, FixtureState> current)
    {
        foreach (var (key, expected) in baseline)
        {
            if (!current.TryGetValue(key, out var actual))
                return true;

            if (!FixtureStateEqual(expected, actual))
                return true;
        }

        return baseline.Count != current.Count;
    }

    private static bool FixtureStateEqual(FixtureState expected, FixtureState actual)
    {
        if (expected.IsCircle != actual.IsCircle)
            return false;

        if (expected.Mask != actual.Mask)
            return false;

        if (!expected.IsCircle)
            return true;

        return NearlyEqual(expected.Position.X, actual.Position.X)
            && NearlyEqual(expected.Position.Y, actual.Position.Y)
            && NearlyEqual(expected.Radius, actual.Radius);
    }

    private static void AssertFixtureEqual(FixtureState expected, FixtureState actual, string fixtureKey)
    {
        Assert.That(actual.IsCircle, Is.EqualTo(expected.IsCircle), $"Fixture '{fixtureKey}' circle type changed.");
        Assert.That(actual.Mask, Is.EqualTo(expected.Mask), $"Fixture '{fixtureKey}' collision mask changed.");

        if (!expected.IsCircle)
            return;

        Assert.That(actual.Position.X, Is.EqualTo(expected.Position.X).Within(Epsilon), $"Fixture '{fixtureKey}' X position drifted.");
        Assert.That(actual.Position.Y, Is.EqualTo(expected.Position.Y).Within(Epsilon), $"Fixture '{fixtureKey}' Y position drifted.");
        Assert.That(actual.Radius, Is.EqualTo(expected.Radius).Within(Epsilon), $"Fixture '{fixtureKey}' radius drifted.");
    }

    private static bool NearlyEqual(float a, float b)
    {
        return MathF.Abs(a - b) <= Epsilon;
    }

    private readonly record struct FixtureState(bool IsCircle, Vector2 Position, float Radius, int Mask);
}
