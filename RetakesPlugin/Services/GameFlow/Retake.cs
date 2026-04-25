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
        private TeamService _teamService = null!;
        private BombService _bombService = null!;
        private SpawnRepository _spawnRepository = null!;
        private PlayerTeleportService _playerTeleportService = null!;
        private readonly Random _random = new();

        private List<SpawnPointModel> _spawns = new();
        private bool _playersTeleportedThisRound;

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

            _serviceProvider = services.BuildServiceProvider();

            _retakeState = _serviceProvider.GetRequiredService<RetakeState>();
            _serverSettingsService = _serviceProvider.GetRequiredService<ServerSettingsService>();
            _teamService = _serviceProvider.GetRequiredService<TeamService>();
            _bombService = _serviceProvider.GetRequiredService<BombService>();
            _spawnRepository = _serviceProvider.GetRequiredService<SpawnRepository>();
            _playerTeleportService = _serviceProvider.GetRequiredService<PlayerTeleportService>();

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

            _spawnRepository.SaveSpawn(ModuleDirectory, _retakeState._currentMapName, newPoint);
            player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Point {ChatColors.Gold}{placeName} {ChatColors.Default}saved!");
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            _ = info;

            if (Utilities.GetPlayers().Count(p => p.SteamID != 0) == 1)
            {
                @event.Userid?.ChangeTeam(CsTeam.Terrorist);
                _serverSettingsService.StartRetakeIfFirstPlayer(true);
            }

            return HookResult.Continue;
        }

        private void OnMapStart(string mapName)
        {
            _retakeState._currentMapName = mapName;

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

            ResetRoundState();

            Server.NextFrame(() => Server.ExecuteCommand("mp_restartgame 1"));
            Console.WriteLine("[Retake] Warmup ended, retake mode reinitialized.");

            return HookResult.Continue;
        }

        private HookResult OnRoundRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            ResetRoundState();

            _serverSettingsService.EnsureApplied();


            Server.NextFrame(() =>
            {
                _teamService.ShuffleTeam();

                Server.NextFrame(() =>
                {
                    _retakeState._planterId = _teamService.SelectRandomPlanter();
                    _retakeState._isRetakeActive = true;
                    Console.WriteLine($"[Retake] Selected site for this round: {_retakeState._targetSite}");

                    _playersTeleportedThisRound = _playerTeleportService.TeleportPlayers(_spawns);
                    if (!_playersTeleportedThisRound)
                    {
                        Console.WriteLine("[Retake Warning] RoundStart teleport failed, will retry at FreezeEnd.");
                    }
                });
            });

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _ = info;
            _retakeState._lastWinnerTeam = @event.Winner;
            return HookResult.Continue;
        }

        private HookResult OnGameEnd(EventGameEnd @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            _retakeState._isRetakeActive = false;
            _retakeState._isBombPlanted = false;
            _retakeState._planterId = 0;
            _retakeState._targetSite = '\0';
            _retakeState._lastWinnerTeam = 0;
            _retakeState._currentMapName = Server.MapName;

            return HookResult.Continue;
        }

        private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            if (!_retakeState._isRetakeActive)
            {
                _retakeState._targetSite = _random.Next(2) == 0 ? 'A' : 'B';
                _retakeState._planterId = _teamService.SelectRandomPlanter();
                _retakeState._isRetakeActive = true;
                Console.WriteLine("[Retake] FreezeEnd fallback activated retake flow.");
            }

            if (!_playersTeleportedThisRound && !_playerTeleportService.TeleportPlayers(_spawns))
            {
                Console.WriteLine("[Retake Warning] FreezeEnd aborted: teleport phase had no valid spawns.");
                return HookResult.Continue;
            }

            _playersTeleportedThisRound = true;

            if (_bombService.PlantBomb())
            {
                _retakeState._isRetakeActive = true;
                _retakeState._isBombPlanted = true;

                Server.PrintToChatAll($" {ChatColors.Green}[Retake] {ChatColors.Default}The bomb is planted on {ChatColors.Gold}{_retakeState._targetSite} {ChatColors.Default}site!");
                Console.WriteLine($"[Retake Log] New round started on Site {_retakeState._targetSite}");
            }

            return HookResult.Continue;
        }

        private void ResetRoundState()
        {
            _retakeState._isRetakeActive = false;
            _retakeState._isBombPlanted = false;
            _retakeState._planterId = 0;
            _retakeState._targetSite = _random.Next(2) == 0 ? 'A' : 'B';
            _playersTeleportedThisRound = false;
        }
    }
}
