using Avalonia.Controls;
using System.Text.Json;

namespace PlugHub.Shared.Models
{
    /// <summary>
    /// Defines the interval at which log files should be rolled over to new files.
    /// </summary>
    public enum LoggingRollingInterval
    {
        /// <summary>
        /// Log files never roll over - all logging goes to a single file.
        /// </summary>
        Infinite,

        /// <summary>
        /// Log files roll over annually.
        /// </summary>
        Year,

        /// <summary>
        /// Log files roll over monthly.
        /// </summary>
        Month,

        /// <summary>
        /// Log files roll over daily.
        /// </summary>
        Day,

        /// <summary>
        /// Log files roll over hourly.
        /// </summary>
        Hour,

        /// <summary>
        /// Log files roll over every minute.
        /// </summary>
        Minute
    }

    /// <summary>
    /// Central configuration class for PlugHub application settings, including logging, 
    /// configuration management, storage paths, and plugin directories. All properties 
    /// have sensible defaults suitable for most deployment scenarios.
    /// </summary>
    public sealed class AppConfig
    {
        /// <summary>
        /// Gets or sets the root directory for all PlugHub application data.
        /// Defaults to %APPDATA%\PlugHub on Windows or equivalent on other platforms.
        /// </summary>
        public string? BaseDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub");

        #region AppConfig: Logging Settings

        /// <summary>
        /// Gets or sets the directory where log files will be written.
        /// Defaults to a "Logging" subdirectory within the base directory.
        /// </summary>
        public string? LoggingDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Logging");

        /// <summary>
        /// Gets or sets how frequently log files should roll over to new files.
        /// Daily rollover provides a good balance between file management and granularity.
        /// </summary>
        public LoggingRollingInterval? LoggingRolloverInterval { get; set; } = LoggingRollingInterval.Day;

        /// <summary>
        /// Gets or sets the filename pattern for log files.
        /// The dash in "application-.log" will be replaced with timestamp information
        /// based on the rollover interval (e.g., "application-20250123.log" for daily).
        /// </summary>
        public string? LoggingFileName { get; set; } = "application-.log";

        #endregion

        #region AppConfig: Config Settings

        /// <summary>
        /// Gets or sets the directory where configuration files will be stored.
        /// Defaults to a "Config" subdirectory within the base directory.
        /// </summary>
        public string? ConfigDirectory { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Config");

        /// <summary>
        /// Gets or sets the JSON serialization options used for configuration file operations.
        /// Customize this to control formatting, naming policies, and other serialization behavior.
        /// </summary>
        public JsonSerializerOptions? ConfigJsonOptions { get; set; } = new JsonSerializerOptions();

        #endregion

        #region AppConfig: Local Storage Settings

        /// <summary>
        /// Gets or sets the directory path for application data storage.
        /// Used by plugins and the core application for persistent data storage.
        /// Defaults to a "Storage" subdirectory within the base directory.
        /// </summary>
        public string? StorageDirectory { get; set; }
            = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PlugHub", "Storage");

        #endregion

        #region AppConfig: Local Plugin Settings

        /// <summary>
        /// Gets or sets the directory path where plugin assemblies are located.
        /// Defaults to a "Plugins" subdirectory within the application's base directory,
        /// making it suitable for portable deployments and easy plugin management.
        /// </summary>
        public string? PluginDirectory { get; set; }
            = Path.Combine(AppContext.BaseDirectory, "Plugins");

        #endregion

        #region AppConfig: Shell & Theme Settings

        /// <summary>
        /// Gets or sets the identifier of the main view to load at startup.
        /// This should match the composite key of a plugin-provided main view
        /// (e.g., "Namespace.ViewType:Key"). If not set, the default main view is used.
        /// </summary>
        public string MainViewKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the application should use
        /// the built-in default theme engine (e.g., FluentAvalonia) at startup.
        /// If false, no theme is applied unless explicitly configured elsewhere.
        /// </summary>
        public bool UseDefaultTheme { get; set; } = true;

        /// <summary>
        /// Gets or sets the system theme to request at startup.
        /// Valid values are the same as Avalonia's ThemeVariant: Default, Light, Dark.
        /// </summary>
        public string SystemTheme { get; set; } = "Default";

        /// <summary>
        /// Gets or sets a value indicating whether the application should prefer
        /// the operating system's theme setting when determining the theme.
        /// </summary>
        public bool PreferSystemTheme { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the application should prefer
        /// the user's accent color from the operating system.
        /// </summary>
        public bool PreferUserAccentColor { get; set; } = true;

        #endregion

        #region AppConfig: Main Window Settings

        /// <summary>
        /// Gets or sets the Avalonia resource URI for the window icon.
        /// Example: "avares://PlugHub/Assets/avalonia-logo.ico"
        /// </summary>
        public string WindowIconPath { get; set; } = "avares://PlugHub/Assets/avalonia-logo.ico";

        /// <summary>
        /// Gets or sets the title text for the main window.
        /// </summary>
        public string WindowTitle { get; set; } = "PlugHub";

        /// <summary>
        /// Gets or sets the minimum width of the main window at startup.
        /// </summary>
        public double WindowMinWidth { get; set; } = 640;

        /// <summary>
        /// Gets or sets the minimum height of the main window at startup.
        /// </summary>
        public double WindowMinHeight { get; set; } = 480;

        /// <summary>
        /// Gets or sets the initial width of the main window.
        /// </summary>
        public double WindowWidth { get; set; } = 1024;

        /// <summary>
        /// Gets or sets the initial height of the main window.
        /// </summary>
        public double WindowHeight { get; set; } = 768;

        /// <summary>
        /// Gets or sets the initial window state (Normal, Minimized, Maximized, FullScreen).
        /// </summary>
        public WindowState WindowStartupState { get; set; } = WindowState.Normal;

        /// <summary>
        /// Gets or sets the startup location of the main window (Manual, CenterScreen, CenterOwner).
        /// </summary>
        public WindowStartupLocation WindowStartupLocation { get; set; } = WindowStartupLocation.CenterScreen;

        /// <summary>
        /// Gets or sets the transparency level hint for the main window.
        /// </summary>
        public WindowTransparencyLevel TransparencyPreference { get; set; } = WindowTransparencyLevel.None;

        /// <summary>
        /// Gets or sets a value indicating whether the main window can be resized.
        /// </summary>
        public bool CanResize { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the main window should appear in the taskbar.
        /// </summary>
        public bool ShowInTaskbar { get; set; } = true;

        /// <summary>
        /// Gets or sets the system decorations mode for the main window.
        /// </summary>
        public SystemDecorations SystemDecorations { get; set; } = SystemDecorations.BorderOnly;

        /// <summary>
        /// Gets or sets a value indicating whether the client area should extend into the window decorations.
        /// </summary>
        public bool ExtendClientAreaToDecorationsHint { get; set; } = true;

        /// <summary>
        /// Gets or sets the height hint for the extended client area title bar.
        /// </summary>
        public int ExtendClientAreaTitleBarHeightHint { get; set; } = 0;

        #endregion

        #region AppConfig: Application Identity

        /// <summary>
        /// Gets or sets the display name of the application.
        /// Defaults to "PlugHub" if not specified.
        /// </summary>
        public string AppName { get; set; } = "PlugHub";

        /// <summary>
        /// Gets or sets the Avalonia resource URI for the application icon.
        /// Example: "avares://PlugHub/Assets/avalonia-logo.ico"
        /// </summary>
        public string AppIconPath { get; set; } = "avares://PlugHub/Assets/avalonia-logo.ico";

        /// <summary>
        /// Gets or sets the application link (e.g., website URL) to open when the app icon is clicked.
        /// </summary>
        public string AppLink { get; set; } = "https://enterlucent.com";

        #endregion
    }
}