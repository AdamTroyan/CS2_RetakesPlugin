using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Core;

namespace RetakesPlugin.Services.GameFlow
{
    public class InstaDefuse
    {
        private const float ThreatRadius = 650.0f;

        private readonly RetakeState _retakeState;
        private readonly RetakeLogger _logger;
        private readonly HashSet<int> _infernoThreat = new();

        private float _heThreatUntil;
        private float _molotovThreatUntil;
        private float _infernoFallbackThreatUntil;
        private Vector? _heThreatPosition;
        private Vector? _molotovThreatPosition;

        public InstaDefuse(RetakeState retakeState, RetakeLogger logger)
        {
            _retakeState = retakeState;
            _logger = logger;
        }

        public void ResetThreats()
        {
            _heThreatUntil = 0.0f;
            _molotovThreatUntil = 0.0f;
            _infernoFallbackThreatUntil = 0.0f;
            _heThreatPosition = null;
            _molotovThreatPosition = null;
            _infernoThreat.Clear();
        }

        public void RegisterHeThreat(object heEvent)
        {
            _heThreatUntil = (float)Server.EngineTime + 1.5f;
            _heThreatPosition = TryGetEventPosition(heEvent);
        }

        public void RegisterMolotovThreat(object molotovEvent)
        {
            _molotovThreatUntil = (float)Server.EngineTime + 1.5f;
            _molotovThreatPosition = TryGetEventPosition(molotovEvent);
        }

        public void RegisterInfernoThreatStart(object infernoEvent)
        {
            var infernoId = TryGetInfernoEntityId(infernoEvent);
            if (infernoId.HasValue)
            {
                _infernoThreat.Add(infernoId.Value);
                return;
            }

            _infernoFallbackThreatUntil = (float)Server.EngineTime + 7.0f;
        }

        public void RegisterInfernoThreatExpire(object infernoEvent)
        {
            var infernoId = TryGetInfernoEntityId(infernoEvent);
            if (infernoId.HasValue)
            {
                _infernoThreat.Remove(infernoId.Value);
            }

            _infernoFallbackThreatUntil = 0.0f;
        }

        public void AttemptInstantDefuse(CCSPlayerController? defuser)
        {
            if (defuser == null || !defuser.IsValid || defuser.TeamNum != (byte)CsTeam.CounterTerrorist)
            {
                return;
            }

            if (!_retakeState.IsBombPlanted)
            {
                return;
            }

            var plantedBomb = FindPlantedBomb();
            if (plantedBomb == null)
            {
                return;
            }

            if (plantedBomb.CannotBeDefused)
            {
                return;
            }

            if (TeamHasAlivePlayers(CsTeam.Terrorist))
            {
                return;
            }

            if (HasGrenadeThreat())
            {
                Server.PrintToChatAll($" {ChatColors.Red}[Retake] {ChatColors.Default}Instant defuse is blocked by active grenade threat.");
                _logger.Warning("InstantDefuseBlocked", "Instant defuse blocked by nearby grenade threat.", defuser);
                return;
            }

            var bombTimeUntilDetonation = plantedBomb.C4Blow - (float)Server.EngineTime;

            var defuseLength = plantedBomb.DefuseLength;
            if (defuseLength != 5.0f && defuseLength != 10.0f)
            {
                defuseLength = defuser.PawnHasDefuser ? 5.0f : 10.0f;
            }

            var timeLeftAfterDefuse = bombTimeUntilDetonation - defuseLength;
            if (timeLeftAfterDefuse < 0.0f)
            {
                Server.NextFrame(() =>
                {
                    var activeBomb = FindPlantedBomb();
                    if (activeBomb == null)
                    {
                        return;
                    }

                    activeBomb.C4Blow = 1.0f;
                });

                return;
            }

            Server.NextFrame(() =>
            {
                var activeBomb = FindPlantedBomb();
                if (activeBomb == null)
                {
                    return;
                }

                activeBomb.DefuseCountDown = 0.0f;
                _retakeState.IsBombPlanted = false;
                _logger.Info("InstantDefuseSuccess", "Instant defuse completed.", defuser);
            });
        }

        public void HandleBombBeginDefuse(EventBombBegindefuse @event)
        {
            try
            {
                var player = @event.Userid;

                if (player != null && player.IsValid && player.PawnIsAlive)
                {
                    AttemptInstantDefuse(player);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("OnBombBeginDefuseFailed", "Unhandled exception in bomb defuse handler.", ex);
            }
        }

        private static CPlantedC4? FindPlantedBomb()
        {
            return Utilities.FindAllEntitiesByDesignerName<CPlantedC4>("planted_c4").FirstOrDefault(bomb => bomb != null && bomb.IsValid && bomb.BombTicking);
        }

        private static bool TeamHasAlivePlayers(CsTeam team)
        {
            return Utilities.GetPlayers().Any(player => player != null && player.IsValid && player.TeamNum == (byte)team && player.PawnIsAlive);
        }

        private bool HasGrenadeThreat()
        {
            var now = (float)Server.EngineTime;
            var plantedBomb = FindPlantedBomb();
            if (plantedBomb == null || plantedBomb.AbsOrigin == null)
            {
                return _heThreatUntil > now || _molotovThreatUntil > now || _infernoThreat.Count > 0 || _infernoFallbackThreatUntil > now;
            }

            var bombPos = plantedBomb.AbsOrigin;

            var heThreatNearBomb = _heThreatUntil > now && IsWithinThreatRadius(_heThreatPosition, bombPos);
            var molotovThreatNearBomb = _molotovThreatUntil > now && IsWithinThreatRadius(_molotovThreatPosition, bombPos);

            return heThreatNearBomb || molotovThreatNearBomb || _infernoThreat.Count > 0 || _infernoFallbackThreatUntil > now;
        }

        private static bool IsWithinThreatRadius(Vector? threatPosition, Vector bombPosition)
        {
            if (threatPosition == null)
            {
                return true;
            }

            var dx = threatPosition.X - bombPosition.X;
            var dy = threatPosition.Y - bombPosition.Y;
            var dz = threatPosition.Z - bombPosition.Z;
            var distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);

            return distanceSquared <= ThreatRadius * ThreatRadius;
        }

        private static Vector? TryGetEventPosition(object gameEvent)
        {
            if (gameEvent == null)
            {
                return null;
            }

            var eventType = gameEvent.GetType();
            var xProp = eventType.GetProperty("X");
            var yProp = eventType.GetProperty("Y");
            var zProp = eventType.GetProperty("Z");

            if (xProp == null || yProp == null || zProp == null)
            {
                return null;
            }

            if (!TryConvertToFloat(xProp.GetValue(gameEvent), out var x)
                || !TryConvertToFloat(yProp.GetValue(gameEvent), out var y)
                || !TryConvertToFloat(zProp.GetValue(gameEvent), out var z))
            {
                return null;
            }

            return new Vector(x, y, z);
        }

        private static bool TryConvertToFloat(object? value, out float result)
        {
            if (value is float asFloat)
            {
                result = asFloat;
                return true;
            }

            if (value is double asDouble)
            {
                result = (float)asDouble;
                return true;
            }

            if (value is int asInt)
            {
                result = asInt;
                return true;
            }

            return float.TryParse(value?.ToString(), out result);
        }

        private static int? TryGetInfernoEntityId(object infernoEvent)
        {
            var eventType = infernoEvent.GetType();

            var idProperty = eventType.GetProperty("Entityid") ?? eventType.GetProperty("EntityId") ?? eventType.GetProperty("Infernoid") ?? eventType.GetProperty("InfernoId");

            var value = idProperty?.GetValue(infernoEvent);
            if (value == null)
            {
                return null;
            }

            if (value is int intId)
            {
                return intId;
            }

            return int.TryParse(value.ToString(), out var parsedId) ? parsedId : null;
        }
    }
}
