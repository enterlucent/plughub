using Avalonia.Controls;

namespace PlugHub.Shared.Models
{
    /// <summary>
    /// Defines environment-specific settings for the PlugHub application.
    /// These values typically vary by deployment environment (e.g., dev, test, prod)
    /// and can override or complement values in <see cref="AppConfig"/>.
    /// </summary>
    public sealed class AppEnv
    {
        #region AppEnv: Shell & Theme Settings

        /// <inheritdoc cref="AppConfig.MainViewKey"/>
        public string? MainViewKey { get; set; }

        /// <inheritdoc cref="AppConfig.UseDefaultTheme"/>
        public bool? UseDefaultTheme { get; set; }

        /// <inheritdoc cref="AppConfig.SystemTheme"/>
        public string? SystemTheme { get; set; }

        /// <inheritdoc cref="AppConfig.PreferSystemTheme"/>
        public bool? PreferSystemTheme { get; set; }

        /// <inheritdoc cref="AppConfig.PreferUserAccentColor"/>
        public bool? PreferUserAccentColor { get; set; }

        #endregion

        #region AppEnv: Main Window Settings

        /// <inheritdoc cref="AppConfig.WindowIconPath"/>
        public string? WindowIconPath { get; set; }

        /// <inheritdoc cref="AppConfig.WindowTitle"/>
        public string? WindowTitle { get; set; }

        /// <inheritdoc cref="AppConfig.WindowMinWidth"/>
        public double? WindowMinWidth { get; set; }

        /// <inheritdoc cref="AppConfig.WindowMinHeight"/>
        public double? WindowMinHeight { get; set; }

        /// <inheritdoc cref="AppConfig.WindowWidth"/>
        public double? WindowWidth { get; set; }

        /// <inheritdoc cref="AppConfig.WindowHeight"/>
        public double? WindowHeight { get; set; }

        /// <inheritdoc cref="AppConfig.WindowState"/>
        public WindowState? WindowStartupState { get; set; }

        /// <inheritdoc cref="AppConfig.WindowStartupLocation"/>
        public WindowStartupLocation? WindowStartupLocation { get; set; }

        /// <inheritdoc cref="AppConfig.TransparencyPreference"/>
        public WindowTransparencyLevel? TransparencyPreference { get; set; }

        /// <inheritdoc cref="AppConfig.CanResize"/>
        public bool? CanResize { get; set; }

        /// <inheritdoc cref="AppConfig.ShowInTaskbar"/>
        public bool? ShowInTaskbar { get; set; }

        /// <inheritdoc cref="AppConfig.SystemDecorations"/>
        public SystemDecorations? SystemDecorations { get; set; }

        /// <inheritdoc cref="AppConfig.ExtendClientAreaToDecorationsHint"/>
        public bool? ExtendClientAreaToDecorationsHint { get; set; }

        /// <inheritdoc cref="AppConfig.ExtendClientAreaTitleBarHeightHint"/>
        public int? ExtendClientAreaTitleBarHeightHint { get; set; }

        #endregion

        #region AppEnv: Application Identity

        /// <inheritdoc cref="AppConfig.AppName"/>
        public string? AppName { get; set; }

        /// <inheritdoc cref="AppConfig.AppIconPath"/>
        public string? AppIconPath { get; set; }

        /// <inheritdoc cref="AppConfig.AppLink"/>
        public string? AppLink { get; set; }

        #endregion
    }
}