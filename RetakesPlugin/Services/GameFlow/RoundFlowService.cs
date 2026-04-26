using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Core;
using RetakesPlugin.Models;

namespace RetakesPlugin.Services.GameFlow
{
    public class RoundFlowService
    {
        private readonly RetakeState _retakeState;
        private readonly ServerSettingsService _serverSettingsService;
        private readonly TeamService _teamService;
        private readonly PlayerTeleportService _playerTeleportService;
        private readonly BombService _bombService;
        private readonly RetakeLogger _logger;
        private readonly Random _random;

        private bool _playersTeleportedThisRound;

        public RoundFlowService(
            RetakeState retakeState,
            ServerSettingsService serverSettingsService,
            TeamService teamService,
            PlayerTeleportService playerTeleportService,
            BombService bombService,
            RetakeLogger logger,
            Random random)
        {
            _retakeState = retakeState;
            _serverSettingsService = serverSettingsService;
            _teamService = teamService;
            _playerTeleportService = playerTeleportService;
            _bombService = bombService;
            _logger = logger;
            _random = random;
        }

        public void HandleWarmupEnd()
        {
            ResetRoundState();

            Server.NextFrame(() => Server.ExecuteCommand("mp_restartgame 1"));
            _logger.Info("WarmupEnded", "Warmup ended and retake mode was reinitialized.");
        }

        public void HandlePlayerConnectFull(CCSPlayerController? joiningPlayer)
        {
            if (joiningPlayer == null || !joiningPlayer.IsValid)
            {
                return;
            }

            int otherActiveHumanPlayers = Utilities.GetPlayers().Count(p =>
                p.IsValid
                && p.SteamID != 0
                && (p.TeamNum == (byte)CsTeam.Terrorist || p.TeamNum == (byte)CsTeam.CounterTerrorist)
                && !p.IsBot
                && p.SteamID != joiningPlayer.SteamID);

            if (otherActiveHumanPlayers == 0)
            {
                joiningPlayer.ChangeTeam(CsTeam.Terrorist);
                Server.NextFrame(() =>
                {
                    Server.ExecuteCommand("mp_restartgame 1");
                });
                _logger.Info("FirstPlayerRestart", "First active player joined. Restarting game state.");
            }

            Server.NextFrame(() =>
            {
                _teamService.EnforceSimpleQueue();
            });

            _logger.Info("PlayerConnected", "Player joined server and queue enforcement ran.", joiningPlayer);
        }

        public void HandleRoundStart(List<RetakesPlugin.Models.SpawnPoint> spawns)
        {
            ResetRoundState();
            _retakeState.RoundNumber++;
            _serverSettingsService.EnsureApplied();

            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    _teamService.EnforceSimpleQueue();

                    if (!_teamService.TryEnsurePlanter(out var planterId))
                    {
                        _logger.Warning("RoundStartNoPlanter", "RoundStart aborted: no valid player available for planter fallback.");
                        return;
                    }

                    _retakeState.PlanterId = planterId;
                    _retakeState.IsRetakeActive = true;
                    _logger.Info("RoundStart", $"Round started. Selected site {_retakeState.TargetSite}.");

                    _playersTeleportedThisRound = _playerTeleportService.TeleportPlayers(spawns);
                    if (!_playersTeleportedThisRound)
                    {
                        _logger.Warning("RoundStartTeleportFailed", "RoundStart teleport failed. Retrying at FreezeEnd.");
                    }
                });
            });
        }

        public void HandleRoundEnd(int winnerTeam)
        {
            _retakeState.LastWinnerTeam = winnerTeam;

            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    _teamService.ShuffleTeam();
                    _teamService.EnforceSimpleQueue();
                });
            });
        }

        public void HandleGameEnd()
        {
            _retakeState.ResetMatch(Server.MapName);
            _logger.Info("GameEnd", "Game ended and match state was reset.");
        }

        public void HandleRoundFreezeEnd(List<RetakesPlugin.Models.SpawnPoint> spawns)
        {
            if (!_retakeState.IsRetakeActive)
            {
                _retakeState.TargetSite = _random.Next(2) == 0 ? 'A' : 'B';

                if (!_teamService.TryEnsurePlanter(out var planterId))
                {
                    _logger.Warning("FreezeEndNoPlanter", "FreezeEnd aborted: no valid player available for planter fallback.");
                    return;
                }

                _retakeState.PlanterId = planterId;
                _retakeState.IsRetakeActive = true;
                _logger.Warning("FreezeEndFallback", "FreezeEnd fallback activated retake flow.");
            }

            if (!_playersTeleportedThisRound && !_playerTeleportService.TeleportPlayers(spawns))
            {
                _logger.Warning("FreezeEndNoSpawns", "FreezeEnd aborted: teleport phase had no valid spawns.");
                return;
            }

            _playersTeleportedThisRound = true;

            if (TryPlantBomb())
            {
                _retakeState.IsRetakeActive = true;
                _retakeState.IsBombPlanted = true;

                Server.PrintToChatAll($" {ChatColors.Green}[Retake] {ChatColors.Default}The bomb is planted on {ChatColors.Gold}{_retakeState.TargetSite} {ChatColors.Default}site!");
                _logger.Info("BombPlanted", $"Bomb planted on site {_retakeState.TargetSite}.");
            }
            else
            {
                _logger.Warning("BombPlantFailed", "Bomb plant failed even after fallback planter retry.");
            }
        }

        public void KickBotIf2Players()
        {
            int activeHumanPlayers = Utilities.GetPlayers().Count(p =>
                p != null
                && p.IsValid
                && p.SteamID != 0
                && (p.TeamNum == (byte)CsTeam.Terrorist || p.TeamNum == (byte)CsTeam.CounterTerrorist));

            if (activeHumanPlayers >= 2)
            {
                Server.ExecuteCommand("bot_quota 0");
                Server.ExecuteCommand("bot_kick");
            }
        }

        private void ResetRoundState()
        {
            _retakeState.ResetRound(_random);
            _playersTeleportedThisRound = false;
        }

        private bool TryPlantBomb()
        {
            if (_bombService.PlantBomb())
            {
                return true;
            }

            if (!_teamService.TryEnsurePlanter(out var planterId))
            {
                return false;
            }

            _retakeState.PlanterId = planterId;
            return _bombService.PlantBomb();
        }
    }
}