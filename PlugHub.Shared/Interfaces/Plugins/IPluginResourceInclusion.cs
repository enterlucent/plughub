using Avalonia.Controls;
using Avalonia.Styling;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin component that provides Avalonia resource dictionaries (AXAML files)
    /// that must be loaded before the main UI initializes.
    /// Declares all dependency and ordering relationships for conflict-free, deterministic resource loading.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this descriptor.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="ResourceUri">URI of the AXAML resource to be loaded as a ResourceInclude.</param>
    /// <param name="BaseUri">Base URI for resolving relative resource paths (defaults to plugin's base URI).</param>
    /// <param name="Factory">Optional delegate that creates one or more <see cref="IResourceDictionary"/> or <see cref="IResourceProvider"/> instances at runtime.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginResourceIncludeDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        string? ResourceUri = null,
        string? BaseUri = null,
        Func<IResourceDictionary>? Factory = null,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null
    ) : PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that supply Avalonia resource dictionaries.
    /// Provides descriptors for AXAML resource files (styles, themes, control templates, etc.)
    /// that need to be loaded during application bootstrap.
    /// </summary>
    [DescriptorProvider("GetResourceIncludeDescriptors", false)]
    public interface IPluginResourceInclusion : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors defining Avalonia resource dictionaries
        /// (AXAML files containing styles, themes, or other resources) offered by this plugin.
        /// </summary>
        IEnumerable<PluginResourceIncludeDescriptor> GetResourceIncludeDescriptors();
    }
}