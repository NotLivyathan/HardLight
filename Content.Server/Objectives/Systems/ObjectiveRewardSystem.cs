using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Managers; // HardLight
using Content.Server.GameTicking;
using Content.Server.Preferences.Managers; // HardLight
using Content.Server.Popups;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Database;
using Content.Server.Administration.Logs;
using Content.Server.Objectives.Components;
using Content.Server._NF.Bank;
using Content.Shared.Chat; // HardLight
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Popups;
using Content.Shared.Preferences; // HardLight
using Robust.Shared.Player;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Server system that pays players into their in-character bank account when they complete objectives
/// which are configured with <see cref="ObjectiveRewardComponent"/>.
/// </summary>
public sealed class ObjectiveRewardSystem : EntitySystem
{
    private const float CompletionThreshold = 0.999f; // HardLight

    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!; // HardLight
    [Dependency] private readonly ISharedPlayerManager _players = default!; // HardLight
    [Dependency] private readonly IServerPreferencesManager _prefs = default!; // HardLight

    private float _accum;
    private const float ScanInterval = 2.0f; // seconds

    public override void Initialize()
    {
        base.Initialize();

        // Final sweep at round end to catch anything that completed right at the end.
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndTextAppend);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accum += frameTime;
        if (_accum < ScanInterval)
            return;
        _accum = 0f;

        ScanAndReward();
    }

    private void OnRoundEndTextAppend(RoundEndTextAppendEvent ev)
    {
        // Do a final pass to award anything that just completed.
        ScanAndReward(isRoundEnd: true);
    }

    private void ScanAndReward(bool isRoundEnd = false)
    {
        // HardLight start
        var mindQuery = EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var mindId, out var mind))
        {
            if (mind.Objectives.Count == 0)
                continue;

            foreach (var objective in mind.Objectives)
            {
                if (!TryComp(objective, out ObjectiveRewardComponent? reward))
                    continue; // No reward configured

                if (reward.Rewarded)
                    continue;

                // For objectives marked as round-end only, skip early periodic payment
                if (reward.OnlyAtRoundEnd && !isRoundEnd)
                    continue;

                var progress = _objectives.GetProgress(objective, (mindId, mind));
                if (progress == null)
                    continue;

                if (progress.Value < CompletionThreshold)
                    continue;

                // Mark completed zero/negative payouts as processed to avoid scanning forever.
                if (reward.Amount <= 0)
                {
                    reward.Rewarded = true;
                    continue;
                }

                // Completed! Attempt payout once.
                if (TryDepositReward(mind, reward.Amount, out var payoutTarget))
                {
                    reward.Rewarded = true;

                    // Optional feedback popup when we have a valid in-world target.
                    if (reward.NotifyPlayer && payoutTarget is { } target)
                    {
                        var msg = reward.PopupMessage ?? "Objective complete.";
                        _popup.PopupEntity(msg, target, Filter.Entities(target), false, PopupType.Small);
                    }

                    var title = Name(objective);
                    TrySendPayoutChat(mind, reward.Amount, title, isRoundEnd);
                    var payoutTargetText = payoutTarget is { } targetUid
                        ? ToPrettyString(targetUid)
                        : mind.UserId?.ToString() ?? "unknown-user";
                    _adminLog.Add(LogType.Action, LogImpact.Low,
                        $"ObjectiveReward: Paid {reward.Amount} to {payoutTargetText} for completing objective '{title}' (ent {objective}).");
                }
            }
        }
        // HardLight end
    }

    // HardLight: Sends a payout confirmation message to the player's chat session.
    private void TrySendPayoutChat(MindComponent mind, int amount, string objectiveTitle, bool isRoundEnd)
    {
        if (mind.UserId is not { } userId)
            return;

        if (!_players.TryGetSessionById(userId, out var session))
            return;

        var amountText = Content.Shared._NF.Bank.BankSystemExtensions.ToSpesoString(amount);
        var highlightedAmount = $"[color=white]+{amountText}[/color]";
        var message = isRoundEnd
            ? $"Objective complete: {objectiveTitle} ({highlightedAmount})."
            : $"Objective payout: {objectiveTitle} ({highlightedAmount}).";

        var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, EntityUid.Invalid, false, session.Channel);
    }

    // HardLight: Deposits reward money to an in-world bank account, with a profile-based fallback.
    private bool TryDepositReward(MindComponent mind, int amount, out EntityUid? payoutTarget)
    {
        payoutTarget = null;

        // Preferred path: deposit via the active/original in-world entity bank account.
        if (TryGetPayoutTarget(mind, out var target) && _bank.TryBankDeposit(target.Value, amount))
        {
            payoutTarget = target.Value;
            return true;
        }

        // Fallback path for dead/ghosted/bodyless players: deposit directly to selected profile.
        if (mind.UserId is not { } userId)
            return false;

        if (!_players.TryGetSessionById(userId, out var session))
            return false;

        if (!_prefs.TryGetCachedPreferences(userId, out var prefs))
            return false;

        if (prefs.SelectedCharacter is not HumanoidCharacterProfile profile)
            return false;

        return _bank.TryBankDeposit(session, prefs, profile, amount, out _);
    }

    private bool TryGetPayoutTarget(MindComponent mind, [NotNullWhen(true)] out EntityUid? target)
    {
        // Prefer the currently owned entity (most reliable for an active player and has the BankAccountComponent).
        if (mind.OwnedEntity is { } owned && EntityManager.EntityExists(owned) && HasComp<BankAccountComponent>(owned))
        {
            target = owned;
            return true;
        }

        // Fallback: the original owned entity if it still exists and has a bank account.
        var original = GetEntity(mind.OriginalOwnedEntity);
        if (original is { } orig && EntityManager.EntityExists(orig) && HasComp<BankAccountComponent>(orig))
        {
            target = orig;
            return true;
        }

        target = null;
        return false;
    }
}
