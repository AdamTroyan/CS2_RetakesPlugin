using CounterStrikeSharp.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RetakesPlugin.Core
{
    public class RetakeState
    {
        public bool _isRetakeActive = false;
        public bool _isBombPlanted = false;

        public ulong _planterId = 0;
        public char _targetSite = '\0';

        public int _lastWinnerTeam = 0;
        public string _currentMapName = Server.MapName;
    }
}
