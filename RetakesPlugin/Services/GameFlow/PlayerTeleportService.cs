using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Core;
using RetakesPlugin.Models;
using RetakesPlugin.Services.Spawns;
using System.Collections.Generic;
using System.Linq;
using SpawnPointModel = RetakesPlugin.Models.SpawnPoint;

namespace RetakesPlugin.Services.GameFlow
{
    public class PlayerTeleportService
    {
        private const string TeamT = "t";
        private const string TeamCt = "ct";
        private const string SpawnTypePlant = "plant";
        private const string SpawnTypePlayer = "player";

        private readonly RetakeState _retakeState;
        private readonly SpawnSelectionService _spawnSelectionService;
        private readonly LoadoutService _loadoutService;
        private readonly RetakeLogger _logger;
        private readonly Random _random;

        public PlayerTeleportService(RetakeState retakeState, SpawnSelectionService spawnSelectionService, LoadoutService loadoutService, RetakeLogger logger, Random random)
        {
            _retakeState = retakeState;
            _spawnSelectionService = spawnSelectionService;
            _loadoutService = loadoutService;
            _logger = logger;
            _random = random;
        }

        public bool TeleportPlayers(List<SpawnPointModel> allSpawns)
        {
            if (allSpawns.Count == 0)
            {
                _logger.Warning("TeleportNoSpawns", "No spawn points loaded. Teleport phase skipped.");
                return false;
            }

            if (_retakeState.TargetSite != 'A' && _retakeState.TargetSite != 'B')
            {
                _retakeState.TargetSite = _random.Next(2) == 0 ? 'A' : 'B';
            }

            char site = char.ToLowerInvariant(_retakeState.TargetSite);

            var tPlantPoints = _spawnSelectionService.GetSpawnPoints(allSpawns, TeamT, site, SpawnTypePlant);
            var tPlayerPoints = _spawnSelectionService.GetSpawnPoints(allSpawns, TeamT, site, SpawnTypePlayer);
            var ctPlayerPoints = _spawnSelectionService.GetSpawnPoints(allSpawns, TeamCt, site, SpawnTypePlayer);
            var tFallbackPoints = _spawnSelectionService.GetSpawnPoints(allSpawns, TeamT, site, SpawnTypePlayer);
            var ctFallbackPoints = _spawnSelectionService.GetSpawnPoints(allSpawns, TeamCt, site, SpawnTypePlayer);

            var players = Utilities.GetPlayers().Where(p => p.IsValid && p.PawnIsAlive).ToList();
            bool teleportedAnyone = false;

            foreach (var p in players)
            {
                var pawn = p.PlayerPawn.Value;
                if (pawn == null) continue;

                var selectedPoint = PickSpawnPointForPlayer(p, tPlantPoints, tPlayerPoints, ctPlayerPoints, tFallbackPoints, ctFallbackPoints);

                if (selectedPoint != null)
                {
                    pawn.Teleport(new Vector(selectedPoint.X, selectedPoint.Y, selectedPoint.Z), new QAngle(0, selectedPoint.Yaw, 0), new Vector(0, 0, 0));
                    _loadoutService.RemovePlayerWeapons(pawn);
                    _loadoutService.GiveLoadout(p);
                    teleportedAnyone = true;
                }
            }

            if (!teleportedAnyone)
            {
                _logger.Warning("TeleportNoValidSpawn", "No valid spawn was found for alive players.");
            }
            else
            {
                _logger.Debug("TeleportCompleted", "Teleport/loadout pass completed for alive players.");
            }

            return teleportedAnyone;
        }

        private SpawnPointModel? PickSpawnPointForPlayer(
            CCSPlayerController player,
            List<SpawnPointModel> tPlantPoints,
            List<SpawnPointModel> tPlayerPoints,
            List<SpawnPointModel> ctPlayerPoints,
            List<SpawnPointModel> tFallbackPoints,
            List<SpawnPointModel> ctFallbackPoints)
        {
            if (player.TeamNum == 2)
            {
                if (player.SteamID == _retakeState.PlanterId && tPlantPoints.Count > 0)
                {
                    return _spawnSelectionService.PopSpawn(tPlantPoints);
                }

                if (tPlayerPoints.Count > 0)
                {
                    return _spawnSelectionService.PopSpawn(tPlayerPoints);
                }

                if (tFallbackPoints.Count > 0)
                {
                    return tFallbackPoints[_random.Next(tFallbackPoints.Count)];
                }
            }
            else if (player.TeamNum == 3 && ctPlayerPoints.Count > 0)
            {
                return _spawnSelectionService.PopSpawn(ctPlayerPoints);
            }
            else if (player.TeamNum == 3 && ctFallbackPoints.Count > 0)
            {
                return ctFallbackPoints[_random.Next(ctFallbackPoints.Count)];
            }

            return null;
        }
    }
}
