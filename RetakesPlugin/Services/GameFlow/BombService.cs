using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Core;

namespace RetakesPlugin.Services.GameFlow
{
    public class BombService
    {
        private readonly RetakeState _retakeState;
        public BombService(RetakeState retakeState)
        {
            _retakeState = retakeState;
        }
        public bool PlantBomb()
        {
            if (_retakeState.IsBombPlanted) return false;

            var planter = Utilities.GetPlayerFromSteamId(_retakeState.PlanterId);
            if (planter == null || planter.PlayerPawn.Value == null) return false;

            var pos = planter.PlayerPawn.Value.AbsOrigin!;
            var ang = planter.PlayerPawn.Value.AbsRotation!;

            var plantedC4 = Utilities.CreateEntityByName<CPlantedC4>("planted_c4");
            if (plantedC4 == null || !plantedC4.IsValid) return false;

            plantedC4.Teleport(pos, ang, new Vector(0, 0, 0));
            plantedC4.BombSite = _retakeState.TargetSite == 'B' || _retakeState.TargetSite == 'b' ? 1 : 0;
            plantedC4.DispatchSpawn();

            plantedC4.C4Blow = (float)Server.EngineTime + 40.0f;
            plantedC4.BombTicking = true;

            var bombEvent = new EventBombPlanted(false)
            {
                Userid = planter,
                Site = plantedC4.BombSite
            };
            bombEvent.FireEvent(false);

            _retakeState.IsBombPlanted = true;
            return true;
        }
    }
}
