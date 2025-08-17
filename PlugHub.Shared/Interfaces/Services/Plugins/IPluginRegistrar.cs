using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Services.Plugins
{

    /// <summary>
    /// Defines methods for enabling or disabling plugins and plugin interfaces in the PlugHub system.
    /// All state changes are persisted in the plugin registry manifest.
    /// </summary>
    public interface IPluginRegistrar
    {
        /// <summary>
        /// Returns a read-only list of currently enabled plugins.
        /// The list cannot be modified. Item mutability depends on PluginReference implementation.
        /// </summary>
        public IReadOnlyList<PluginReference> GetEnabledPlugins();


        /// <summary>
        /// Gets whether any interface of the specified plugin is enabled.
        /// </summary>
        /// <param name="plugin">The plugin instance to query.</param>
        /// <returns>True if any associated interface is enabled; otherwise, false.</returns>
        public bool IsEnabled(PluginReference plugin);

        /// <summary>
        /// Gets whether a specific plugin interface is enabled.
        /// </summary>
        /// <param name="pluginInterface">The plugin interface to query.</param>
        /// <returns>True if enabled; otherwise, false.</returns>
        public bool IsEnabled(PluginInterface pluginInterface);


        /// <summary>
        /// Disables a plugin interface, updating the manifest.
        /// </summary>
        /// <param name="pluginInterface">The plugin interface to disable.</param>
        public void SetDisabled(PluginInterface pluginInterface);

        /// <summary>
        /// Disables a plugin and all its interfaces, updating the manifest.
        /// </summary>
        /// <param name="plugin">The plugin instance to disable.</param>
        public void SetDisabled(PluginReference plugin);


        /// <summary>
        /// Enables a plugin interface, updating the manifest.
        /// </summary>
        /// <param name="pluginInterface">The plugin interface to enable.</param>
        public void SetEnabled(PluginInterface pluginInterface);

        /// <summary>
        /// Enables a plugin and all its interfaces, updating the manifest.
        /// </summary>
        /// <param name="plugin">The plugin instance to enable.</param>
        public void SetEnabled(PluginReference plugin);
    }
}