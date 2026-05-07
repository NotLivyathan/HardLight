using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._NF.Roles.Systems;
using Content.Server._NF.Station.Components;
using Content.Server.Maps;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Components;
using Content.Server.StationRecords.Systems;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared._NF.StationRecords;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
#nullable enable

namespace Content.IntegrationTests.Tests.StationRecords;

[TestFixture]
public sealed class GeneralStationRecordConsoleTest
{
        [TestPrototypes]
        private const string Prototypes =
                "- type: vessel\n"
                + "  id: TestRecordsShipVessel\n"
                + "  parent: BaseVessel\n"
                + "  name: Test Records Vessel\n"
                + "  description: Test records vessel.\n"
                + "  price: 1000\n"
                + "  category: Small\n"
                + "  group: Shipyard\n"
                + "  shuttlePath: /Maps/Test/empty.yml\n"
                + "  class:\n"
                + "  - Civilian\n"
                + "  engine:\n"
                + "  - Uranium\n"
                + "\n"
                + "- type: vessel\n"
                + "  id: TestRecordsCargoVessel\n"
                + "  parent: BaseVessel\n"
                + "  name: Test Records Cargo Vessel\n"
                + "  description: Test records cargo vessel.\n"
                + "  price: 1000\n"
                + "  category: Small\n"
                + "  group: Shipyard\n"
                + "  shuttlePath: /Maps/Test/empty.yml\n"
                + "  class:\n"
                + "  - Cargo\n"
                + "  engine:\n"
                + "  - Uranium\n"
                + "\n"
                + "- type: gameMap\n"
                + "  id: TestRecordsShipStation\n"
                + "  minPlayers: 0\n"
                + "  mapName: TestRecordsShipStation\n"
                + "  mapPath: /Maps/Test/empty.yml\n"
                + "  stations:\n"
                + "    Station:\n"
                + "      mapNameTemplate: TestRecordsShipStation\n"
                + "      stationProto: StandardFrontierVessel\n"
                + "      components:\n"
                + "        - type: ExtraShuttleInformation\n"
                + "          vessel: TestRecordsShipVessel\n"
                + "          advertisement: Original advertisement\n"
                + "        - type: StationJobs\n"
                + "          availableJobs:\n"
                + "            Mercenary: [0, 2]\n"
                + "\n"
                + "- type: gameMap\n"
                + "  id: TestRecordsCargoShipStation\n"
                + "  minPlayers: 0\n"
                + "  mapName: TestRecordsCargoShipStation\n"
                + "  mapPath: /Maps/Test/empty.yml\n"
                + "  stations:\n"
                + "    Station:\n"
                + "      mapNameTemplate: TestRecordsCargoShipStation\n"
                + "      stationProto: StandardFrontierVessel\n"
                + "      components:\n"
                + "        - type: ExtraShuttleInformation\n"
                + "          vessel: TestRecordsCargoVessel\n"
                + "          advertisement: Cargo ship advertisement\n";

    [Test]
    public async Task ShipAdvertisementRequiresMatchingShuttleDeedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
        var mapLoader = entitySystemManager.GetEntitySystem<MapLoaderSystem>();
        var stationSystem = entitySystemManager.GetEntitySystem<StationSystem>();
        var recordsConsoleSystem = entitySystemManager.GetEntitySystem<GeneralStationRecordConsoleSystem>();

        var station = EntityUid.Invalid;
        var gridUid = EntityUid.Invalid;
        var console = EntityUid.Invalid;
        var unauthorizedCard = EntityUid.Invalid;
        var authorizedCard = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var shipProto = prototypeManager.Index<GameMapPrototype>("TestRecordsShipStation");
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/Test/empty.yml"), out _, out var grids), Is.True);
            Assert.That(grids, Is.Not.Null);

            gridUid = grids!.First().Owner;
            station = stationSystem.InitializeNewStation(shipProto.Stations["Station"], new[] { gridUid }, "Records Ship");
            entityManager.EnsureComponent<StationMemberComponent>(gridUid).Station = station;
            console = entityManager.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            entityManager.EnsureComponent<GeneralStationRecordConsoleComponent>(console);
            ForceGridUid(entityManager.GetComponent<TransformComponent>(console), gridUid);

            unauthorizedCard = entityManager.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);
            authorizedCard = entityManager.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);

            var deed = entityManager.EnsureComponent<ShuttleDeedComponent>(authorizedCard);
            deed.ShuttleUid = entityManager.GetNetEntity(gridUid);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(stationSystem.GetOwningStation(console), Is.EqualTo(station));
            Assert.That(entitySystemManager.GetEntitySystem<StationJobsSystem>().IsShipCrewHiringStation(station), Is.True);
            Assert.That(CanEditShipRecords(recordsConsoleSystem, unauthorizedCard, console), Is.False);
            Assert.That(CanEditShipRecords(recordsConsoleSystem, authorizedCard, console), Is.True);
        });

        await server.WaitPost(() =>
        {
            var consoleEnt = (Entity<GeneralStationRecordConsoleComponent>) (console, entityManager.GetComponent<GeneralStationRecordConsoleComponent>(console));
            var msg = new SetStationAdvertisementMsg("Unauthorized advertisement")
            {
                Actor = unauthorizedCard,
            };

            InvokeAdvertisementHandler(recordsConsoleSystem, consoleEnt, msg);
        });

        await server.WaitAssertion(() =>
        {
            var shuttleInfo = entityManager.GetComponent<ExtraShuttleInformationComponent>(station);
            Assert.That(shuttleInfo.Advertisement, Is.EqualTo("Original advertisement"));
        });

        await server.WaitPost(() =>
        {
            var consoleEnt = (Entity<GeneralStationRecordConsoleComponent>) (console, entityManager.GetComponent<GeneralStationRecordConsoleComponent>(console));
            var msg = new SetStationAdvertisementMsg("Authorized advertisement")
            {
                Actor = authorizedCard,
            };

            InvokeAdvertisementHandler(recordsConsoleSystem, consoleEnt, msg);
        });

        await server.WaitAssertion(() =>
        {
            var shuttleInfo = entityManager.GetComponent<ExtraShuttleInformationComponent>(station);
            Assert.That(shuttleInfo.Advertisement, Is.EqualTo("Authorized advertisement"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CargoShipAdvertisementStillEditableTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
        var mapLoader = entitySystemManager.GetEntitySystem<MapLoaderSystem>();
        var stationSystem = entitySystemManager.GetEntitySystem<StationSystem>();
        var recordsConsoleSystem = entitySystemManager.GetEntitySystem<GeneralStationRecordConsoleSystem>();

        var station = EntityUid.Invalid;
        var gridUid = EntityUid.Invalid;
        var console = EntityUid.Invalid;
        var authorizedCard = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var shipProto = prototypeManager.Index<GameMapPrototype>("TestRecordsCargoShipStation");
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/Test/empty.yml"), out _, out var grids), Is.True);
            Assert.That(grids, Is.Not.Null);

            gridUid = grids!.First().Owner;
            station = stationSystem.InitializeNewStation(shipProto.Stations["Station"], new[] { gridUid }, "Cargo Records Ship");
            entityManager.EnsureComponent<StationMemberComponent>(gridUid).Station = station;
            console = entityManager.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            entityManager.EnsureComponent<GeneralStationRecordConsoleComponent>(console);
            ForceGridUid(entityManager.GetComponent<TransformComponent>(console), gridUid);

            authorizedCard = entityManager.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);
            var deed = entityManager.EnsureComponent<ShuttleDeedComponent>(authorizedCard);
            deed.ShuttleUid = entityManager.GetNetEntity(gridUid);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(stationSystem.GetOwningStation(console), Is.EqualTo(station));
            Assert.That(entitySystemManager.GetEntitySystem<StationJobsSystem>().IsShipCrewHiringStation(station), Is.False);
            Assert.That(CanEditShipRecords(recordsConsoleSystem, authorizedCard, console), Is.True);
        });

        await server.WaitPost(() =>
        {
            var consoleEnt = (Entity<GeneralStationRecordConsoleComponent>) (console, entityManager.GetComponent<GeneralStationRecordConsoleComponent>(console));
            var msg = new SetStationAdvertisementMsg("Updated cargo ship advertisement")
            {
                Actor = authorizedCard,
            };

            InvokeAdvertisementHandler(recordsConsoleSystem, consoleEnt, msg);
        });

        await server.WaitAssertion(() =>
        {
            var shuttleInfo = entityManager.GetComponent<ExtraShuttleInformationComponent>(station);
            Assert.That(shuttleInfo.Advertisement, Is.EqualTo("Updated cargo ship advertisement"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShipAdvertisementAllowsSameStationSecondaryGridConsoleTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
        var mapLoader = entitySystemManager.GetEntitySystem<MapLoaderSystem>();
        var stationSystem = entitySystemManager.GetEntitySystem<StationSystem>();
        var recordsConsoleSystem = entitySystemManager.GetEntitySystem<GeneralStationRecordConsoleSystem>();

        var station = EntityUid.Invalid;
        var primaryGridUid = EntityUid.Invalid;
        var secondaryGridUid = EntityUid.Invalid;
        var console = EntityUid.Invalid;
        var authorizedCard = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var shipProto = prototypeManager.Index<GameMapPrototype>("TestRecordsShipStation");
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/Test/empty.yml"), out _, out var primaryGrids), Is.True);
            Assert.That(primaryGrids, Is.Not.Null);
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/Test/empty.yml"), out _, out var secondaryGrids), Is.True);
            Assert.That(secondaryGrids, Is.Not.Null);

            primaryGridUid = primaryGrids!.First().Owner;
            secondaryGridUid = secondaryGrids!.First().Owner;

            station = stationSystem.InitializeNewStation(shipProto.Stations["Station"], new[] { primaryGridUid }, "Records Ship");
            entityManager.EnsureComponent<StationMemberComponent>(primaryGridUid).Station = station;
            entityManager.EnsureComponent<StationMemberComponent>(secondaryGridUid).Station = station;

            console = entityManager.SpawnEntity(null, new EntityCoordinates(secondaryGridUid, new Vector2(0.5f, 0.5f)));
            entityManager.EnsureComponent<GeneralStationRecordConsoleComponent>(console);
            ForceGridUid(entityManager.GetComponent<TransformComponent>(console), secondaryGridUid);

            authorizedCard = entityManager.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);
            var deed = entityManager.EnsureComponent<ShuttleDeedComponent>(authorizedCard);
            deed.ShuttleUid = entityManager.GetNetEntity(primaryGridUid);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            Assert.That(stationSystem.GetOwningStation(console), Is.EqualTo(station));
            Assert.That(CanEditShipRecords(recordsConsoleSystem, authorizedCard, console), Is.True);
        });

        await server.WaitPost(() =>
        {
            var consoleEnt = (Entity<GeneralStationRecordConsoleComponent>) (console, entityManager.GetComponent<GeneralStationRecordConsoleComponent>(console));
            var msg = new SetStationAdvertisementMsg("Secondary grid advertisement")
            {
                Actor = authorizedCard,
            };

            InvokeAdvertisementHandler(recordsConsoleSystem, consoleEnt, msg);
        });

        await server.WaitAssertion(() =>
        {
            var shuttleInfo = entityManager.GetComponent<ExtraShuttleInformationComponent>(station);
            Assert.That(shuttleInfo.Advertisement, Is.EqualTo("Secondary grid advertisement"));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShipJobAdjustmentRequiresMatchingShuttleDeedTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
        var mapLoader = entitySystemManager.GetEntitySystem<MapLoaderSystem>();
        var stationSystem = entitySystemManager.GetEntitySystem<StationSystem>();
        var stationJobsSystem = entitySystemManager.GetEntitySystem<StationJobsSystem>();
        var recordsConsoleSystem = entitySystemManager.GetEntitySystem<GeneralStationRecordConsoleSystem>();

        var station = EntityUid.Invalid;
        var gridUid = EntityUid.Invalid;
        var console = EntityUid.Invalid;
        var unauthorizedCard = EntityUid.Invalid;
        var authorizedCard = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var shipProto = prototypeManager.Index<GameMapPrototype>("TestRecordsShipStation");
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/Test/empty.yml"), out _, out var grids), Is.True);
            Assert.That(grids, Is.Not.Null);

            gridUid = grids!.First().Owner;
            station = stationSystem.InitializeNewStation(shipProto.Stations["Station"], new[] { gridUid }, "Records Ship");
            entityManager.EnsureComponent<StationMemberComponent>(gridUid).Station = station;
            console = entityManager.SpawnEntity(null, new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            entityManager.EnsureComponent<GeneralStationRecordConsoleComponent>(console);
            ForceGridUid(entityManager.GetComponent<TransformComponent>(console), gridUid);

            unauthorizedCard = entityManager.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);
            authorizedCard = entityManager.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);

            var deed = entityManager.EnsureComponent<ShuttleDeedComponent>(authorizedCard);
            deed.ShuttleUid = entityManager.GetNetEntity(gridUid);
        });

        await server.WaitRunTicks(1);

        int? initialMax = null;
        int? initialSlots = null;

        await server.WaitAssertion(() =>
        {
            Assert.That(stationJobsSystem.TryGetJobMidRoundMax(station, StationJobsSystem.ShipFreelancerInterviewJobId, out initialMax), Is.True);
            Assert.That(stationJobsSystem.TryGetJobSlot(station, StationJobsSystem.ShipFreelancerInterviewJobId, out initialSlots), Is.True);
        });

        await server.WaitPost(() =>
        {
            var consoleEnt = (Entity<GeneralStationRecordConsoleComponent>) (console, entityManager.GetComponent<GeneralStationRecordConsoleComponent>(console));
            var msg = new AdjustStationJobMsg(StationJobsSystem.ShipFreelancerInterviewJobId, 1)
            {
                Actor = unauthorizedCard,
            };

            InvokeJobAdjustmentHandler(recordsConsoleSystem, consoleEnt, msg);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(stationJobsSystem.TryGetJobMidRoundMax(station, StationJobsSystem.ShipFreelancerInterviewJobId, out var currentMax), Is.True);
            Assert.That(currentMax, Is.EqualTo(initialMax));
            Assert.That(stationJobsSystem.TryGetJobSlot(station, StationJobsSystem.ShipFreelancerInterviewJobId, out var currentSlots), Is.True);
            Assert.That(currentSlots, Is.EqualTo(initialSlots));
        });

        await server.WaitPost(() =>
        {
            var consoleEnt = (Entity<GeneralStationRecordConsoleComponent>) (console, entityManager.GetComponent<GeneralStationRecordConsoleComponent>(console));
            var msg = new AdjustStationJobMsg(StationJobsSystem.ShipFreelancerInterviewJobId, 1)
            {
                Actor = authorizedCard,
            };

            InvokeJobAdjustmentHandler(recordsConsoleSystem, consoleEnt, msg);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(stationJobsSystem.TryGetJobMidRoundMax(station, StationJobsSystem.ShipFreelancerInterviewJobId, out var currentMax), Is.True);
            Assert.That(currentMax, Is.EqualTo(initialMax + 1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShipRecordListingIncludesOnlyShipCrewAndDeedOwnerTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var entityManager = server.ResolveDependency<IEntityManager>();
        var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
        var mapLoader = entitySystemManager.GetEntitySystem<MapLoaderSystem>();
        var jobTrackingSystem = entitySystemManager.GetEntitySystem<JobTrackingSystem>();
        var stationSystem = entitySystemManager.GetEntitySystem<StationSystem>();
        var recordsSystem = entitySystemManager.GetEntitySystem<StationRecordsSystem>();
        var recordsConsoleSystem = entitySystemManager.GetEntitySystem<GeneralStationRecordConsoleSystem>();

        var shipStation = EntityUid.Invalid;
        var otherStation = EntityUid.Invalid;
        var shipGridUid = EntityUid.Invalid;
        var otherGridUid = EntityUid.Invalid;
        var shipCrew = EntityUid.Invalid;
        var outsideCrew = EntityUid.Invalid;
        var ownerCard = EntityUid.Invalid;

        await server.WaitPost(() =>
        {
            var shipProto = prototypeManager.Index<GameMapPrototype>("TestRecordsShipStation");
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/Test/empty.yml"), out _, out var shipGrids), Is.True);
            Assert.That(shipGrids, Is.Not.Null);
            Assert.That(mapLoader.TryLoadMap(new ResPath("/Maps/Test/empty.yml"), out _, out var otherGrids), Is.True);
            Assert.That(otherGrids, Is.Not.Null);

            shipGridUid = shipGrids!.First().Owner;
            otherGridUid = otherGrids!.First().Owner;

            shipStation = stationSystem.InitializeNewStation(shipProto.Stations["Station"], new[] { shipGridUid }, "Records Ship");
            otherStation = stationSystem.InitializeNewStation(shipProto.Stations["Station"], new[] { otherGridUid }, "Other Records Ship");

            entityManager.EnsureComponent<StationMemberComponent>(shipGridUid).Station = shipStation;
            entityManager.EnsureComponent<StationMemberComponent>(otherGridUid).Station = otherStation;

            shipCrew = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            outsideCrew = entityManager.SpawnEntity(null, MapCoordinates.Nullspace);
            ownerCard = entityManager.SpawnEntity("PassengerIDCard", MapCoordinates.Nullspace);

            var shipCrewKey = recordsSystem.AddRecordEntry(shipStation, new GeneralStationRecord
            {
                Name = "Ship Crew",
                JobTitle = "Mercenary",
                JobPrototype = "Mercenary",
            });
            recordsSystem.SetEntityKey(shipCrew, shipCrewKey);
            jobTrackingSystem.EnsureTrackedJob(shipCrew, "Mercenary", shipStation);

            var outsideCrewKey = recordsSystem.AddRecordEntry(otherStation, new GeneralStationRecord
            {
                Name = "Outside Crew",
                JobTitle = "Mercenary",
                JobPrototype = "Mercenary",
            });
            recordsSystem.SetEntityKey(outsideCrew, outsideCrewKey);
            jobTrackingSystem.EnsureTrackedJob(outsideCrew, "Mercenary", otherStation);

            var ownerKey = recordsSystem.AddRecordEntry(shipStation, new GeneralStationRecord
            {
                Name = "Ship Owner",
                JobTitle = "Captain",
                JobPrototype = "Captain",
            });
            recordsSystem.SetIdKey(ownerCard, ownerKey);
            var deed = entityManager.EnsureComponent<ShuttleDeedComponent>(ownerCard);
            deed.ShuttleUid = entityManager.GetNetEntity(shipGridUid);
        });

        await server.WaitRunTicks(1);

        await server.WaitAssertion(() =>
        {
            var listing = BuildShipRecordListing(recordsConsoleSystem, shipStation);

            Assert.That(listing.Values, Does.Contain("Ship Crew"));
            Assert.That(listing.Values, Does.Contain("Ship Owner"));
            Assert.That(listing.Values, Does.Not.Contain("Outside Crew"));
        });

        await pair.CleanReturnAsync();
    }

    private static void InvokeAdvertisementHandler(
        GeneralStationRecordConsoleSystem system,
        Entity<GeneralStationRecordConsoleComponent> console,
        SetStationAdvertisementMsg msg)
    {
        var method = typeof(GeneralStationRecordConsoleSystem).GetMethod(
            "OnAdvertisementChanged",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        object[] args = { console, msg };
        method!.Invoke(system, args);
    }

    private static void InvokeJobAdjustmentHandler(
        GeneralStationRecordConsoleSystem system,
        Entity<GeneralStationRecordConsoleComponent> console,
        AdjustStationJobMsg msg)
    {
        var method = typeof(GeneralStationRecordConsoleSystem).GetMethod(
            "OnAdjustJob",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);

        object[] args = { console, msg };
        method!.Invoke(system, args);
    }

    private static Dictionary<uint, string> BuildShipRecordListing(GeneralStationRecordConsoleSystem system, EntityUid station)
    {
        var method = typeof(GeneralStationRecordConsoleSystem).GetMethod(
            "TryBuildShipRecordListing",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types:
            [
                typeof(EntityUid),
                typeof(StationRecordsFilter),
                typeof(Dictionary<uint, string>).MakeByRefType(),
            ],
            modifiers: null);

        Assert.That(method, Is.Not.Null);

        object?[] args = { station, null, null };
        var success = (bool) method!.Invoke(system, args)!;
        Assert.That(success, Is.True);

        return (Dictionary<uint, string>) args[2]!;
    }

    private static bool CanEditShipRecords(GeneralStationRecordConsoleSystem system, EntityUid actor, EntityUid target)
    {
        var method = typeof(GeneralStationRecordConsoleSystem).GetMethod(
            "CanEditShipRecords",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return (bool) method!.Invoke(system, new object[] { actor, target })!;
    }

    private static void ForceGridUid(TransformComponent transform, EntityUid gridUid)
    {
        var field = typeof(TransformComponent).GetField("_gridUid", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field!.SetValue(transform, gridUid);
    }
}
