using Microsoft.Extensions.DependencyInjection;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that supports dependency injection.
    /// Contains type and lifetime information required to register services with a dependency injection container,
    /// as well as dependency, conflict, and ordering relationships relative to other injectors.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="InterfaceType">The interface type to be injected.</param>
    /// <param name="ImplementationType">Optional explicit implementation type; if null, Instance is used.</param>
    /// <param name="Instance">Optional singleton instance to provide if implementation type is not specified.</param>
    /// <param name="Lifetime">Registration lifetime (singleton, scoped, transient) for this injector.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginInjectorDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Type InterfaceType,
        Type? ImplementationType = null,
        object? Instance = null,
        ServiceLifetime Lifetime = ServiceLifetime.Singleton,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that participate in dependency injection.
    /// Provides descriptors for services the plugin injects into the host or other plugins.
    /// </summary>
    [DescriptorProvider("GetInjectionDescriptors", false)]
    public interface IPluginDependencyInjection : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors detailing the dependency injection points
        /// offered by this plugin.
        /// </summary>
        IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors();
    }
}