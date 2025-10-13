using Avalonia.Styling;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;


namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that provides Avalonia StyleInclude resources (AXAML files)
    /// that must be loaded before the main UI initializes.
    /// Declares all dependency and ordering relationships for conflict-free, deterministic style loading.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="ResourceUri">URI of the AXAML resource to be loaded as a StyleInclude.</param>
    /// <param name="BaseUri">Base URI for resolving relative resource paths (defaults to plugin's base URI).</param>
    /// <param name="Factory">Optional delegate that creates one or more <see cref="IStyle"/> instances at runtime.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginStyleIncludeDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        string? ResourceUri = null,
        string? BaseUri = null,
        Func<IStyle>? Factory = null,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null
    ) : PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

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