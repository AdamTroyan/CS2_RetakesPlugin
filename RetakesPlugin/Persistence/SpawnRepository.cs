using RetakesPlugin.Models;
using RetakesPlugin.Services.GameFlow;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RetakesPlugin.Persistence
{
    public class SpawnRepository
    {
        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };
        private static readonly object _fileLock = new();
        private readonly RetakeLogger _logger;

        public SpawnRepository(RetakeLogger logger)
        {
            _logger = logger;
        }

        public List<SpawnPoint> LoadSpawns(string moduleDirectory, string mapName)
        {
            string filePath = Path.Combine(moduleDirectory, $"{mapName}.json");
            if (!File.Exists(filePath))
            {
                _logger.Warning("SpawnFileMissing", $"No spawn file found for map {mapName}.");
                return new List<SpawnPoint>();
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<SpawnPoint>>(jsonString) ?? new List<SpawnPoint>();
            }
            catch (Exception ex)
            {
                _logger.Error("SpawnLoadFailed", "Critical error loading spawn JSON.", ex);
                return new List<SpawnPoint>();
            }
        }

        public void SaveSpawn(string moduleDirectory, string mapName, SpawnPoint point)
        {
            string filePath = Path.Combine(moduleDirectory, $"{mapName}.json");
            lock (_fileLock)
            {
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
                        _logger.Error("SpawnReadFailed", "Failed to read existing spawn JSON before save.", ex);
                    }
                }

                spawns.Add(point);
                string outputJson = JsonSerializer.Serialize(spawns, _writeOptions);
                File.WriteAllText(filePath, outputJson);
                _logger.Info("SpawnSavedToDisk", $"Spawn saved to {Path.GetFileName(filePath)}. Total points: {spawns.Count}.");
            }
        }
    }
}
