using PlugHub.Shared.Interfaces.Services;

namespace PlugHub.Shared.Interfaces
{
    /// <summary>
    /// Base interface for all plugin components within PlugHub.
    /// Used for type identification and discovery.
    /// </summary>
    public interface IPlugin
    { }

    /// <summary>
    /// Interface for plugins that participate in dependency injection.
    /// Provides descriptors for services the plugin injects into the host or other plugins.
    /// </summary>
    public interface IPluginDependencyInjector : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors detailing the dependency injection points
        /// offered by this plugin.
        /// </summary>
        IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors();
    }

    /// <summary>
    /// Interface for plugins that register configuration options.
    /// Provides metadata describing configuration settings.
    /// </summary>
    public interface IPluginConfiguration : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors representing the configuration
        /// settings exposed by this plugin.
        /// </summary>
        IEnumerable<PluginConfigurationDescriptor> GetConfigurationDescriptors(ITokenService tokenService);
    }

    /// <summary>
    /// Interface for plugins that supply branding assets, configuration and/or metadata.
    /// Provides descriptors for visual, branding, and identity-related resources included with the plugin.
    /// </summary>
    public interface IPluginBranding : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors defining branding elements
        /// (such as icons, logos, configuration, or theme colors) offered by this plugin.
        /// </summary>
        IEnumerable<PluginBrandingDescriptor> GetBrandingDescriptors();
    }
}