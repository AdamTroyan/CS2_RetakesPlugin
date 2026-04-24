using CounterStrikeSharp;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using System.Text.Json;

namespace RetakesPlugin
{
    public class Retake : BasePlugin
    {
        public override string ModuleName => "Retake Plugin";
        public override string ModuleVersion => "1.0";

        bool _isRetakeActive = false;
        bool _isBombPlanted = false;

        ulong _planterId = 0;
        char _targetSite = '\0';
        private readonly Random _random = new();

        private int _lastWinnerTeam = 0;
        private string _currentMapName = Server.MapName;
        private static readonly string _serverSettingsCommandBatch = string.Join("; ",
            "mp_freezetime 0",
            "mp_teammates_are_enemies 0",
            "mp_autoteambalance 0",
            "mp_limitteams 0",
            "mp_c4timer 40",
            "mp_ignore_round_win_conditions 0",
            "mp_give_player_c4 0",
            "mp_halftime 0",
            "mp_halftime_duration 0",
            "mp_match_can_clinch 0",
            "mp_maxrounds 30",
            "bot_quota_mode fill",
            "bot_quota 8",
            "mp_friendlyfire 0",
            "bot_difficulty 3",
            "bot_defer_to_human_goals 0",
            "bot_defer_to_human_items 0",
            "bot_chatter off",
            "bot_allow_grenades 1",
            "bot_join_after_player 0",
            "bot_unfreeze",
            "sv_autobunnyhopping 1",
            "sv_enablebunnyhopping 1",
            "mp_buytime 0",
            "mp_buy_anywhere 0",
            "mp_buy_during_immunity 0",
            "mp_startmoney 0",
            "mp_maxmoney 0");

        private List<SpawnPoint> _BSpawns = new();

        public override void Load(bool hotReload)
        {
            RegisterEvents();
            RegisterCommands();

            Console.WriteLine("Retakes Plugin is loaded.");
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
                if (player != null) player.PrintToChat($" {ChatColors.Red}Teams are managed automatically!");
            });
        }

        [ConsoleCommand("css_save", "Saves a retake spawn point")]
        [CommandHelper(minArgs: 1, usage: "<place>", whoCanExecute: CommandUsage.CLIENT_ONLY)]
        public void OnSaveCommand(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.PlayerPawn.Value == null) return;

            string placeName = info.ArgByIndex(1);

            var pos = player.PlayerPawn.Value.AbsOrigin!;
            var ang = player.PlayerPawn.Value.AbsRotation!;

            var newPoint = new SpawnPoint
            {
                Place = placeName,
                X = pos.X,
                Y = pos.Y,
                Z = pos.Z,
                Yaw = ang.Y
            };

            string filePath = Path.Combine(ModuleDirectory, $"{_currentMapName}.json");
            List<SpawnPoint> spawns = new();

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    spawns = JsonSerializer.Deserialize<List<SpawnPoint>>(json) ?? new List<SpawnPoint>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Retake Error] Failed to read JSON: {ex.Message}");
                }
            }

            spawns.Add(newPoint);

            string outputJson = JsonSerializer.Serialize(spawns, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, outputJson);

            player.PrintToChat($" {ChatColors.Green}[Retake] {ChatColors.Default}Point {ChatColors.Gold}{placeName} {ChatColors.Default}saved!");
        }

        private static HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            _ = info;

            if (Utilities.GetPlayers().Where(p => p.SteamID != 0).ToList().Count == 1)
            {
                @event.Userid?.ChangeTeam(CsTeam.Terrorist);
                StartRetake(true);
            }

            return HookResult.Continue;
        }

        private void OnMapStart(string mapName)
        {
            _BSpawns.Clear();
            _currentMapName = mapName;

            string filePath = Path.Combine(ModuleDirectory, $"{mapName}.json");

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[Retake] Error: No spawn file found for {mapName}");
                return;
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                var loadedData = JsonSerializer.Deserialize<List<SpawnPoint>>(jsonString);

                if (loadedData != null)
                {
                    _BSpawns = loadedData;
                    Console.WriteLine($"[Retake] Success: Loaded {_BSpawns.Count} points from {Path.GetFileName(filePath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Retake] Critical Error loading JSON: {ex.Message}");
            }
        }

        private HookResult OnWarmupEnd(EventWarmupEnd @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            _isRetakeActive = false;
            _isBombPlanted = false;
            _planterId = 0;
            _targetSite = _random.Next(2) == 0 ? 'A' : 'B';

            Server.NextFrame(() => Server.ExecuteCommand("mp_restartgame 1"));
            Console.WriteLine("[Retake] Warmup ended, retake mode reinitialized.");

            return HookResult.Continue;
        }

        private HookResult OnRoundRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            _isRetakeActive = false;
            _isBombPlanted = false;
            _planterId = 0;
            _targetSite = _random.Next(2) == 0 ? 'A' : 'B';

            ApplyServerSettings(); // Better safe than sorry..

            Server.NextFrame(() =>
            {
                ShuffleTeam();

                Server.NextFrame(() =>
                {
                    _planterId = SelectRandomPlanter();
                    _isRetakeActive = true;
                    Console.WriteLine($"[Retake] Selected site for this round: {_targetSite}");
                });
            });

            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            _ = info;

            _lastWinnerTeam = @event.Winner;
            return HookResult.Continue;
        }

        private HookResult OnGameEnd(EventGameEnd @event, GameEventInfo info)
        {
            _isRetakeActive = false;
            _isBombPlanted = false;

            _planterId = 0;
            _targetSite = '\0';

            _lastWinnerTeam = 0;
            _currentMapName = Server.MapName;

            return HookResult.Continue;
        }

        private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            _ = @event;
            _ = info;

            if (!_isRetakeActive)
            {
                _targetSite = _random.Next(2) == 0 ? 'A' : 'B';
                _planterId = SelectRandomPlanter();
                _isRetakeActive = true;
                Console.WriteLine("[Retake] FreezeEnd fallback activated retake flow.");
            }

            TeleportPlayers();

            if (PlantBomb())
            {
                _isRetakeActive = true;
                _isBombPlanted = true;

                Server.PrintToChatAll($" {ChatColors.Green}[Retake] {ChatColors.Default}The bomb is planted on {ChatColors.Gold}{_targetSite} {ChatColors.Default}site!");
                Console.WriteLine($"[Retake Log] New round started on Site {_targetSite}");
            }

            return HookResult.Continue;
        }

        private static void StartRetake(bool isFirstPlayer)
        {
            if (isFirstPlayer)
            {
                Server.ExecuteCommand("mp_freezetime 0");
                Server.ExecuteCommand("mp_restartgame 1");
            }
        }

        private static void ApplyServerSettings()
        {
            Server.ExecuteCommand(_serverSettingsCommandBatch);

            Console.WriteLine("[Retake] All server settings applied via Plugin.");
        }

        private void ShuffleTeam()
        {
            var players = Utilities.GetPlayers().Where(p => p.IsValid && p.SteamID != 0).ToList();
            int count = players.Count;
            if (count == 0) return;

            int tTargetCount = (count == 1) ? 1 : count / 2;

            var prioritizedPlayers = players.OrderByDescending(p => p.TeamNum == _lastWinnerTeam).ToList();
            ShuffleInPlace(prioritizedPlayers);

            for (int i = 0; i < prioritizedPlayers.Count; i++)
            {
                var p = prioritizedPlayers[i];
                CsTeam nextTeam = (i < tTargetCount) ? CsTeam.Terrorist : CsTeam.CounterTerrorist;

                if (p.TeamNum != (byte)nextTeam)
                {
                    p.SwitchTeam(nextTeam);
                }
            }
        }

        private ulong SelectRandomPlanter()
        {
            var terrorists = Utilities.GetPlayers().Where(p => p.IsValid && p.TeamNum == 2 && p.SteamID != 0).ToList();

            if (terrorists.Count > 0)
            {
                var selectedPlanter = terrorists[_random.Next(terrorists.Count)];

                Console.WriteLine($"[Retake] Planter selected: {selectedPlanter.PlayerName} (ID: {selectedPlanter.SteamID})");

                return selectedPlanter.SteamID;
            }

            Console.WriteLine("[Retake Warning] No valid terrorist found for planting!");
            return 0;
        }

        private bool PlantBomb()
        {
            if (_isBombPlanted) return false;

            var planter = Utilities.GetPlayerFromSteamId(_planterId);
            if (planter == null || planter.PlayerPawn.Value == null) return false;

            var pos = planter.PlayerPawn.Value.AbsOrigin!;
            var ang = planter.PlayerPawn.Value.AbsRotation!;

            var plantedC4 = Utilities.CreateEntityByName<CPlantedC4>("planted_c4");
            if (plantedC4 == null || !plantedC4.IsValid) return false;

            plantedC4.Teleport(pos, ang, new Vector(0, 0, 0));
            plantedC4.BombSite = (_targetSite == 'B' || _targetSite == 'b') ? 1 : 0;
            plantedC4.DispatchSpawn();

            plantedC4.C4Blow = (float)Server.EngineTime + 40.0f;
            plantedC4.BombTicking = true;

            var bombEvent = new EventBombPlanted(false)
            {
                Userid = planter,
                Site = plantedC4.BombSite
            };
            bombEvent.FireEvent(false);

            _isBombPlanted = true;
            return true;
        }

        private void TeleportPlayers()
        {
            if (_targetSite != 'A' && _targetSite != 'B')
            {
                _targetSite = _random.Next(2) == 0 ? 'A' : 'B';
            }

            char site = char.ToLowerInvariant(_targetSite);

            var tPlantPoints = GetSpawnPoints("t", site, "plant");
            var tPlayerPoints = GetSpawnPoints("t", site, "player");
            var ctPlayerPoints = GetSpawnPoints("ct", site, "player");

            var players = Utilities.GetPlayers().Where(p => p.IsValid && p.PawnIsAlive).ToList();

            foreach (var p in players)
            {
                var pawn = p.PlayerPawn.Value;
                if (pawn == null) continue;

                var selectedPoint = PickSpawnPointForPlayer(p, tPlantPoints, tPlayerPoints, ctPlayerPoints);

                if (selectedPoint != null)
                {
                    pawn.Teleport(new Vector(selectedPoint.X, selectedPoint.Y, selectedPoint.Z), new QAngle(0, selectedPoint.Yaw, 0), new Vector(0, 0, 0));
                }

                RemovePlayerWeapons(pawn);
                GiveLoadout(p);
            }
        }

        private SpawnPoint? PickSpawnPointForPlayer(CCSPlayerController player, List<SpawnPoint> tPlantPoints, List<SpawnPoint> tPlayerPoints, List<SpawnPoint> ctPlayerPoints)
        {
            if (player.TeamNum == 2)
            {
                if (player.SteamID == _planterId && tPlantPoints.Count > 0)
                {
                    return PopSpawn(tPlantPoints);
                }

                if (tPlayerPoints.Count > 0)
                {
                    return PopSpawn(tPlayerPoints);
                }
            }
            else if (player.TeamNum == 3 && ctPlayerPoints.Count > 0)
            {
                return PopSpawn(ctPlayerPoints);
            }

            return null;
        }

        private List<SpawnPoint> GetSpawnPoints(string teamPrefix, char site, string type)
        {
            var points = _BSpawns.Where(s => IsMatchingSpawn(s.Place, teamPrefix, site, type)).ToList();

            ShuffleInPlace(points);
            return points;
        }

        private static bool IsMatchingSpawn(string place, string teamPrefix, char site, string type)
        {
            if (string.IsNullOrWhiteSpace(place))
            {
                return false;
            }

            var parts = place.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            if (!parts[0].Equals(teamPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!parts[2].Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var siteWithIndex = parts[1];
            if (siteWithIndex.Length < 2)
            {
                return false;
            }

            if (char.ToLowerInvariant(siteWithIndex[0]) != char.ToLowerInvariant(site))
            {
                return false;
            }

            return siteWithIndex[1..].All(char.IsDigit);
        }

        private void ShuffleInPlace<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static SpawnPoint PopSpawn(List<SpawnPoint> points)
        {
            SpawnPoint point = points[0];
            points.RemoveAt(0);
            return point;
        }

        private static void RemovePlayerWeapons(CCSPlayerPawn pawn)
        {
            var weaponServices = pawn.WeaponServices;
            if (weaponServices?.MyWeapons == null) return;

            var weapons = weaponServices.MyWeapons.ToList();
            foreach (var w in weapons)
            {
                if (w?.Value != null && w.Value.IsValid) w.Value.Remove();
            }
        }

        private static void GiveLoadout(CCSPlayerController player)
        {
            player.GiveNamedItem("weapon_knife");
            player.GiveNamedItem("item_assaultsuit");

            if (player.TeamNum == 2)
            {
                player.GiveNamedItem("weapon_ak47");
                player.GiveNamedItem("weapon_glock");
            }
            else if (player.TeamNum == 3)
            {
                player.GiveNamedItem("weapon_m4a1_silencer");
                player.GiveNamedItem("weapon_usp_silencer");
                player.GiveNamedItem("item_defuser");
            }

            Server.NextFrame(() =>
            {
                player.ExecuteClientCommand("slot1");
            });
        }

        public class SpawnPoint
        {
            public string Place { get; set; } = "";
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Yaw { get; set; }
        }
    }
}
