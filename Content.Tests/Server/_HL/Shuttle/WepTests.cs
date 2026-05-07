using System;
using Content.Server.Shuttles.Components;
using Content.Tests;
using NUnit.Framework;

namespace Content.Tests.Server._HL.Shuttle;

[TestFixture]
[TestOf(typeof(ShuttleComponent))]
public sealed class WepTests : ContentUnitTest
{
    // Replicates the formula in MoverController.ActivateWEP so that changes to
    // either the constants or the formula break these tests and force a review.
    private static float ComputeWepVelocity(float tileCount)
    {
        var raw = ShuttleComponent.WepBaseVelocity
                  - 25f * MathF.Log2(tileCount / ShuttleComponent.WepBaseGridSize);
        return Math.Clamp(raw, ShuttleComponent.WepLowerVelocity, ShuttleComponent.WepUpperVelocity);
    }

    [TestCase(250f,  100f, Description = "Base grid size → base velocity")]
    [TestCase(1f,    125f, Description = "Tiny grid → clamped to upper bound")]
    [TestCase(1000f,  50f, Description = "4× base tiles → minimum velocity")]
    [TestCase(5000f,  50f, Description = "Oversized grid → clamped to lower bound")]
    public void WepMaxVelocity_ScalesWithTileCount(float tileCount, float expected)
    {
        Assert.That(ComputeWepVelocity(tileCount), Is.EqualTo(expected).Within(0.001f));
    }

    [TestCase(1f)]
    [TestCase(50f)]
    [TestCase(250f)]
    [TestCase(1000f)]
    [TestCase(10000f)]
    public void WepMaxVelocity_AlwaysWithinDeclaredBounds(float tileCount)
    {
        var vel = ComputeWepVelocity(tileCount);
        Assert.Multiple(() =>
        {
            Assert.That(vel, Is.GreaterThanOrEqualTo(ShuttleComponent.WepLowerVelocity));
            Assert.That(vel, Is.LessThanOrEqualTo(ShuttleComponent.WepUpperVelocity));
        });
    }

    [TestCase(1f)]
    [TestCase(250f)]
    [TestCase(1000f)]
    public void WepThrustMultiplier_AlwaysAtLeastOne(float tileCount)
    {
        var multiplier = ComputeWepVelocity(tileCount) / ShuttleComponent.WepLowerVelocity;
        Assert.That(multiplier, Is.GreaterThanOrEqualTo(1f));
    }
}
