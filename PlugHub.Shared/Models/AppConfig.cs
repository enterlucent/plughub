using System.Text.Json;

namespace PlugHub.Shared.Models
{
    public enum LoggingRollingInterval
    {
        Infinite,
        Year,
        Month,
        Day,
        Hour,
        Minute
    }

    public sealed class AppConfig
    {
        public string AppName { get; set; } = "PlugHub";
        public string BaseDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub");

        #region AppConfig: Logging Settings

        public string LoggingDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Logging");

        public LoggingRollingInterval LoggingRolloverInterval { get; set; } = LoggingRollingInterval.Day;

        public string LoggingFileName { get; set; } = "application-.log";

        #endregion

        #region AppConfig: Config Settings

        public string ConfigDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Config");

        public JsonSerializerOptions ConfigJsonOptions { get; set; } = new JsonSerializerOptions();

        public bool HotReloadOnChange { get; set; } = false;

        #endregion

        #region AppConfig: Local Storage

        public string StorageFolderPath { get; set; }
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Storage");

        #endregion

        #region AppConfig: Local Plugins

        public string PluginFolderPath { get; set; }
            = Path.Combine(AppContext.BaseDirectory, "Plugins");

        #endregion
    }
}
