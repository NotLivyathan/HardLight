using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.Server.StationRecords.Components;
using Content.Server.StationRecords.Systems;
using Content.Server._NF.Shipyard.Systems;
using Content.Server.Maps;
using Content.Shared.Shuttles.Save;
using Content.Shared.StationRecords;
using Content.Shared.VendingMachines;
using NUnit.Framework;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests._NF.Shipyard
{
    [TestFixture]
    public sealed class ShipyardGridSaveTest
    {
        [Test]
        public async Task TestAmbitionShipSave()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var entityManager = server.ResolveDependency<IEntityManager>();
            var mapSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedMapSystem>();
            var mapLoader = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MapLoaderSystem>();
            var shipyardGridSaveSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ShipyardGridSaveSystem>();

            await server.WaitPost(() =>
            {
                // Create a test map
                var mapUid = mapSystem.CreateMap(out var mapId);

                // Load the ambition ship
                var mapLoaded = mapLoader.TryLoadGrid(mapId, new ResPath("/Maps/_NF/Shuttles/Expedition/ambition.yml"), out var gridUid);

                Assert.That(mapLoaded, Is.True, "Should successfully load the ambition ship");
                Assert.That(gridUid, Is.Not.Null, "Should get a valid grid UID");

                // Test that the grid can be cleaned for saving without errors
                if (gridUid != null)
                    shipyardGridSaveSystem.CleanGridForSaving(gridUid.Value);

                // Check that vending machines have been deleted
                var vendingMachineQuery = entityManager.EntityQueryEnumerator<VendingMachineComponent>();
                var foundVendingMachine = false;

                while (vendingMachineQuery.MoveNext(out var vendingUid, out var vendingComp))
                {
                    var transform = entityManager.GetComponent<TransformComponent>(vendingUid);
                    if (gridUid != null && transform.GridUid == gridUid.Value)
                    {
                        foundVendingMachine = true;
                        break;
                    }
                }

                Assert.That(foundVendingMachine, Is.False, "No vending machines should remain in cleaned grid");

                // Clean up
                mapSystem.DeleteMap(mapId);
            });

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task TestPhysicsComponentsPreservedAfterCleaning()
        {
            await using var pair = await PoolManager.GetServerClient();
            var server = pair.Server;

            var entityManager = server.ResolveDependency<IEntityManager>();
            var mapSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedMapSystem>();
            var mapLoader = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MapLoaderSystem>();
            var shipyardGridSaveSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ShipyardGridSaveSystem>();

            await server.WaitPost(() =>
            {
                // Create a test map
                var mapUid = mapSystem.CreateMap(out var mapId);

                // Load the ambition ship
                var mapLoaded = mapLoader.TryLoadGrid(mapId, new ResPath("/Maps/_NF/Shuttles/Expedition/ambition.yml"), out var gridUid);

                Assert.That(mapLoaded, Is.True, "Should successfully load the ambition ship");
                Assert.That(gridUid, Is.Not.Null, "Should get a valid grid UID");

                if (gridUid != null)
                {
                    // Count entities with physics components before cleaning
                    var physicsQuery = entityManager.EntityQueryEnumerator<PhysicsComponent>();
                    var physicsEntitiesBeforeCleaning = 0;

                    while (physicsQuery.MoveNext(out var physicsUid, out var physicsComp))
                    {
                        var transform = entityManager.GetComponent<TransformComponent>(physicsUid);
                        if (transform.GridUid == gridUid.Value)
                        {
                            physicsEntitiesBeforeCleaning++;
                        }
                    }

                    // Clean the grid for saving (this was removing physics components before the fix)
                    shipyardGridSaveSystem.CleanGridForSaving(gridUid.Value);

                    // Count entities with physics components after cleaning
                    var physicsQueryAfter = entityManager.EntityQueryEnumerator<PhysicsComponent>();
                    var physicsEntitiesAfterCleaning = 0;

                    while (physicsQueryAfter.MoveNext(out var physicsUidAfter, out var physicsCompAfter))
                    {
                        var transformAfter = entityManager.GetComponent<TransformComponent>(physicsUidAfter);
                        if (transformAfter.GridUid == gridUid.Value)
                        {
                            physicsEntitiesAfterCleaning++;
                        }
                    }

                    // Physics components should be preserved after cleaning
                    Assert.That(physicsEntitiesAfterCleaning, Is.EqualTo(physicsEntitiesBeforeCleaning),
                        $"Physics components should be preserved after cleaning. Before: {physicsEntitiesBeforeCleaning}, After: {physicsEntitiesAfterCleaning}");

                    // Ensure we actually had some physics entities to test with
                    Assert.That(physicsEntitiesBeforeCleaning, Is.GreaterThan(0),
                        "Test ship should have entities with physics components to validate the test");
                }

                // Clean up
                mapSystem.DeleteMap(mapId);
            });

            await pair.CleanReturnAsync();
        }

        [Test]
        public async Task TestUsedStationRecordsConsoleDoesNotBreakShipSave()
        {
            await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
            var server = pair.Server;

            var entityManager = server.ResolveDependency<IEntityManager>();
            var mapManager = server.ResolveDependency<IMapManager>();
            var mapSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<SharedMapSystem>();
            var playerManager = server.ResolveDependency<IPlayerManager>();
            var recordsConsoleSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<GeneralStationRecordConsoleSystem>();
            var shipyardGridSaveSystem = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<ShipyardGridSaveSystem>();

            await server.WaitAssertion(() =>
            {
                var session = playerManager.Sessions.Single();
                var mapUid = mapSystem.CreateMap(out var mapId);
                var gridEnt = mapManager.CreateGridEntity(mapId);
                var gridUid = gridEnt.Owner;
                var gridComp = gridEnt.Comp;

                entityManager.RunMapInit(gridUid, entityManager.GetComponent<MetaDataComponent>(gridUid));

                mapSystem.SetTile(gridUid, gridComp, Vector2i.Zero, new Tile(1));

                var consoleUid = entityManager.SpawnEntity("ComputerStationRecords", new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
                var console = entityManager.GetComponent<GeneralStationRecordConsoleComponent>(consoleUid);

                recordsConsoleSystem.SetTransientState((consoleUid, console), 7, new StationRecordsFilter(StationRecordFilterType.Name, "applicant"));

                Assert.Multiple(() =>
                {
                    Assert.That(console.ActiveKey, Is.EqualTo(7));
                    Assert.That(console.Filter, Is.Not.Null);
                });

                var success = shipyardGridSaveSystem.TrySaveGridAsShip(gridUid, "UsedRecordsConsoleShip", session.UserId.ToString(), session);

                Assert.Multiple(() =>
                {
                    Assert.That(success, Is.True, "Ship save should succeed when station records console state has been populated.");
                    Assert.That(console.ActiveKey, Is.Null, "Transient selected record state should be cleared before serialization.");
                    Assert.That(console.Filter, Is.Null, "Transient filter state should be cleared before serialization.");
                });

                mapSystem.DeleteMap(mapId);
            });

            await pair.CleanReturnAsync();
        }
    }
}
