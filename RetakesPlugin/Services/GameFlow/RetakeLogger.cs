using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using RetakesPlugin.Core;

namespace RetakesPlugin.Services.GameFlow
{
    public enum RetakeLogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Security
    }

    public class RetakeLogger
    {
        private readonly RetakeState _retakeState;
        private readonly Dictionary<string, int> _eventCounters = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<RetakeLogLevel, int> _levelCounters = new();
        private bool _debugEnabled;

        public RetakeLogger(RetakeState retakeState)
        {
            _retakeState = retakeState;
        }

        public bool IsDebugEnabled => _debugEnabled;

        public void SetDebugEnabled(bool enabled, CCSPlayerController? changedBy = null)
        {
            _debugEnabled = enabled;
            Info("DebugMode", $"Debug mode set to {(enabled ? "ON" : "OFF")}", changedBy);
        }

        public void Debug(string eventName, string message, CCSPlayerController? player = null)
        {
            Write(RetakeLogLevel.Debug, eventName, message, player);
        }

        public void Info(string eventName, string message, CCSPlayerController? player = null)
        {
            Write(RetakeLogLevel.Info, eventName, message, player);
        }

        public void Warning(string eventName, string message, CCSPlayerController? player = null)
        {
            Write(RetakeLogLevel.Warning, eventName, message, player);
        }

        public void Error(string eventName, string message, Exception? exception = null, CCSPlayerController? player = null)
        {
            var details = exception == null ? message : $"{message} | {exception.GetType().Name}: {exception.Message}";
            Write(RetakeLogLevel.Error, eventName, details, player);
        }

        public void Security(string eventName, string message, CCSPlayerController? player = null)
        {
            Write(RetakeLogLevel.Security, eventName, message, player);
        }

        public string GetCountersSnapshot()
        {
            var levels = string.Join(", ", _levelCounters.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
            var events = string.Join(", ", _eventCounters.OrderByDescending(kvp => kvp.Value).Take(6).Select(kvp => $"{kvp.Key}:{kvp.Value}"));

            return $"levels=[{levels}] topEvents=[{events}]";
        }

        private void Write(RetakeLogLevel level, string eventName, string message, CCSPlayerController? player)
        {
            if (level == RetakeLogLevel.Debug && !_debugEnabled)
            {
                return;
            }

            if (_levelCounters.ContainsKey(level))
            {
                _levelCounters[level]++;
            }
            else
            {
                _levelCounters[level] = 1;
            }

            if (_eventCounters.ContainsKey(eventName))
            {
                _eventCounters[eventName]++;
            }
            else
            {
                _eventCounters[eventName] = 1;
            }

            var mapName = string.IsNullOrWhiteSpace(_retakeState.CurrentMapName) ? Server.MapName : _retakeState.CurrentMapName;
            var round = _retakeState.RoundNumber;

            string playerSuffix = string.Empty;
            if (player != null && player.IsValid)
            {
                playerSuffix = $" [player:{player.PlayerName} steam:{player.SteamID}]";
            }

            Console.WriteLine($"[Retake][{level}][{eventName}][map:{mapName}][round:{round}] {message}{playerSuffix}");
        }
    }
}
