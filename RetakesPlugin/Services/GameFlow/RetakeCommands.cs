using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Core;
using RetakesPlugin.Persistence;
using SpawnPointModel = RetakesPlugin.Models.SpawnPoint;

namespace RetakesPlugin.Services.GameFlow
{
    public class RetakeCommands
    {
        private readonly RetakeState _retakeState;
        private readonly SpawnRepository _spawnRepository;
        private readonly RetakeLogger _logger;
        private readonly CommandRateLimitService _commandRateLimitService;

        public RetakeCommands(
            RetakeState retakeState,
            SpawnRepository spawnRepository,
            RetakeLogger logger,
            CommandRateLimitService commandRateLimitService)
        {
            _retakeState = retakeState;
            _spawnRepository = spawnRepository;
            _logger = logger;
            _commandRateLimitService = commandRateLimitService;
        }

        public void HandleJoinTeam(CCSPlayerController? player, CommandInfo info)
        {
            _ = info;

            if (player == null || !player.IsValid)
            {
                return;
            }

            if (!_commandRateLimitService.Allow(player, "jointeam", 3, 10, 15, out var retryAfter))
            {
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Please wait {retryAfter}s before using jointeam again.");
                return;
            }

            player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Manual team switching is disabled during retakes.");
            _logger.Security("JoinTeamBlocked", "Manual jointeam attempt blocked.", player);
        }

        public void HandleRetakeDebug(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid)
            {
                return;
            }

            if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
            {
                _logger.Security("DebugToggleDenied", "Player attempted debug toggle without permission.", player);
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Only admins can toggle debug mode.");
                return;
            }

            if (!_commandRateLimitService.Allow(player, "css_retake_debug", 3, 10, 20, out var retryAfter))
            {
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Please wait {retryAfter}s before using debug toggle again.");
                return;
            }

            var requestedValue = info.ArgByIndex(1).Trim().ToLowerInvariant();
            bool enable;
            if (string.IsNullOrWhiteSpace(requestedValue))
            {
                enable = !_logger.IsDebugEnabled;
            }
            else
            {
                enable = requestedValue == "1" || requestedValue == "on" || requestedValue == "true";
            }

            _logger.SetDebugEnabled(enable, player);
            player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Debug mode is now {(enable ? ChatColors.Gold + "ON" : ChatColors.Gold + "OFF")}{ChatColors.Default}.");
        }

        public void HandleLogStats(CCSPlayerController? player, CommandInfo info)
        {
            _ = info;

            if (player == null || !player.IsValid)
            {
                return;
            }

            if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
            {
                _logger.Security("LogStatsDenied", "Player attempted log stats without permission.", player);
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Only admins can view log counters.");
                return;
            }

            var snapshot = _logger.GetCountersSnapshot();
            _logger.Info("LogStatsRequested", snapshot, player);
            player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}{snapshot}");
        }

        public void HandleSaveSpawn(string moduleDirectory, CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
            {
                return;
            }

            if (!AdminManager.PlayerHasPermissions(player, "@css/root"))
            {
                _logger.Security("SpawnSaveDenied", "Player attempted to save spawn without permission.", player);
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Only admins can save spawns.");
                return;
            }

            if (!_commandRateLimitService.Allow(player, "css_save", 5, 20, 30, out var retryAfter))
            {
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Too many save attempts. Try again in {retryAfter}s.");
                return;
            }

            string placeName = info.ArgByIndex(1);
            var pos = player.PlayerPawn.Value.AbsOrigin!;
            var ang = player.PlayerPawn.Value.AbsRotation!;

            var newPoint = new SpawnPointModel
            {
                Place = placeName,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                Yaw = ang.Y
            };

            _spawnRepository.SaveSpawn(moduleDirectory, _retakeState.CurrentMapName, newPoint);
            _logger.Info("SpawnSaved", $"Spawn point '{placeName}' saved.", player);
            player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Point {ChatColors.Gold}{placeName} {ChatColors.Default}saved!");
        }
    }
}
