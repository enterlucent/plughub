using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Services.Plugins
{
    /// <summary>
    /// Manages the enabled/disabled state of plugins and their interfaces within the PlugHub system.
    /// Responsible for persisting state changes to the plugin manifest, and providing metadata about plugin extension points.
    /// </summary>
    public interface IPluginRegistrar
    {
        /// <summary>
        /// Retrieves the list of plugin descriptors associated with a specified plugin interface type.
        /// These descriptors define how various plugins implement or extend the given interface.
        /// </summary>
        /// <param name="pluginInterfaceType">
        /// The <see cref="Type"/> representing a plugin extension interface.
        /// Must be assignable to <see cref="IPlugin"/>.
        /// </param>
        /// <returns>
        /// A <see cref="List{PluginDescriptor}"/> containing metadata descriptors for plugins that interact with the specified interface type.
        /// </returns>
        public List<PluginDescriptor> GetDescriptorsForInterface(Type pluginInterfaceType);

        /// <summary>
        /// Retrieves the current plugin manifest representing the global interface enable state.
        /// </summary>
        /// <returns>
        /// The <see cref="PluginManifest"/> instance that records interface enable status.
        /// </returns>
        public PluginManifest GetManifest();

        /// <summary>
        /// Persists the given <see cref="PluginManifest"/> to the underlying storage.
        /// </summary>
        /// <param name="manifest">
        /// The <see cref="PluginManifest"/> instance containing updated interface states to write.
        /// </param>
        /// <remarks>
        /// This is the synchronous entry point.  
        /// It delegates to <see cref="SaveManifestAsync(PluginManifest)"/> but blocks on completion.  
        /// Use this method only when calling code cannot be async (e.g., constructors, legacy APIs).  
        /// For new code paths, prefer the async version to avoid blocking the thread.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the manifest is null or contains null <c>InterfaceStates</c>.</exception>
        /// <exception cref="InvalidOperationException">Propagated if the underlying accessor fails.</exception>
        public void SaveManifest(PluginManifest manifest);

        /// <summary>
        /// Asynchronously persists the given <see cref="PluginManifest"/> to the underlying storage.
        /// </summary>
        /// <param name="manifest">
        /// The <see cref="PluginManifest"/> instance containing updated interface states to write.
        /// </param>
        /// <remarks>
        /// This is the async-first implementation.  
        /// It should be preferred in most scenarios as it does not block the calling thread.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown if the manifest is null or contains null <c>InterfaceStates</c>.</exception>
        /// <exception cref="InvalidOperationException">Propagated if the underlying accessor fails.</exception>
        public Task SaveManifestAsync(PluginManifest manifest);

        /// <summary>
        /// Determines whether a specific plugin interface is currently enabled for a given plugin ID.
        /// </summary>
        /// <param name="pluginId">The <see cref="Guid"/> uniquely identifying the plugin.</param>
        /// <param name="interfaceType">The <see cref="Type"/> of the plugin interface.</param>
        /// <returns>
        /// <c>true</c> if the specified interface on the plugin is enabled; otherwise, <c>false</c>.
        /// </returns>
        public bool IsEnabled(Guid pluginId, Type interfaceType);

        /// <summary>
        /// Sets the enabled or disabled state of a specific interface for a specified plugin.
        /// </summary>
        /// <param name="pluginId">The unique <see cref="Guid"/> of the plugin.</param>
        /// <param name="interfaceType">The <see cref="Type"/> of the interface to modify.</param>
        /// <param name="enabled">
        /// <c>true</c> to enable the interface; <c>false</c> to disable it.
        /// Defaults to <c>true</c>.
        /// </param>
        public void SetEnabled(Guid pluginId, Type interfaceType, bool enabled = true);

        /// <summary>
        /// Sets all interfaces of a given plugin to an enabled or disabled state.
        /// </summary>
        /// <param name="pluginId">The unique <see cref="Guid"/> of the plugin.</param>
        /// <param name="enabled">
        /// <c>true</c> to enable all interfaces; <c>false</c> to disable them.
        /// Defaults to <c>true</c>.
        /// </param>
        public void SetAllEnabled(Guid pluginId, bool enabled = true);
    }
}
