using Content.IntegrationTests.Pair;
using Content.Server.Power.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._NF.Shuttle;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class FtlDriveRangeTests
{
    private TestPair _pair = default!;
    private IEntityManager _entManager = default!;
    private SharedShuttleSystem _shuttle = default!;

    [SetUp]
    public async Task Setup()
    {
        _pair = await PoolManager.GetServerClient();

        var server = _pair.Server;
        _entManager = server.ResolveDependency<IEntityManager>();
        _shuttle = _entManager.System<SharedShuttleSystem>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _pair.CleanReturnAsync();
    }

    [Test]
    public async Task GetFTLRange_UsesHighestPoweredDriveOnSameGridOnly()
    {
        var map = await _pair.CreateTestMap();
        var otherMap = await _pair.CreateTestMap();

        await _pair.Server.WaitAssertion(() =>
        {
            Assert.That(_shuttle.GetFTLRange(map.Grid.Owner), Is.EqualTo(SharedShuttleSystem.FTLRange));

            var localDrive = _entManager.SpawnEntity("MachineFTLDrive", new EntityCoordinates(map.Grid.Owner, 0f, 0f));
            var localDrivePower = _entManager.GetComponent<ApcPowerReceiverComponent>(localDrive);
            localDrivePower.Powered = true;

            var remoteDrive = _entManager.SpawnEntity("MachineFTLDrive50", new EntityCoordinates(otherMap.Grid.Owner, 0f, 0f));
            var remoteDrivePower = _entManager.GetComponent<ApcPowerReceiverComponent>(remoteDrive);
            remoteDrivePower.Powered = true;

            var strongerLocalDrive = _entManager.SpawnEntity("MachineFTLDrive50", new EntityCoordinates(map.Grid.Owner, 0f, 0f));
            var strongerLocalDrivePower = _entManager.GetComponent<ApcPowerReceiverComponent>(strongerLocalDrive);
            strongerLocalDrivePower.Powered = false;

            Assert.Multiple(() =>
            {
                Assert.That(_shuttle.GetFTLRange(map.Grid.Owner), Is.EqualTo(5000f), "Other grids should not affect this shuttle's range.");
                Assert.That(_shuttle.GetFTLRange(otherMap.Grid.Owner), Is.EqualTo(10000f));
            });

            strongerLocalDrivePower.Powered = true;
            Assert.That(_shuttle.GetFTLRange(map.Grid.Owner), Is.EqualTo(10000f), "Multiple drives should use the strongest powered drive instead of stacking.");
        });
    }

    [Test]
    public async Task FTLFree_RespectsPoweredDriveRange()
    {
        var map = await _pair.CreateTestMap();

        await _pair.Server.WaitAssertion(() =>
        {
            var farCoordinates = new EntityCoordinates(map.MapUid, 300f, 0f);

            Assert.That(_shuttle.FTLFree(map.Grid.Owner, farCoordinates, Angle.Zero, null), Is.False,
                "Shuttles without a powered drive should still be limited to the default range.");

            var drive = _entManager.SpawnEntity("MachineFTLDrive", new EntityCoordinates(map.Grid.Owner, 0f, 0f));
            var drivePower = _entManager.GetComponent<ApcPowerReceiverComponent>(drive);
            drivePower.Powered = true;

            Assert.That(_shuttle.FTLFree(map.Grid.Owner, farCoordinates, Angle.Zero, null), Is.True,
                "A powered drive should extend the actual FTL validation range.");

            drivePower.Powered = false;
            Assert.That(_shuttle.FTLFree(map.Grid.Owner, farCoordinates, Angle.Zero, null), Is.False,
                "Unpowered drives should not contribute to FTL range.");
        });
    }
}