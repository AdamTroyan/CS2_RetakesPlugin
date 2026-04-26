using CounterStrikeSharp;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.DependencyInjection;
using RetakesPlugin.Core;
using RetakesPlugin.Models;
using RetakesPlugin.Persistence;
using RetakesPlugin.Services.Spawns;
using SpawnPointModel = RetakesPlugin.Models.SpawnPoint;

namespace RetakesPlugin.Services.GameFlow
{
    public class Retake : BasePlugin
    {
        public override string ModuleName => "Retake Plugin";
        public override string ModuleVersion => "2.0";

        private ServiceProvider _serviceProvider = null!;
        private RetakeState _retakeState = null!;
        private ServerSettingsService _serverSettingsService = null!;
        private RoundFlowService _roundFlowService = null!;
        private TeamService _teamService = null!;
        private InstaDefuse _instaDefuse = null!;
        private AfkService _afkService = null!;
        private SpawnRepository _spawnRepository = null!;
        private RetakeLogger _logger = null!;
        private CommandRateLimitService _commandRateLimitService = null!;
        private RetakeCommands _retakeCommands = null!;
        private readonly Random _random = new();

        private List<SpawnPointModel> _spawns = new();

        public override void Load(bool hotReload)
        {
            var services = new ServiceCollection();

            services.AddSingleton(_random);
            services.AddSingleton<RetakeState>();
            services.AddSingleton<RetakeLogger>();
            services.AddSingleton<CommandRateLimitService>();
            services.AddSingleton<ServerSettingsService>();
            services.AddSingleton<TeamService>();
            services.AddSingleton<BombService>();
            services.AddSingleton<InstaDefuse>();
            services.AddSingleton<AfkService>();
            services.AddSingleton<SpawnRepository>();
            services.AddSingleton<SpawnSelectionService>();
            services.AddSingleton<LoadoutService>();
            services.AddSingleton<PlayerTeleportService>();
            services.AddSingleton<RoundFlowService>();
            services.AddSingleton<RetakeCommands>();

            _serviceProvider = services.BuildServiceProvider();

            _retakeState = _serviceProvider.GetRequiredService<RetakeState>();
            _logger = _serviceProvider.GetRequiredService<RetakeLogger>();
            _commandRateLimitService = _serviceProvider.GetRequiredService<CommandRateLimitService>();
            _serverSettingsService = _serviceProvider.GetRequiredService<ServerSettingsService>();
            _roundFlowService = _serviceProvider.GetRequiredService<RoundFlowService>();
            _teamService = _serviceProvider.GetRequiredService<TeamService>();
            _instaDefuse = _serviceProvider.GetRequiredService<InstaDefuse>();
            _afkService = _serviceProvider.GetRequiredService<AfkService>();
            _spawnRepository = _serviceProvider.GetRequiredService<SpawnRepository>();
            _retakeCommands = _serviceProvider.GetRequiredService<RetakeCommands>();

            RegisterEvents();

            AddTimer(1.0f, _afkService.CheckAfkPlayers, CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);
            AddTimer(30.0f, () => _logger.Debug("LogCounters", _logger.GetCountersSnapshot()), CounterStrikeSharp.API.Modules.Timers.TimerFlags.REPEAT);

            _logger.Info("PluginLoaded", "Retakes plugin is loaded.");
        }

        public void Todo()
        {

        }

        public override void Unload(bool hotReload)
        {
            _serviceProvider?.Dispose();
        }

        private void RegisterEvents()
        {
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterEventHandler<EventHegrenadeDetonate>(OnHeGrenadeDetonate);
            RegisterEventHandler<EventMolotovDetonate>(OnMolotovDetonate);
            RegisterEventHandler<EventInfernoStartburn>(OnInfernoStartBurn);
            RegisterEventHandler<EventInfernoExpire>(OnInfernoExpire);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventGameEnd>(OnGameEnd);
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
            RegisterEventHandler<EventBombBegindefuse>(OnBombBeginDefuse);
        }

        [ConsoleCommand("jointeam", "Restrict manual team join")]
        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnJoinTeamCommand(CCSPlayerController? player, CommandInfo info)
        {
            _retakeCommands.HandleJoinTeam(player, info);
        }

        [RequiresPermissions("@css/root")]
        [ConsoleCommand("css_retake_debug", "Toggle retake debug logs")]
        [CommandHelper(minArgs: 0, usage: "[on|off|1|0]", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnRetakeDebugCommand(CCSPlayerController? player, CommandInfo info)
        {
            _retakeCommands.HandleRetakeDebug(player, info);
        }

        [RequiresPermissions("@css/root")]
        [ConsoleCommand("css_logstats", "Print retake log counters")]
        [CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnLogStatsCommand(CCSPlayerController? player, CommandInfo info)
        {
            _retakeCommands.HandleLogStats(player, info);
        }

        [RequiresPermissions("@css/root")]
        [ConsoleCommand("css_save", "Saves a retake spawn point")]
        [CommandHelper(minArgs: 1, usage: "<place>", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnSaveCommand(CCSPlayerController? player, CommandInfo info)
        {
            _retakeCommands.HandleSaveSpawn(ModuleDirectory, player, info);
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            _ = info;

            _roundFlowService.HandlePlayerConnectFull(@event.Userid);

            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult OnBombBeginDefuse(EventBombBegindefuse @event, GameEventInfo info)
        {
            _ = info;

            _instaDefuse.HandleBombBeginDefuse(@event);

            return HookResult.Continue;
        }

        private HookResult OnHeGrenadeDetonate(EventHegrenadeDetonate @event, GameEventInfo info)
        {
            _ = info;

            _instaDefuse.RegisterHeThreat(@event);
            return HookResult.Continue;
        }

        private HookResult OnMolotovDetonate(EventMolotovDetonate @event, GameEventInfo info)
        {
            _ = info;

            _instaDefuse.RegisterMolotovThreat(@event);
            return HookResult.Continue;
        }

        private HookResult OnInfernoStartBurn(EventInfernoStartburn @event, GameEventInfo info)
        {
            _ = info;

            _instaDefuse.RegisterInfernoThreatStart(@event);
            return HookResult.Continue;
        }

        private HookResult OnInfernoExpire(EventInfernoExpire @event, GameEventInfo info)
        {
            _ = info;

            _instaDefuse.RegisterInfernoThreatExpire(@event);
            return HookResult.Continue;
        }

        private void OnMapStart(string mapName)
        {
            _retakeState.CurrentMapName = mapName;

            _spawns = _spawnRepository.LoadSpawns(ModuleDirectory, mapName);

            if (_spawns.Count > 0)
            {
                _logger.Info("SpawnsLoaded", $"Loaded {_spawns.Count} points from {mapName}.json");
            }
            else
            {
                _logger.Warning("SpawnsMissing", $"No spawn points loaded for {mapName}.");
            }
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            _roundFlowService.HandleWarmupEnd();

            return HookResult.Continue;
        }

        private HookResult OnRoundRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            try
            {
                _instaDefuse.ResetThreats();
                _roundFlowService.HandleRoundStart(_spawns);
            }
            catch (Exception ex)
            {
                _logger.Error("OnRoundStartFailed", "Unhandled exception in round start flow.", ex);
            }

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _ = info;
            _roundFlowService.HandleRoundEnd(@event.Winner);

            _roundFlowService.KickBotIf2Players();

            return HookResult.Continue;
        }

        private HookResult OnGameEnd(EventGameEnd @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            _roundFlowService.HandleGameEnd();

            return HookResult.Continue;
        }

        private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            try
            {
                _roundFlowService.HandleRoundFreezeEnd(_spawns);
            }
            catch (Exception ex)
            {
                _logger.Error("OnRoundFreezeEndFailed", "Unhandled exception in round freeze end flow.", ex);
            }

            return HookResult.Continue;
        }
    }
}
