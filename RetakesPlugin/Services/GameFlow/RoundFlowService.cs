using System.Runtime.CompilerServices;
using CounterStrikeSharp.API;
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
        private readonly Random _random;

        private bool _playersTeleportedThisRound;

        public RoundFlowService(
            RetakeState retakeState,
            ServerSettingsService serverSettingsService,
            TeamService teamService,
            PlayerTeleportService playerTeleportService,
            BombService bombService,
            Random random)
        {
            _retakeState = retakeState;
            _serverSettingsService = serverSettingsService;
            _teamService = teamService;
            _playerTeleportService = playerTeleportService;
            _bombService = bombService;
            _random = random;
        }

        public void HandleWarmupEnd()
        {
            ResetRoundState();

            Server.NextFrame(() => Server.ExecuteCommand("mp_restartgame 1"));
            Console.WriteLine("[Retake] Warmup ended, retake mode reinitialized.");
        }

        public void HandleRoundStart(List<SpawnPoint> spawns)
        {
            ResetRoundState();
            _serverSettingsService.EnsureApplied();

            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    _retakeState.PlanterId = _teamService.SelectRandomPlanter();
                    _retakeState.IsRetakeActive = true;
                    Console.WriteLine($"[Retake] Selected site for this round: {_retakeState.TargetSite}");

                    _playersTeleportedThisRound = _playerTeleportService.TeleportPlayers(spawns);
                    if (!_playersTeleportedThisRound)
                    {
                        Console.WriteLine("[Retake Warning] RoundStart teleport failed, will retry at FreezeEnd.");
                    }
                });
            });
        }

        public void HandleRoundEnd(int winnerTeam)
        {
            _retakeState.LastWinnerTeam = winnerTeam;

            _teamService.ShuffleTeam();
        }

        public void HandleGameEnd()
        {
            _retakeState.ResetMatch(Server.MapName);
        }

        public void HandleRoundFreezeEnd(List<SpawnPoint> spawns)
        {
            if (!_retakeState.IsRetakeActive)
            {
                _retakeState.TargetSite = _random.Next(2) == 0 ? 'A' : 'B';
                _retakeState.PlanterId = _teamService.SelectRandomPlanter();
                _retakeState.IsRetakeActive = true;
                Console.WriteLine("[Retake] FreezeEnd fallback activated retake flow.");
            }

            if (!_playersTeleportedThisRound && !_playerTeleportService.TeleportPlayers(spawns))
            {
                Console.WriteLine("[Retake Warning] FreezeEnd aborted: teleport phase had no valid spawns.");
                return;
            }

            _playersTeleportedThisRound = true;

            if (_bombService.PlantBomb())
            {
                _retakeState.IsRetakeActive = true;
                _retakeState.IsBombPlanted = true;

                Server.PrintToChatAll($" {ChatColors.Green}[Retake] {ChatColors.Default}The bomb is planted on {ChatColors.Gold}{_retakeState.TargetSite} {ChatColors.Default}site!");
                Console.WriteLine($"[Retake Log] New round started on Site {_retakeState.TargetSite}");
            }
        }

        public void KickBotIf2Players(){
            int activeHumanPlayers = Utilities.GetPlayers().Count(p =>
                p != null
                && p.IsValid
                && p.SteamID != 0
                && (p.TeamNum == (byte)CsTeam.Terrorist || p.TeamNum == (byte)CsTeam.CounterTerrorist));

            if(activeHumanPlayers >= 2){
                Server.ExecuteCommand("bot_quota 0");
                Server.ExecuteCommand("bot_kick");
            }
        }

        private void ResetRoundState()
        {
            _retakeState.ResetRound(_random);
            _playersTeleportedThisRound = false;
        }
    }
}