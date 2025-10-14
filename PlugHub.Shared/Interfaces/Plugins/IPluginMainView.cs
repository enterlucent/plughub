using Avalonia.Controls;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;
using PlugHub.Shared.ViewModels;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Descriptor for a plugin-provided main view.
    /// Defines the view, view model, and optional factories,
    /// along with dependency, conflict, and ordering metadata.
    /// </summary>
    /// <param name="PluginID">Unique identifier of the plugin providing this descriptor.</param>
    /// <param name="DescriptorID">Unique identifier of this descriptor.</param>
    /// <param name="Version">Version string of the descriptor.</param>
    /// <param name="Key">Identifier for this main view; combined with its ViewType to form a unique config key.</param>
    /// <param name="ViewType">Concrete Avalonia <see cref="UserControl"/> type for the view.</param>
    /// <param name="ViewModelType">Associated <see cref="BaseViewModel"/> type for the view.</param>
    /// <param name="ViewFactory">Optional factory to create the view instance.</param>
    /// <param name="ViewModelFactory">Optional factory to create the view model instance.</param>
    /// <param name="LoadBefore">Descriptors that should load after this one.</param>
    /// <param name="LoadAfter">Descriptors that should load before this one.</param>
    /// <param name="DependsOn">Descriptors this one depends on.</param>
    /// <param name="ConflictsWith">Descriptors that cannot coexist with this one.</param>
    public record PluginMainViewDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        string Key,
        Type ViewType,
        Type ViewModelType,
        Func<IServiceProvider, UserControl>? ViewFactory = null,
        Func<IServiceProvider, BaseViewModel>? ViewModelFactory = null,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null
    ) : PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Contract for plugins that contribute main views (top-level pages) to the host.
    /// Provides descriptors describing the plugin's views and related metadata.
    /// </summary>
    [DescriptorProvider("GetMainViewDescriptors", false)]
    public interface IPluginMainView : IPlugin
    {
        /// <summary>
        /// Returns descriptors for the main views provided by this plugin.
        /// </summary>
        IEnumerable<PluginPageDescriptor> GetMainViewDescriptors();
    }
}