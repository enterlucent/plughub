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
        /// Gets the extension point descriptors for the specified <see cref="IPlugin"/> interface type.
        /// </summary>
        /// <param name="pluginInterfaceType">
        /// The interface type that defines a plugin extension point. Must be assignable from <see cref="IPlugin"/>.
        /// </param>
        /// <returns>
        /// A list of <see cref="PluginDescriptor"/> objects describing how plugins interact with the given <see cref="IPlugin"/> interface type.
        /// </returns>
        public List<PluginDescriptor> GetDescriptorsForInterface(Type pluginInterfaceType);
        PluginManifest GetManifest();
        bool IsEnabled(Guid pluginId, Type interfaceType);
        void SaveManifest(PluginManifest manifest);
        void SetAllEnabled(Guid pluginId, bool enabled = true);
        void SetEnabled(Guid pluginId, Type interfaceType, bool enabled = true);
    }
}