using Microsoft.Extensions.DependencyInjection;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;

namespace PlugHub.Shared
{
    /// <summary>
    /// Base descriptor that defines the identity and load characteristics for a plugin or plugin interface.
    /// Encapsulates core metadata, including unique plugin ID, version, and all declared dependency and load order relationships.
    /// Used as a stable contract for all forms of interface-level sorting, resolution, and conflict management within PlugHub.
    /// </summary>
    public abstract record PluginDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null);

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
    /// Describes a plugin component that registers configuration options, schema, or settings with the host system.
    /// Integrates full dependency graph metadata for controlled initialization order, and provides explicit service parameterization.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin defining configuration.</param>
    /// <param name="InterfaceID">Unique identifier for the interface provided by this descriptor.</param>
    /// <param name="Version">Version of the providing plugin.</param>
    /// <param name="ConfigType">Strongly typed POCO schema used for plugin configuration.</param>
    /// <param name="ConfigServiceParams">The configuration service parameters and policy information (see IConfigServiceParams) chosen by this plugin.</param>
    /// <param name="LoadBefore">Configuration descriptors that should be initialized after this one.</param>
    /// <param name="LoadAfter">Configuration descriptors that should be initialized before this one.</param>
    /// <param name="DependsOn">Explicit dependencies required for this configuration to load.</param>
    /// <param name="ConflictsWith">Plugins or configurations that conflict with this one.</param>
    public record PluginConfigurationDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        Type ConfigType,
        IConfigServiceParams ConfigServiceParams,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null
    ) : PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);


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
    public record PluginBrandingDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        Action<IConfigAccessorFor<AppConfig>>? BrandConfiguration = null,
        Action<IServiceProvider>? BrandServices = null,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);
}