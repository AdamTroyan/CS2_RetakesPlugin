using CounterStrikeSharp;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
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
        private SpawnRepository _spawnRepository = null!;
        private readonly Random _random = new();

        private List<SpawnPointModel> _spawns = new();

        public override void Load(bool hotReload)
        {
            var services = new ServiceCollection();

            services.AddSingleton(_random);
            services.AddSingleton<RetakeState>();
            services.AddSingleton<ServerSettingsService>();
            services.AddSingleton<TeamService>();
            services.AddSingleton<BombService>();
            services.AddSingleton<SpawnRepository>();
            services.AddSingleton<SpawnSelectionService>();
            services.AddSingleton<LoadoutService>();
            services.AddSingleton<PlayerTeleportService>();
            services.AddSingleton<RoundFlowService>();

            _serviceProvider = services.BuildServiceProvider();

            _retakeState = _serviceProvider.GetRequiredService<RetakeState>();
            _serverSettingsService = _serviceProvider.GetRequiredService<ServerSettingsService>();
            _roundFlowService = _serviceProvider.GetRequiredService<RoundFlowService>();
            _spawnRepository = _serviceProvider.GetRequiredService<SpawnRepository>();

            RegisterEvents();
            RegisterCommands();

            Console.WriteLine("Retakes Plugin is loaded.");
        }

        public override void Unload(bool hotReload)
        {
            _serviceProvider?.Dispose();
        }

        private void RegisterEvents()
        {
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterEventHandler<EventWarmupEnd>(OnWarmupEnd);
            RegisterEventHandler<EventRoundStart>(OnRoundRoundStart);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventGameEnd>(OnGameEnd);
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
        }

        private void RegisterCommands()
        {
            AddCommand("jointeam", "Restrict manual team join", (player, info) =>
            {
                if (player != null)
                {
                    player.PrintToChat($" {ChatColors.Red}Teams are managed automatically!");
                }
            });
        }

        [ConsoleCommand("css_save", "Saves a retake spawn point")]
        [CommandHelper(minArgs: 1, usage: "<place>", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnSaveCommand(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.PlayerPawn.Value == null)
            {
                return;
            }

            string placeName = info.ArgByIndex(1);
            var pos = player.PlayerPawn.Value.AbsOrigin!;
            var ang = player.PlayerPawn.Value.AbsRotation!;

            var newPoint = new SpawnPointModel
            {
                Place = placeName,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                Yaw = ang.Y
            };

            _spawnRepository.SaveSpawn(ModuleDirectory, _retakeState.CurrentMapName, newPoint);
            player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Point {ChatColors.Gold}{placeName} {ChatColors.Default}saved!");
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            _ = info;

            int activeHumanPlayers = Utilities.GetPlayers().Count(p =>
                p.IsValid
                && p.SteamID != 0
                && (p.TeamNum == (byte)CsTeam.Terrorist || p.TeamNum == (byte)CsTeam.CounterTerrorist));

            if (activeHumanPlayers == 0)
            {
                @event.Userid?.ChangeTeam(CsTeam.Terrorist);
                Server.ExecuteCommand("mp_restartgame 1"); 
            }

            return HookResult.Continue;
        }

        private void OnMapStart(string mapName)
        {
            _retakeState.CurrentMapName = mapName;
            _retakeState.ServerSettingsApplied = false;

            _spawns = _spawnRepository.LoadSpawns(ModuleDirectory, mapName);

            if (_spawns.Count > 0)
            {
                Console.WriteLine($"[Retake] Success: Loaded {_spawns.Count} points from {mapName}.json");
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

            _roundFlowService.HandleRoundStart(_spawns);

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _ = info;
            _roundFlowService.HandleRoundEnd(@event.Winner);

            // Check if now 2 players, if so then kick the bot
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

            _roundFlowService.HandleRoundFreezeEnd(_spawns);

            return HookResult.Continue;
        }
    }
}
