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
        private readonly RetakeState _retakeState;
        private readonly Random _random;

        public TeamService(RetakeState retakeState, Random random)
        {
            _retakeState = retakeState;
            _random = random;
        }

        public void ShuffleTeam()
        {
            var snapshot = Utilities.GetPlayers().Where(IsEligibleForRetakeTeam).Select(p => new { p.SteamID, p.TeamNum }).ToList();

            int count = snapshot.Count;
            if (count > 0)
            {
                int tTargetCount = count == 1 ? 1 : count / 2;

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

            var spectators = Utilities.GetPlayers().Where(p => p.IsValid && p.SteamID != 0 && p.TeamNum == (byte)CsTeam.Spectator).ToList();

            int ctCount = Utilities.GetPlayers().Count(p => p.IsValid && p.TeamNum == (byte)CsTeam.CounterTerrorist);

            foreach (var spectator in spectators)
            {
                if (ctCount >= 5)
                {
                    break;
                }

                spectator.SwitchTeam(CsTeam.CounterTerrorist);
                ctCount++;
            }
        }

        public ulong SelectRandomPlanter()
        {
            var terrorists = Utilities.GetPlayers().Where(p => IsEligibleForRetakeTeam(p) && p.TeamNum == (byte)CsTeam.Terrorist).ToList();

            if (terrorists.Count > 0)
            {
                var aliveTerrorists = terrorists.Where(p => p.PawnIsAlive).ToList();
                var planterPool = aliveTerrorists.Count > 0 ? aliveTerrorists : terrorists;
                var selectedPlanter = planterPool[_random.Next(planterPool.Count)];

                Console.WriteLine($"[Retake] Planter selected: {selectedPlanter.PlayerName} (ID: {selectedPlanter.SteamID})");

                return selectedPlanter.SteamID;
            }

            Console.WriteLine("[Retake Warning] No valid terrorist found for planting!");
            return 0;
        }

        private void ShuffleInPlace<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
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
