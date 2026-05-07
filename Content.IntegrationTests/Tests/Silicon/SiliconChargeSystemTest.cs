using System.Collections.Generic;
using Content.Server._EinsteinEngines.Silicon.Charge;
using Content.Server._EinsteinEngines.Silicon.Death;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._EinsteinEngines.Silicon.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Silicon;

[TestFixture]
[TestOf(typeof(SiliconChargeSystem))]
public sealed class SiliconChargeSystemTest
{
    [Test]
    public async Task GhostedSiliconWithoutBatteryDoesNotEnterChargeDeathTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mindSystem = entityManager.EntitySysManager.GetEntitySystem<SharedMindSystem>();

        EntityUid silicon = EntityUid.Invalid;
        EntityUid replacement = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            silicon = SpawnBatteryPoweredSilicon(entityManager);
            replacement = entityManager.SpawnEntity(null, new MapCoordinates());
            entityManager.EnsureComponent<MindContainerComponent>(replacement);

            var mind = mindSystem.CreateMind(null);
            mindSystem.TransferTo(mind, silicon, mind: mind);
            mindSystem.TransferTo(mind, replacement, mind: mind.Comp);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.GetComponent<MindContainerComponent>(silicon).HasMind, Is.False);
            Assert.That(entityManager.HasComponent<SleepingComponent>(silicon), Is.False);
            Assert.That(entityManager.HasComponent<ForcedSleepingComponent>(silicon), Is.False);
            Assert.That(entityManager.GetComponent<SiliconDownOnDeadComponent>(silicon).Dead, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ControlledSiliconWithoutBatteryStillEntersChargeDeathTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entityManager = server.ResolveDependency<IEntityManager>();
        var mindSystem = entityManager.EntitySysManager.GetEntitySystem<SharedMindSystem>();

        EntityUid silicon = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            silicon = SpawnBatteryPoweredSilicon(entityManager);

            var mind = mindSystem.CreateMind(null);
            mindSystem.TransferTo(mind, silicon, mind: mind);
        });

        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(entityManager.GetComponent<MindContainerComponent>(silicon).HasMind, Is.True);
            Assert.That(entityManager.HasComponent<SleepingComponent>(silicon), Is.True);
            Assert.That(entityManager.HasComponent<ForcedSleepingComponent>(silicon), Is.True);
            Assert.That(entityManager.GetComponent<SiliconDownOnDeadComponent>(silicon).Dead, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnBatteryPoweredSilicon(IEntityManager entityManager)
    {
        var silicon = entityManager.SpawnEntity(null, new MapCoordinates());
        var siliconComp = entityManager.EnsureComponent<SiliconComponent>(silicon);
        siliconComp.BatteryPowered = true;
        siliconComp.EntityType = SiliconType.Player;
        siliconComp.SpeedModifierThresholds = new Dictionary<int, float>
        {
            [0] = 1f,
        };

        entityManager.EnsureComponent<MindContainerComponent>(silicon);
        entityManager.EnsureComponent<SiliconDownOnDeadComponent>(silicon);
        return silicon;
    }
}