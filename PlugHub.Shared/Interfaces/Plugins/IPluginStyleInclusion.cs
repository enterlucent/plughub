using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;


namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that provides Avalonia StyleInclude resources (AXAML files)
    /// that must be loaded before the main UI initializes.
    /// Declares all dependency and ordering relationships for conflict-free, deterministic style loading.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the style-providing plugin.</param>
    /// <param name="InterfaceID">Unique identifier for the interface provided by this descriptor.</param>
    /// <param name="Version">Version of the style include descriptor.</param>
    /// <param name="ResourceUri">URI of the AXAML resource to be loaded as a StyleInclude.</param>
    /// <param name="BaseUri">Base URI for resolving relative resource paths (defaults to plugin's base URI).</param>
    /// <param name="LoadBefore">Style descriptors that should be loaded after this one.</param>
    /// <param name="LoadAfter">Style descriptors that should be loaded before this one.</param>
    /// <param name="DependsOn">Style resources/descriptors that this style depends on.</param>
    /// <param name="ConflictsWith">Style resources that cannot coexist with this descriptor.</param>
    public record PluginStyleIncludeDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        string ResourceUri,
        string? BaseUri = null,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that supply Avalonia StyleInclude resources.
    /// Provides descriptors for AXAML style files that need to be loaded during application bootstrap.
    /// </summary>
    [DescriptorProvider("GetStyleIncludeDescriptors", false)]
    public interface IPluginStyleInclusion : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors defining StyleInclude resources
        /// (AXAML files containing styles, themes, or resource dictionaries) offered by this plugin.
        /// </summary>
        IEnumerable<PluginStyleIncludeDescriptor> GetStyleIncludeDescriptors();
    }
}