using PlugHub.Shared.Attributes;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that registers configuration options, schema, or settings with the host system.
    /// Integrates full dependency graph metadata for controlled initialization order, and provides explicit service parameterization.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin defining configuration.</param>
    /// <param name="DescriptorID">Unique identifier for the interface provided by this descriptor.</param>
    /// <param name="Version">Version of the providing plugin.</param>
    /// <param name="ConfigType">Strongly typed POCO schema used for plugin configuration.</param>
    /// <param name="ConfigServiceParams">A factory lambda that, when provided an ITokenService, returns the configuration service parameters and policy information (see IConfigServiceParams) chosen by this plugin.</param>
    /// <param name="LoadBefore">Configuration descriptors that should be initialized after this one.</param>
    /// <param name="LoadAfter">Configuration descriptors that should be initialized before this one.</param>
    /// <param name="DependsOn">Explicit dependencies required for this configuration to load.</param>
    /// <param name="ConflictsWith">Plugins or configurations that conflict with this one.</param>
    public record PluginConfigurationDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Type ConfigType,
        Func<ITokenService, IConfigServiceParams> ConfigServiceParams,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that register configuration options.
    /// Provides metadata describing configuration settings.
    /// </summary>
    [ProvidesDescriptor("GetConfigurationDescriptors", false)]
    public interface IPluginConfiguration : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors representing the configuration
        /// settings exposed by this plugin.
        /// </summary>
        IEnumerable<PluginConfigurationDescriptor> GetConfigurationDescriptors();
    }
}