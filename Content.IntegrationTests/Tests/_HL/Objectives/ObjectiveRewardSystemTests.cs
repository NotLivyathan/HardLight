using System.Linq;
using Content.Server.Mind;
using Content.Server.Objectives.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._HL.Objectives;

[TestFixture]
public sealed class ObjectiveRewardSystemTests
{
    [Test]
    public async Task RewardsOnlyObjectiveOwnerOnce()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = false,
        });

        await pair.Server.AddDummySessions(1);
        await pair.RunTicksSync(10);

        var entMan = pair.Server.EntMan;
        var mindSys = pair.Server.System<MindSystem>();
        var playerMan = pair.Server.PlayerMan;

        EntityUid ownerEntity = default;
        EntityUid otherEntity = default;
        EntityUid ownerMindId = default;
        MindComponent ownerMind = default!;
        EntityUid objective = default;
        const int rewardAmount = 12345;

        await pair.Server.WaitPost(() =>
        {
            var sessions = playerMan.Sessions.ToArray();
            Assert.That(sessions.Length, Is.GreaterThanOrEqualTo(2), "Expected at least two sessions for objective ownership isolation test.");

            var ownerSession = sessions[0];
            var otherSession = sessions[1];

            ownerEntity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            otherEntity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            entMan.EnsureComponent<BankAccountComponent>(ownerEntity);
            entMan.EnsureComponent<BankAccountComponent>(otherEntity);

            ownerMindId = mindSys.CreateMind(ownerSession.UserId);
            var otherMindId = mindSys.CreateMind(otherSession.UserId);

            ownerMind = entMan.GetComponent<MindComponent>(ownerMindId);
            var otherMind = entMan.GetComponent<MindComponent>(otherMindId);

            mindSys.TransferTo(ownerMindId, ownerEntity, mind: ownerMind);
            mindSys.TransferTo(otherMindId, otherEntity, mind: otherMind);

            objective = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<ObjectiveComponent>(objective);
            entMan.EnsureComponent<FreeObjectiveComponent>(objective);

            var reward = entMan.EnsureComponent<ObjectiveRewardComponent>(objective);
            reward.Amount = rewardAmount;
            reward.NotifyPlayer = false;
            reward.OnlyAtRoundEnd = false;

            mindSys.AddObjective(ownerMindId, ownerMind, objective);
        });

        int ownerInitialBalance = 0;
        int otherInitialBalance = 0;

        await pair.Server.WaitPost(() =>
        {
            ownerInitialBalance = entMan.GetComponent<BankAccountComponent>(ownerEntity).Balance;
            otherInitialBalance = entMan.GetComponent<BankAccountComponent>(otherEntity).Balance;
        });

        await pair.RunTicksSync(180);

        int ownerBalanceAfterFirstPass = 0;
        int otherBalanceAfterFirstPass = 0;

        await pair.Server.WaitAssertion(() =>
        {
            var ownerBank = entMan.GetComponent<BankAccountComponent>(ownerEntity);
            var otherBank = entMan.GetComponent<BankAccountComponent>(otherEntity);
            var reward = entMan.GetComponent<ObjectiveRewardComponent>(objective);

            Assert.That(reward.Rewarded, Is.True, "Objective reward should be marked rewarded after first payout.");
            Assert.That(ownerBank.Balance - ownerInitialBalance, Is.EqualTo(rewardAmount), "Objective owner should receive exactly one payout.");
            Assert.That(otherBank.Balance - otherInitialBalance, Is.EqualTo(0), "Non-owner should not receive payout for someone else's objective.");

            ownerBalanceAfterFirstPass = ownerBank.Balance;
            otherBalanceAfterFirstPass = otherBank.Balance;
        });

        await pair.RunTicksSync(180);

        await pair.Server.WaitAssertion(() =>
        {
            var ownerBank = entMan.GetComponent<BankAccountComponent>(ownerEntity);
            var otherBank = entMan.GetComponent<BankAccountComponent>(otherEntity);

            Assert.That(ownerBank.Balance, Is.EqualTo(ownerBalanceAfterFirstPass), "Objective payout should not repeat on subsequent scans.");
            Assert.That(otherBank.Balance, Is.EqualTo(otherBalanceAfterFirstPass), "Non-owner balance should remain unchanged on subsequent scans.");
        });

        await pair.CleanReturnAsync();
    }
}
