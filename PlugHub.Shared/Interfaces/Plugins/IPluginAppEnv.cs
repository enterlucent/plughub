using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that provides environment configuration settings (such as runtime options, environment-specific variables, or initialization logic).
    /// Declares all dependency and ordering relationships for conflict-free, deterministic environment setup.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="AppEnv">Optional action to apply modifications to the runtime environment configuration.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginAppEnvDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Action<AppEnv>? AppEnv = null,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that supply runtime environment configuration or initialization logic.
    /// Provides descriptors for customizing application environment state, runtime behavior, and environment-specific setup included with the plugin.
    /// </summary>
    [DescriptorProvider("GetAppEnvDescriptors")]
    public interface IPluginAppEnv : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors defining environment configuration and initialization steps (such as environment variables, runtime options, or configuration adjustments) offered by this plugin.
        /// </summary>
        IEnumerable<PluginAppEnvDescriptor> GetAppEnvDescriptors();
    }
}