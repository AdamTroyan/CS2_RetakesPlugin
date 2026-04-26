using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Core;
using System.Collections.Generic;
using System.Linq;

namespace RetakesPlugin.Services.GameFlow
{
    public class TeamService
    {
        private const int MaxActivePlayers = 10;
        private const double TerroristRatio = 0.45;

        private readonly RetakeState _retakeState;
        private readonly Random _random;
        private readonly RetakeLogger _logger;
        private readonly Queue<ulong> _waitingQueue = new();
        private readonly HashSet<ulong> _queuedPlayers = new();

        public TeamService(RetakeState retakeState, Random random, RetakeLogger logger)
        {
            _retakeState = retakeState;
            _random = random;
            _logger = logger;
        }

        public void ShuffleTeam()
        {
            var snapshot = Utilities.GetPlayers().Where(IsEligibleForRetakeTeam).Select(p => new { p.SteamID, p.TeamNum }).ToList();

            int count = snapshot.Count;
            if (count > 0)
            {
                int tTargetCount = GetTargetTerroristCount(count);

                var winners = snapshot.Where(p => p.TeamNum == _retakeState.LastWinnerTeam).Select(p => p.SteamID).ToList();
                var others = snapshot.Where(p => p.TeamNum != _retakeState.LastWinnerTeam).Select(p => p.SteamID).ToList();

                ShuffleInPlace(winners);
                ShuffleInPlace(others);

                var prioritizedPlayers = new List<ulong>(snapshot.Count);
                prioritizedPlayers.AddRange(winners);
                prioritizedPlayers.AddRange(others);

                var currentPlayersById = Utilities.GetPlayers().Where(IsEligibleForRetakeTeam).ToDictionary(p => p.SteamID, p => p);

                for (int i = 0; i < prioritizedPlayers.Count; i++)
                {
                    if (!currentPlayersById.TryGetValue(prioritizedPlayers[i], out var p) || !p.IsValid)
                    {
                        continue;
                    }

                    CsTeam nextTeam = i < tTargetCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

                    if (p.TeamNum != (byte)nextTeam)
                    {
                        p.SwitchTeam(nextTeam);
                    }
                }
            }

            EnforceSimpleQueue();
        }

        public ulong SelectRandomPlanter()
        {
            var terrorists = Utilities.GetPlayers().Where(p => IsEligibleForRetakeTeam(p) && p.TeamNum == (byte)CsTeam.Terrorist).ToList();

            if (terrorists.Count > 0)
            {
                var aliveTerrorists = terrorists.Where(p => p.PawnIsAlive).ToList();
                var planterPool = aliveTerrorists.Count > 0 ? aliveTerrorists : terrorists;
                var selectedPlanter = planterPool[_random.Next(planterPool.Count)];

                _logger.Debug("PlanterSelected", "Planter selected from terrorist pool.", selectedPlanter);

                return selectedPlanter.SteamID;
            }

            _logger.Warning("PlanterMissing", "No valid terrorist found for planting.");
            return 0;
        }

        public bool TryEnsurePlanter(out ulong planterId)
        {
            planterId = SelectRandomPlanter();
            if (planterId != 0)
            {
                return true;
            }

            var ctCandidates = Utilities.GetPlayers()
                .Where(p => p.IsValid && p.SteamID != 0 && p.TeamNum == (byte)CsTeam.CounterTerrorist)
                .ToList();

            if (ctCandidates.Count == 0)
            {
                return false;
            }

            var fallbackPlanter = ctCandidates[_random.Next(ctCandidates.Count)];
            fallbackPlanter.SwitchTeam(CsTeam.Terrorist);
            planterId = fallbackPlanter.SteamID;

            _logger.Warning("FallbackPlanter", "Fallback planter was forced from CT to T.", fallbackPlanter);
            return true;
        }

        public void EnforceSimpleQueue()
        {
            var activePlayers = Utilities.GetPlayers()
                .Where(p => p.IsValid && p.SteamID != 0 && (p.TeamNum == (byte)CsTeam.Terrorist || p.TeamNum == (byte)CsTeam.CounterTerrorist))
                .ToList();

            if (activePlayers.Count > MaxActivePlayers)
            {
                var overflowPlayers = activePlayers.Skip(MaxActivePlayers).ToList();
                foreach (var overflow in overflowPlayers)
                {
                    overflow.SwitchTeam(CsTeam.Spectator);
                    EnqueuePlayer(overflow.SteamID);
                    overflow.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Server is full. You were moved to spectator queue.");
                    _logger.Info("QueueOverflow", "Player moved to spectator queue due to max active player cap.", overflow);
                }
            }

            PromoteQueuedPlayers();
            CleanupQueue();
        }

        private void ShuffleInPlace<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void PromoteQueuedPlayers()
        {
            while (_waitingQueue.Count > 0)
            {
                int activeCount = Utilities.GetPlayers().Count(p => p.IsValid && p.SteamID != 0 && (p.TeamNum == (byte)CsTeam.Terrorist || p.TeamNum == (byte)CsTeam.CounterTerrorist));
                if (activeCount >= MaxActivePlayers)
                {
                    return;
                }

                var steamId = _waitingQueue.Dequeue();
                _queuedPlayers.Remove(steamId);

                var player = Utilities.GetPlayerFromSteamId(steamId);
                if (player == null || !player.IsValid || player.TeamNum != (byte)CsTeam.Spectator)
                {
                    continue;
                }

                int tCount = Utilities.GetPlayers().Count(p => p.IsValid && p.SteamID != 0 && p.TeamNum == (byte)CsTeam.Terrorist);
                int ctCount = Utilities.GetPlayers().Count(p => p.IsValid && p.SteamID != 0 && p.TeamNum == (byte)CsTeam.CounterTerrorist);
                var preferredTeam = tCount <= ctCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

                player.SwitchTeam(preferredTeam);
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}You joined the active retake players.");
                _logger.Info("QueuePromoted", $"Queued player promoted to {preferredTeam}.", player);
            }
        }

        private static int GetTargetTerroristCount(int activeCount)
        {
            if (activeCount <= 1)
            {
                return activeCount;
            }

            var desired = (int)Math.Round(activeCount * TerroristRatio, MidpointRounding.AwayFromZero);
            return Math.Clamp(desired, 1, activeCount - 1);
        }

        private void EnqueuePlayer(ulong steamId)
        {
            if (_queuedPlayers.Contains(steamId))
            {
                return;
            }

            _waitingQueue.Enqueue(steamId);
            _queuedPlayers.Add(steamId);
        }

        private void CleanupQueue()
        {
            if (_waitingQueue.Count == 0)
            {
                return;
            }

            var refreshedQueue = new Queue<ulong>();
            var refreshedSet = new HashSet<ulong>();

            while (_waitingQueue.Count > 0)
            {
                var steamId = _waitingQueue.Dequeue();
                var player = Utilities.GetPlayerFromSteamId(steamId);
                if (player == null || !player.IsValid || player.TeamNum != (byte)CsTeam.Spectator)
                {
                    continue;
                }

                if (refreshedSet.Add(steamId))
                {
                    refreshedQueue.Enqueue(steamId);
                }
            }

            _waitingQueue.Clear();
            while (refreshedQueue.Count > 0)
            {
                _waitingQueue.Enqueue(refreshedQueue.Dequeue());
            }

            _queuedPlayers.Clear();
            foreach (var steamId in _waitingQueue)
            {
                _queuedPlayers.Add(steamId);
            }
        }

        private static bool IsEligibleForRetakeTeam(CCSPlayerController player)
        {
            return player.IsValid
                && player.SteamID != 0
                && player.TeamNum != (byte)CsTeam.Spectator;
        }
    }
}
