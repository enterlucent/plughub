using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that provides runtime initialization or setup logic during application startup using the built service provider.
    /// Declares all dependency and ordering relationships for conflict-free, deterministic integration of runtime setup tasks.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="AppSetup">Delegate invoked during application startup to perform runtime initialization or setup tasks using the provided <see cref="IServiceProvider"/>. This allows the plugin to resolve services and configure runtime behavior after DI container construction.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginAppSetupDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Action<IServiceProvider>? AppSetup = null,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that supply runtime initialization or setup logic at application startup.
    /// Provides descriptors defining runtime setup actions which accept the built service provider.
    /// </summary>
    [DescriptorProvider("GetAppSetupDescriptors", false)]
    public interface IPluginAppSetup : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors defining runtime setup actions that are invoked during application startup with the built <see cref="IServiceProvider"/>. These actions enable plugins to perform initialization, configuration, or event wiring.
        /// </summary>
        IEnumerable<PluginAppSetupDescriptor> GetAppSetupDescriptors();
    }
}