using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace RetakesPlugin.Services.GameFlow
{
    public class CommandRateLimitService
    {
        private sealed class PlayerCommandState
        {
            public int Count;
            public double WindowStart;
            public double BlockedUntil;
        }

        private readonly RetakeLogger _logger;
        private readonly Dictionary<string, Dictionary<ulong, PlayerCommandState>> _states = new(StringComparer.OrdinalIgnoreCase);

        public CommandRateLimitService(RetakeLogger logger)
        {
            _logger = logger;
        }

        public bool Allow(
            CCSPlayerController player,
            string commandName,
            int maxAttempts,
            double windowSeconds,
            double blockSeconds,
            out int retryAfterSeconds)
        {
            retryAfterSeconds = 0;

            if (player == null || !player.IsValid || player.SteamID == 0)
            {
                return true;
            }

            var now = Server.EngineTime;

            if (!_states.TryGetValue(commandName, out var commandStates))
            {
                commandStates = new Dictionary<ulong, PlayerCommandState>();
                _states[commandName] = commandStates;
            }

            if (!commandStates.TryGetValue(player.SteamID, out var state))
            {
                state = new PlayerCommandState { WindowStart = now };
                commandStates[player.SteamID] = state;
            }

            if (state.BlockedUntil > now)
            {
                retryAfterSeconds = (int)Math.Ceiling(state.BlockedUntil - now);
                return false;
            }

            if (now - state.WindowStart > windowSeconds)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            state.Count++;

            if (state.Count > maxAttempts)
            {
                state.BlockedUntil = now + blockSeconds;
                retryAfterSeconds = (int)Math.Ceiling(blockSeconds);

                _logger.Security(
                    "RateLimitTriggered",
                    $"Command {commandName} exceeded limit ({maxAttempts}/{windowSeconds}s). Blocked for {blockSeconds}s.",
                    player);

                return false;
            }

            if (state.Count == maxAttempts)
            {
                _logger.Warning(
                    "RateLimitNearLimit",
                    $"Command {commandName} reached limit boundary ({maxAttempts}/{windowSeconds}s).",
                    player);
            }

            return true;
        }
    }
}
