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
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="ConfigType">Strongly typed POCO schema used for plugin configuration.</param>
    /// <param name="ConfigServiceParams">A factory lambda that, when provided an ITokenService, returns the configuration service parameters and policy information (see IConfigServiceParams) chosen by this plugin.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginConfigurationDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Type ConfigType,
        Func<ITokenService, IConfigServiceParams> ConfigServiceParams,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that register configuration options.
    /// Provides metadata describing configuration settings.
    /// </summary>
    [DescriptorProvider("GetConfigurationDescriptors", false)]
    public interface IPluginConfiguration : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors representing the configuration
        /// settings exposed by this plugin.
        /// </summary>
        IEnumerable<PluginConfigurationDescriptor> GetConfigurationDescriptors();
    }
}