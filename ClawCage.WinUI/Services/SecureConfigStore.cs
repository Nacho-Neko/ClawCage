using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClawCage.WinUI.Services
{
    internal static class SecureConfigStore
    {
        private const string DbFileName = "secure-config.db";
        private const string ConfigDirName = ".clawcage";
        private const string EnvCollectionName = "env";

        private sealed class EnvEntry
        {
            [BsonId]
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        internal static void SaveEnvironmentValues(IDictionary<string, string> values)
        {
            if (values.Count == 0)
                return;

            var dbPath = GetDbPath(AppRuntimeState.DatabasePath);
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            using var db = new LiteDatabase(dbPath);
            var col = db.GetCollection<EnvEntry>(EnvCollectionName);
            foreach (var (key, value) in values)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                col.Upsert(new EnvEntry { Key = key, Value = value ?? string.Empty });
            }
        }

        internal static void SetEnvironmentValue(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            SaveEnvironmentValues(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [key] = value ?? string.Empty
            });
        }

        internal static void RemoveEnvironmentValue(string databasePath, string key)
        {
            if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(key))
                return;

            var dbPath = GetDbPath(databasePath);
            if (!File.Exists(dbPath))
                return;

            using var db = new LiteDatabase(dbPath);
            var col = db.GetCollection<EnvEntry>(EnvCollectionName);
            col.Delete(key);
        }

        internal static void AddPathEntry(string databasePath, string pathEntry)
        {
            if (string.IsNullOrWhiteSpace(databasePath) || string.IsNullOrWhiteSpace(pathEntry))
                return;

            var current = GetEnvironmentValue("PATH");
            var merged = MergePathLikeValue(current, pathEntry);
            SetEnvironmentValue("PATH", merged);
        }

        internal static string MergePathLikeValue(string? existingValue, string? newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath))
                return existingValue ?? string.Empty;

            var entries = (existingValue ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToList();

            if (!entries.Any(p => string.Equals(p.TrimEnd('\\', '/'), newPath.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                entries.Insert(0, newPath);

            return string.Join(";", entries);
        }

        internal static Dictionary<string, string> LoadEnvironmentValues()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(AppRuntimeState.DatabasePath))
                return result;

            var dbPath = GetDbPath(AppRuntimeState.DatabasePath);
            if (!File.Exists(dbPath))
                return result;

            using var db = new LiteDatabase(dbPath);
            var col = db.GetCollection<EnvEntry>(EnvCollectionName);
            foreach (var item in col.FindAll())
                result[item.Key] = item.Value;

            return result;
        }

        public static string? GetEnvironmentValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            var dbPath = GetDbPath(AppRuntimeState.DatabasePath);
            if (!File.Exists(dbPath))
                return null;

            using var db = new LiteDatabase(dbPath);
            var col = db.GetCollection<EnvEntry>(EnvCollectionName);
            var item = col.FindById(key);
            return item?.Value;
        }

        private static string GetDbPath(string databasePath)
        {
            return Path.Combine(databasePath, ConfigDirName, DbFileName);
        }
    }
}
