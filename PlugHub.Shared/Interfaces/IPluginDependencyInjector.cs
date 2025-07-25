using Microsoft.Extensions.DependencyInjection;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces
{
    /// <summary>
    /// Describes a plugin component that supports dependency injection.
    /// Contains type and lifetime information required to register services with a dependency injection container,
    /// as well as dependency, conflict, and ordering relationships relative to other injectors.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="InterfaceID">Unique identifier for the interface provided by this descriptor.</param>
    /// <param name="Version">Version of the providing plugin.</param>
    /// <param name="InterfaceType">The interface type to be injected.</param>
    /// <param name="ImplementationType">Optional explicit implementation type; if null, Instance is used.</param>
    /// <param name="Instance">Optional singleton instance to provide if implementation type is not specified.</param>
    /// <param name="Lifetime">Registration lifetime (singleton, scoped, transient) for this injector.</param>
    /// <param name="LoadBefore">Plugins/interface injectors that must load after this one.</param>
    /// <param name="LoadAfter">Plugins/interface injectors that must load before this one.</param>
    /// <param name="DependsOn">Plugins that this injector explicitly depends on.</param>
    /// <param name="ConflictsWith">Plugins that cannot be loaded concurrently with this injector.</param>
    public record PluginInjectorDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        Type InterfaceType,
        Type? ImplementationType = null,
        object? Instance = null,
        ServiceLifetime Lifetime = ServiceLifetime.Singleton,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

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
}