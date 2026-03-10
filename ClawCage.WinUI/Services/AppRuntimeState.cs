namespace ClawCage.WinUI.Services
{
    internal static class AppRuntimeState
    {
        private static string _databasePath = string.Empty;

        internal static string DatabasePath
        {
            get
            {
                return _databasePath;
            }
        }

        internal static void Initialize()
        {
            SetDatabasePath(AppSettings.GetString(AppSettingKeys.DatabasePath), persist: false);
        }

        internal static void SetDatabasePath(string? databasePath, bool persist = true)
        {
            var normalizedPath = (databasePath ?? string.Empty).Trim();

            _databasePath = normalizedPath;

            if (persist)
                AppSettings.SetString(AppSettingKeys.DatabasePath, normalizedPath);
        }
    }
}
