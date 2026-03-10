using LiteDB;
using System;
using System.IO;

namespace ClawCage.WinUI.Services
{
    internal static class AppSettings
    {
        private const string DataDirName = ".claw.data";
        private const string DbFileName = "app-settings.db";
        private const string CollectionName = "settings";

        private sealed class SettingEntry
        {
            [BsonId]
            public string Key { get; set; } = string.Empty;

            public BsonValue Value { get; set; } = BsonValue.Null;
        }

        internal static string? GetString(string key)
        {
            var value = GetValue(key);
            if (value is null || value.IsNull)
                return null;

            return value.IsString ? value.AsString : value.ToString();
        }

        internal static void SetString(string key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            UpsertValue(key, value is null ? BsonValue.Null : new BsonValue(value));
        }

        internal static bool GetBool(string key, bool defaultValue = false)
        {
            var value = GetValue(key);
            if (value is null || value.IsNull)
                return defaultValue;

            if (value.IsBoolean)
                return value.AsBoolean;

            if (value.IsString && bool.TryParse(value.AsString, out var parsed))
                return parsed;

            return defaultValue;
        }

        internal static void SetBool(string key, bool value)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            UpsertValue(key, new BsonValue(value));
        }

        private static BsonValue? GetValue(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            using var db = OpenDatabase();
            var col = db.GetCollection<SettingEntry>(CollectionName);
            var item = col.FindById(key);
            return item?.Value;
        }

        private static void UpsertValue(string key, BsonValue value)
        {
            using var db = OpenDatabase();
            var col = db.GetCollection<SettingEntry>(CollectionName);
            col.Upsert(new SettingEntry
            {
                Key = key,
                Value = value
            });
        }

        private static LiteDatabase OpenDatabase()
        {
            var dbPath = GetDatabasePath();
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            return new LiteDatabase(dbPath);
        }

        private static string GetDatabasePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                DataDirName,
                DbFileName);
        }
    }
}
