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
            var players = Utilities.GetPlayers().Where(p => p.IsValid && p.SteamID != 0).ToList();
            int count = players.Count;
            if (count == 0) return;

            int tTargetCount = count == 1 ? 1 : count / 2;

            var winners = players.Where(p => p.TeamNum == _retakeState._lastWinnerTeam).ToList();
            var others = players.Where(p => p.TeamNum != _retakeState._lastWinnerTeam).ToList();

            ShuffleInPlace(winners);
            ShuffleInPlace(others);

            var prioritizedPlayers = new List<CCSPlayerController>(players.Count);
            prioritizedPlayers.AddRange(winners);
            prioritizedPlayers.AddRange(others);

            for (int i = 0; i < prioritizedPlayers.Count; i++)
            {
                var p = prioritizedPlayers[i];
                CsTeam nextTeam = i < tTargetCount ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

                if (p.TeamNum != (byte)nextTeam)
                {
                    p.SwitchTeam(nextTeam);
                }
            }
        }

        public ulong SelectRandomPlanter()
        {
            var terrorists = Utilities.GetPlayers().Where(p => p.IsValid && p.TeamNum == 2 && p.SteamID != 0).ToList();

            if (terrorists.Count > 0)
            {
                var selectedPlanter = terrorists[_random.Next(terrorists.Count)];

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
    }
}
