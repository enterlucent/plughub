namespace PlugHub.Shared.Models
{
    /// <summary>
    /// Defines environment-specific settings for the PlugHub application.
    /// These values typically vary by deployment environment (e.g., dev, test, prod)
    /// and can override or complement values in <see cref="AppConfig"/>.
    /// </summary>
    public sealed class AppEnv
    {
        #region AppEnv: Application Identity

        /// <summary>
        /// Gets or sets the display name of the application.
        /// Defaults to "PlugHub" if not specified.
        /// </summary>
        public string AppName { get; set; } = "PlugHub";

        #endregion

        #region AppEnv: Main View

        /// <summary>
        /// Gets or sets the identifier of the main view to load at startup.
        /// This should match the composite key of a plugin-provided main view
        /// (e.g., "Namespace.ViewType:Key"). If not set, the default main view is used.
        /// </summary>
        public string? MainViewKey { get; set; }

        #endregion
    }
}