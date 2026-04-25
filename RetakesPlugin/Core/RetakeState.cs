using CounterStrikeSharp.API;

namespace RetakesPlugin.Core
{
    public class RetakeState
    {
        public bool IsRetakeActive { get; set; }
        public bool IsBombPlanted { get; set; }

        public ulong PlanterId { get; set; }
        public char TargetSite { get; set; }

        public int LastWinnerTeam { get; set; }
        public string CurrentMapName { get; set; } = Server.MapName;

        public bool ServerSettingsApplied { get; set; }

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
            LastWinnerTeam = 0;
            CurrentMapName = mapName;
        }
    }
}
