using Content.Server._NF.Roles.Systems; // HardLight
using Content.Shared._NF.CCVar; // HardLight
using Content.Shared._NF.Roles.Components; // HardLight
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.GameWindow;
using Content.Shared.Players;
using Content.Shared.Preferences;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking
{
    [UsedImplicitly]
    public sealed partial class GameTicker
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        private readonly Dictionary<NetUserId, System.Threading.CancellationTokenSource> _pendingMindWipes = new();

        // HardLight: Per-player timers that release a tracked job slot after a short grace window
        // following disconnect, so the lobby does not advertise an occupied slot for the full mind-wipe delay.
        private readonly Dictionary<NetUserId, System.Threading.CancellationTokenSource> _pendingJobSlotReleases = new();

        private static readonly TimeSpan MindWipeDelay = TimeSpan.FromMinutes(30);

        private void InitializePlayer()
        {
            _playerManager.PlayerStatusChanged += PlayerStatusChanged;
        }

        private async void PlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            var session = args.Session;

            if (_mind.TryGetMind(session.UserId, out var mindId, out var mind))
            {
                if (args.NewStatus != SessionStatus.Disconnected)
                {
                    _pvsOverride.AddSessionOverride(mindId.Value, session);
                }
            }

            DebugTools.Assert(session.GetMind() == mindId);

            switch (args.NewStatus)
            {
                case SessionStatus.Connected:
                {
                    CancelPendingMindWipe(session.UserId);
                    CancelPendingJobSlotRelease(session.UserId); // HardLight

                    AddPlayerToDb(args.Session.UserId.UserId);

                    // Always make sure the client has player data.
                    if (session.Data.ContentDataUncast == null)
                    {
                        var data = new ContentPlayerData(session.UserId, args.Session.Name);
                        data.Mind = mindId;
                        data.Whitelisted = await _db.GetWhitelistStatusAsync(session.UserId); // Nyanotrasen - Whitelist
                        session.Data.ContentDataUncast = data;
                    }

                    // Make the player actually join the game.
                    // timer time must be > tick length
                    global::Robust.Shared.Timing.Timer.Spawn(0, () => _playerManager.JoinGame(args.Session));

                    var record = await _db.GetPlayerRecordByUserId(args.Session.UserId);
                    var firstConnection = record != null &&
                                          Math.Abs((record.FirstSeenTime - record.LastSeenTime).TotalMinutes) < 1;

                    _chatManager.SendAdminAnnouncement(firstConnection
                        ? Loc.GetString("player-first-join-message", ("name", args.Session.Name))
                        : Loc.GetString("player-join-message", ("name", args.Session.Name)));

                    RaiseNetworkEvent(GetConnectionStatusMsg(), session.Channel);

                    if (firstConnection && _cfg.GetCVar(CCVars.AdminNewPlayerJoinSound))
                        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Effects/newplayerping.ogg"),
                            Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false,
                            audioParams: new AudioParams { Volume = -5f });

                    if (LobbyEnabled && _roundStartCountdownHasNotStartedYetDueToNoPlayers)
                    {
                        _roundStartCountdownHasNotStartedYetDueToNoPlayers = false;
                        _roundStartTime = _gameTiming.CurTime + LobbyDuration;
                    }

                    break;
                }

                case SessionStatus.InGame:
                {
                    CancelPendingMindWipe(session.UserId);
                    CancelPendingJobSlotRelease(session.UserId); // HardLight

                    _userDb.ClientConnected(session);

                    if (mind == null)
                    {
                        if (LobbyEnabled)
                            PlayerJoinLobby(session);
                        else
                            SpawnWaitDb();

                        break;
                    }

                    if (mind.CurrentEntity == null || !Exists(mind.CurrentEntity.Value) || Deleted(mind.CurrentEntity))
                    {
                        DebugTools.Assert(mind.CurrentEntity == null, "a mind's current entity was deleted without updating the mind");

                        // This player is joining the game with an existing mind, but the mind has no entity.
                        // Their entity was probably deleted sometime while they were disconnected, or they were an observer.
                        // Instead of allowing them to spawn in, we will dump and their existing mind in an observer ghost.
                        SpawnObserverWaitDb();
                    }
                    else
                    {
                        if (_playerManager.SetAttachedEntity(session, mind.CurrentEntity))
                        {
                            PlayerJoinGame(session);
                        }
                        else
                        {
                            Log.Error(
                                $"Failed to attach player {session} with mind {ToPrettyString(mindId)} to its current entity {ToPrettyString(mind.CurrentEntity)}");
                            SpawnObserverWaitDb();
                        }
                    }

                    break;
                }

                case SessionStatus.Disconnected:
                {
                    _chatManager.SendAdminAnnouncement(Loc.GetString("player-leave-message", ("name", args.Session.Name)));
                    if (mindId != null)
                    {
                        _pvsOverride.RemoveSessionOverride(mindId.Value, session);
                    }

                    if (mindId != null)
                        ScheduleMindWipe(session.UserId, mindId.Value);

                    // HardLight: Schedule a short-window release of the player's tracked job slot so the
                    // lobby does not show it occupied for the full 30-minute mind-wipe delay.
                    if (mind?.CurrentEntity is { } body)
                        ScheduleJobSlotRelease(session.UserId, body);

                    _userDb.ClientDisconnected(session);
                    break;
                }
            }
            //When the status of a player changes, update the server info text
            UpdateInfoText();

            async void SpawnWaitDb()
            {
                try
                {
                    await _userDb.WaitLoadComplete(session);
                }
                catch (OperationCanceledException)
                {
                    // Bail, user must've disconnected or something.
                    Log.Debug($"Database load cancelled while waiting to spawn {session}");
                    return;
                }

                SpawnPlayer(session, EntityUid.Invalid);
            }

            async void SpawnObserverWaitDb()
            {
                try
                {
                    await _userDb.WaitLoadComplete(session);
                }
                catch (OperationCanceledException)
                {
                    // Bail, user must've disconnected or something.
                    Log.Debug($"Database load cancelled while waiting to spawn {session}");
                    return;
                }

                JoinAsObserver(session);
            }

            async void AddPlayerToDb(Guid id)
            {
                if (RoundId != 0 && _runLevel != GameRunLevel.PreRoundLobby)
                {
                    await _db.AddRoundPlayers(RoundId, id);
                }
            }
        }

        private void ScheduleMindWipe(NetUserId userId, EntityUid mindId)
        {
            CancelPendingMindWipe(userId);

            var cts = new System.Threading.CancellationTokenSource();
            _pendingMindWipes[userId] = cts;

            global::Robust.Shared.Timing.Timer.Spawn(MindWipeDelay, () =>
            {
                if (cts.IsCancellationRequested)
                    return;

                if (!_playerManager.TryGetSessionById(userId, out var session) ||
                    session.State.Status != SessionStatus.Disconnected)
                {
                    CancelPendingMindWipe(userId);
                    return;
                }

                if (_mind.TryGetMind(userId, out var currentMindId, out var currentMind) && currentMindId == mindId)
                {
                    _mind.WipeMind(currentMindId.Value, currentMind);
                }

                CancelPendingMindWipe(userId);
            }, cts.Token);
        }

        private void CancelPendingMindWipe(NetUserId userId)
        {
            if (_pendingMindWipes.TryGetValue(userId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _pendingMindWipes.Remove(userId);
            }
        }

        // HardLight begin: short-window job slot release on disconnect.
        private void ScheduleJobSlotRelease(NetUserId userId, EntityUid body)
        {
            CancelPendingJobSlotRelease(userId);

            var delaySeconds = _cfg.GetCVar(NFCCVars.JobSlotReleaseDelay);
            if (delaySeconds <= 0f)
                return;

            // No need to schedule if the body has no tracked job, or it's already inactive.
            if (!EntityManager.TryGetComponent<JobTrackingComponent>(body, out var jobTracking) || !jobTracking.Active)
                return;

            var cts = new System.Threading.CancellationTokenSource();
            _pendingJobSlotReleases[userId] = cts;

            global::Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(delaySeconds), () =>
            {
                if (cts.IsCancellationRequested)
                    return;

                // Only release if the player is still disconnected.
                if (_playerManager.TryGetSessionById(userId, out var session) &&
                    session.State.Status != SessionStatus.Disconnected)
                {
                    CancelPendingJobSlotRelease(userId);
                    return;
                }

                // Re-resolve the body and component in case the mind moved (cryo, gib, ghost) while disconnected;
                // those paths already invoke OpenJob themselves, so this would be a no-op.
                if (EntityManager.EntityExists(body)
                    && EntityManager.TryGetComponent<JobTrackingComponent>(body, out var currentTracking)
                    && currentTracking.Active)
                {
                    EntityManager.System<JobTrackingSystem>().OpenJob((body, currentTracking), userId);
                }

                CancelPendingJobSlotRelease(userId);
            }, cts.Token);
        }

        private void CancelPendingJobSlotRelease(NetUserId userId)
        {
            if (_pendingJobSlotReleases.TryGetValue(userId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                _pendingJobSlotReleases.Remove(userId);
            }
        }
        // HardLight end

        public HumanoidCharacterProfile GetPlayerProfile(ICommonSession p)
        {
            return (HumanoidCharacterProfile) _prefsManager.GetPreferences(p.UserId).SelectedCharacter;
        }

        public void PlayerJoinGame(ICommonSession session, bool silent = false)
        {
            if (!silent)
                _chatManager.DispatchServerMessage(session, Loc.GetString("game-ticker-player-join-game-message"));

            _playerGameStatuses[session.UserId] = PlayerGameStatus.JoinedGame;
            _db.AddRoundPlayers(RoundId, session.UserId);

            if (_adminManager.HasAdminFlag(session, AdminFlags.Admin))
            {
                if (_allPreviousGameRules.Count > 0)
                {
                    var rulesMessage = GetGameRulesListMessage(true);
                    _chatManager.SendAdminAnnouncementMessage(session, Loc.GetString("starting-rule-selected-preset", ("preset", rulesMessage)));
                }
            }

            RaiseNetworkEvent(new TickerJoinGameEvent(), session.Channel);
        }

        public void ReturnPlayerToLobby(ICommonSession session)
        {
            PlayerJoinLobby(session);
        }

        private void PlayerJoinLobby(ICommonSession session)
        {
            _playerGameStatuses[session.UserId] = LobbyEnabled ? PlayerGameStatus.NotReadyToPlay : PlayerGameStatus.ReadyToPlay;
            _db.AddRoundPlayers(RoundId, session.UserId);

            var client = session.Channel;
            RaiseNetworkEvent(new TickerJoinLobbyEvent(), client);
            RaiseNetworkEvent(GetStatusMsg(session), client);
            RaiseNetworkEvent(GetInfoMsg(), client);
            RaiseLocalEvent(new PlayerJoinedLobbyEvent(session));
        }

        private void ReqWindowAttentionAll()
        {
            RaiseNetworkEvent(new RequestWindowAttentionEvent());
        }
    }

    public sealed class PlayerJoinedLobbyEvent : EntityEventArgs
    {
        public ICommonSession PlayerSession;

        public PlayerJoinedLobbyEvent(ICommonSession playerSession)
        {
            PlayerSession = playerSession;
        }
    }
}
