using CounterStrikeSharp.API;
using System.Collections.Generic;

namespace RetakesPlugin.Core
{
    public class RetakeState
    {
        public bool IsRetakeActive { get; set; }
        public bool IsBombPlanted { get; set; }

        public ulong PlanterId { get; set; }
        public char TargetSite { get; set; }
        public int RoundNumber { get; set; }

        public int LastWinnerTeam { get; set; }
        public string CurrentMapName { get; set; } = Server.MapName;

        public Dictionary<ulong, bool> _isPlayerAFK { get; } = new();

        public void ResetRound(Random random) 
        {
            IsRetakeActive = false;
            IsBombPlanted = false;
            PlanterId = 0;
            TargetSite = random.Next(2) == 0 ? 'A' : 'B';
        }

        public void ResetMatch(string mapName)
        {
            IsRetakeActive = false;
            IsBombPlanted = false;
            PlanterId = 0;
            TargetSite = '\0';
            RoundNumber = 0;
            LastWinnerTeam = 0;
            CurrentMapName = mapName;
            _isPlayerAFK.Clear();
        }
    }
}
