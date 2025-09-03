using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that provides branding assets (such as logos, theme resources),
    /// interface-specific branding, or related metadata.
    /// Declares all dependency and ordering relationships for conflict-free, deterministic branding integration.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the branding-providing plugin.</param>
    /// <param name="InterfaceID">Unique identifier for the interface provided by this descriptor.</param>
    /// <param name="Version">Version of the branding descriptor.</param>
    /// <param name="LoadBefore">Branding descriptors that should be applied after this one.</param>
    /// <param name="LoadAfter">Branding descriptors that should be applied before this one.</param>
    /// <param name="DependsOn">Assets/descriptors that this branding depends on.</param>
    /// <param name="ConflictsWith">Branding assets that cannot coexist with this descriptor.</param>
    public record PluginAppConfigDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        Action<AppConfig>? AppConfiguration = null,
        Action<IServiceProvider>? AppServices = null,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that supply branding assets, configuration and/or metadata.
    /// Provides descriptors for visual, branding, and identity-related resources included with the plugin.
    /// </summary>
    [DescriptorProvider("GetAppConfigDescriptors", false)]
    public interface IPluginAppConfig : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors defining branding elements
        /// (such as icons, logos, configuration, or theme colors) offered by this plugin.
        /// </summary>
        IEnumerable<PluginAppConfigDescriptor> GetAppConfigDescriptors();
    }
}