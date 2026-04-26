using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesPlugin.Core;

namespace RetakesPlugin.Services.GameFlow
{
    public class AfkService
    {
        private readonly RetakeState _retakeState;
        private readonly RetakeLogger _logger;

        private readonly Dictionary<ulong, Vector> _lastKnownPosition = new();
        private readonly Dictionary<ulong, float> _lastKnownYaw = new();
        private readonly Dictionary<ulong, int> _idleSeconds = new();
        private readonly Dictionary<ulong, float> _afkWarningTime = new();

        private const int AfkFlagAfterSeconds = 10;
        private const int KickAfterWarningSeconds = 10;
        private const float MovementDistanceThreshold = 8.0f;
        private const float ViewAngleThreshold = 10.0f;

        public AfkService(RetakeState retakeState, RetakeLogger logger)
        {
            _retakeState = retakeState;
            _logger = logger;
        }

        public void CheckAfkPlayers()
        {
            var now = (float)Server.EngineTime;
            var activePlayerIds = new HashSet<ulong>();

            foreach (var player in Utilities.GetPlayers())
            {
                if (player == null || !player.IsValid || player.SteamID == 0)
                {
                    continue;
                }

                if (player.TeamNum != (byte)CsTeam.Terrorist && player.TeamNum != (byte)CsTeam.CounterTerrorist)
                {
                    ResetAfkTracking(player.SteamID);
                    continue;
                }

                if (player.ControllingBot)
                {
                    // Bot takeover can desync controller pawn movement checks and produce false AFK flags.
                    ResetAfkTracking(player.SteamID);
                    continue;
                }

                if (!player.PawnIsAlive || player.PlayerPawn.Value == null || player.PlayerPawn.Value.AbsOrigin == null)
                {
                    ResetAfkTracking(player.SteamID);
                    continue;
                }

                activePlayerIds.Add(player.SteamID);

                var currentPosition = player.PlayerPawn.Value.AbsOrigin!;
                var currentYaw = player.PlayerPawn.Value.AbsRotation?.Y ?? 0.0f;

                if (!_lastKnownPosition.TryGetValue(player.SteamID, out var lastPosition))
                {
                    _lastKnownPosition[player.SteamID] = new Vector(currentPosition.X, currentPosition.Y, currentPosition.Z);
                    _lastKnownYaw[player.SteamID] = currentYaw;
                    _idleSeconds[player.SteamID] = 0;
                    continue;
                }

                var movedEnough = HasMovedEnough(lastPosition, currentPosition);
                var rotatedEnough = HasRotatedEnough(_lastKnownYaw.TryGetValue(player.SteamID, out var lastYaw) ? lastYaw : currentYaw, currentYaw);
                _lastKnownPosition[player.SteamID] = new Vector(currentPosition.X, currentPosition.Y, currentPosition.Z);
                _lastKnownYaw[player.SteamID] = currentYaw;

                if (movedEnough || rotatedEnough)
                {
                    ResetAfkStateForActiveMovement(player);
                    continue;
                }

                var idle = _idleSeconds.TryGetValue(player.SteamID, out var currentIdle) ? currentIdle + 1 : 1;
                _idleSeconds[player.SteamID] = idle;

                if (!_retakeState._isPlayerAFK.TryGetValue(player.SteamID, out var isAfk) || !isAfk)
                {
                    if (idle >= AfkFlagAfterSeconds)
                    {
                        _retakeState._isPlayerAFK[player.SteamID] = true;
                        _afkWarningTime[player.SteamID] = now;

                        PlayAfkWarningSound(player);
                        player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}You are flagged as AFK. Move or you'll be kicked in {KickAfterWarningSeconds} seconds.");
                        _logger.Warning("AfkFlagged", "Player flagged as AFK after inactivity threshold.", player);
                    }

                    continue;
                }

                if (_afkWarningTime.TryGetValue(player.SteamID, out var warningTime)
                    && now - warningTime >= KickAfterWarningSeconds)
                {
                    _logger.Security("AfkKicked", "Player kicked due to AFK timeout.", player);
                    Server.ExecuteCommand($"kickid {player.UserId} AFK");
                    ResetAfkTracking(player.SteamID);
                }
            }

            CleanupAfkTracking(activePlayerIds);
        }

        private static bool HasMovedEnough(Vector previous, Vector current)
        {
            var dx = current.X - previous.X;
            var dy = current.Y - previous.Y;
            var dz = current.Z - previous.Z;

            return (dx * dx) + (dy * dy) + (dz * dz) >= MovementDistanceThreshold * MovementDistanceThreshold;
        }

        private static bool HasRotatedEnough(float previousYaw, float currentYaw)
        {
            var delta = Math.Abs(previousYaw - currentYaw);
            if (delta > 180.0f)
            {
                delta = 360.0f - delta;
            }

            return delta >= ViewAngleThreshold;
        }

        private static void PlayAfkWarningSound(CCSPlayerController player)
        {
            player.ExecuteClientCommand("play ui/panorama/popup_accept_match_beep.vsnd_c");
        }

        private void ResetAfkStateForActiveMovement(CCSPlayerController player)
        {
            var wasAfk = _retakeState._isPlayerAFK.TryGetValue(player.SteamID, out var isAfk) && isAfk;
            _idleSeconds[player.SteamID] = 0;
            _afkWarningTime.Remove(player.SteamID);
            _retakeState._isPlayerAFK[player.SteamID] = false;

            if (wasAfk)
            {
                player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}You are no longer flagged as AFK");
                _logger.Info("AfkRecovered", "Player recovered from AFK state due to activity.", player);
            }
        }

        private void ResetAfkTracking(ulong steamId)
        {
            _lastKnownPosition.Remove(steamId);
            _lastKnownYaw.Remove(steamId);
            _idleSeconds.Remove(steamId);
            _afkWarningTime.Remove(steamId);
            _retakeState._isPlayerAFK.Remove(steamId);
        }

        private void CleanupAfkTracking(HashSet<ulong> activePlayerIds)
        {
            foreach (var steamId in _lastKnownPosition.Keys.ToList())
            {
                if (!activePlayerIds.Contains(steamId))
                {
                    ResetAfkTracking(steamId);
                }
            }
        }
    }
}
