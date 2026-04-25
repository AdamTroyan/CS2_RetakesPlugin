using RetakesPlugin.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RetakesPlugin.Persistence
{
    public class SpawnRepository
    {
        private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };
        private static readonly object _fileLock = new();

        public List<SpawnPoint> LoadSpawns(string moduleDirectory, string mapName)
        {
            string filePath = Path.Combine(moduleDirectory, $"{mapName}.json");
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[Retake] Error: No spawn file found for {mapName}");
                return new List<SpawnPoint>();
            }

            try
            {
                string jsonString = File.ReadAllText(filePath);
                return JsonSerializer.Deserialize<List<SpawnPoint>>(jsonString) ?? new List<SpawnPoint>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Retake] Critical Error loading JSON: {ex.Message}");
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
                        Console.WriteLine($"[Retake Error] Failed to read JSON: {ex.Message}");
                    }
                }

                spawns.Add(point);
                string outputJson = JsonSerializer.Serialize(spawns, _writeOptions);
                File.WriteAllText(filePath, outputJson);
            }
        }
    }
}
