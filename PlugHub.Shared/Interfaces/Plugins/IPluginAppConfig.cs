using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that provides branding assets (such as logos, theme resources), interface-specific branding, or related metadata.
    /// Declares all dependency and ordering relationships for conflict-free, deterministic branding integration.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginAppConfigDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Action<AppConfig>? AppConfig = null,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that supply branding assets, configuration and/or metadata.
    /// Provides descriptors for visual, branding, and identity-related resources included with the plugin.
    /// </summary>
    [DescriptorProvider("GetAppConfigDescriptors", true)]
    public interface IPluginAppConfig : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors defining branding elements (such as icons, logos, configuration, or theme colors) offered by this plugin.
        /// </summary>
        IEnumerable<PluginAppConfigDescriptor> GetAppConfigDescriptors();
    }
}