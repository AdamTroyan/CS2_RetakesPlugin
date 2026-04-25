using RetakesPlugin.Models;
using System.Collections.Generic;
using System.Linq;

namespace RetakesPlugin.Services.Spawns
{
    public class SpawnSelectionService
    {
        private readonly Random _random;

        public SpawnSelectionService(Random random)
        {
            _random = random;
        }

        public List<SpawnPoint> GetSpawnPoints(List<SpawnPoint> allSpawns, string teamPrefix, char site, string type)
        {
            var points = allSpawns.Where(s => IsMatchingSpawn(s.Place, teamPrefix, site, type)).ToList();
            ShuffleInPlace(points);
            return points;
        }

        public bool IsMatchingSpawn(string place, string teamPrefix, char site, string type)
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

        public SpawnPoint PopSpawn(List<SpawnPoint> points)
        {
            var point = points[0];
            points.RemoveAt(0);
            return point;
        }

        public void ShuffleInPlace<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
