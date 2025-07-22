using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>
    /// Provides core plugin management operations for discovery and dynamic loading in the PlugHub system.
    /// </summary>
    public interface IPluginService
    {
        /// <summary>
        /// Scans the specified directory for eligible plugin assemblies, discovers available plugins, and returns their descriptors.
        /// This method loads metadata only and does not instantiate plugin types.
        /// </summary>
        /// <param name="pluginDirectory">The file system directory to scan for plugin assemblies.</param>
        /// <returns>A collection of <see cref="Plugin"/> objects representing each discovered plugin and its interfaces.</returns>
        IEnumerable<Plugin> Discover(string pluginDirectory);

        /// <summary>
        /// Loads and returns an implementation instance for a given plugin interface, cast to the specified type.
        /// Searches for and loads the concrete plugin, then attempts to cast the result to the desired interface.
        /// Returns <c>null</c> if the plugin cannot be loaded or does not implement the requested interface.
        /// </summary>
        /// <typeparam name="TInterface">The target interface to retrieve; must be a class interface.</typeparam>
        /// <param name="pluginInterface">Descriptor of the plugin interface to instantiate.</param>
        /// <returns>An instance implementing <typeparamref name="TInterface"/>, or <c>null</c> if not available.</returns>
        TInterface? GetLoadedInterface<TInterface>(PluginInterface pluginInterface) where TInterface : class;

        /// <summary>
        /// Loads and returns a plugin instance for the specified interface, cast to the provided strongly-typed plugin base class.
        /// Returns <c>null</c> if the plugin cannot be loaded or does not derive from <typeparamref name="TPlugin"/>.
        /// </summary>
        /// <typeparam name="TPlugin">The expected concrete plugin base type.</typeparam>
        /// <param name="componentInfo">Descriptor of the plugin interface/component to load.</param>
        /// <returns>An instance of <typeparamref name="TPlugin"/>, or <c>null</c> if not available.</returns>
        TPlugin? GetLoadedPlugin<TPlugin>(PluginInterface componentInfo) where TPlugin : PluginBase;
    }
}
