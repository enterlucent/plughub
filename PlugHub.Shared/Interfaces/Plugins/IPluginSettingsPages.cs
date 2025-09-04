using Avalonia.Controls;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;
using PlugHub.Shared.ViewModels;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin-provided page, including its view, view model, group, icon, 
    /// resource dictionaries, and optional menu bar configuration.
    /// Inherits from <see cref="ControlDescsriptor"/>.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this page.</param>
    /// <param name="InterfaceID">Unique identifier for the interface provided by this descriptor.</param>
    /// <param name="Version">Version of the providing plugin.</param>
    /// <param name="ViewType">The concrete type of the Avalonia <see cref="UserControl"/> representing the page's view.</param>
    /// <param name="ViewModelType">The type of the associated view model, typically derived from <see cref="ViewModelBase"/>.</param>
    /// <param name="Group">The logical group for this support page.</param>
    /// <param name="Name">The display name for the support page, as shown in the UI.</param>
    /// <param name="IconSource">A resource key or file path for the icon representing this page.</param>
    /// <param name="ViewFactory">A factory function that creates an instance of the page's <see cref="UserControl"/>. Receives an <see cref="IServiceProvider"/> for dependency injection.</param>
    /// <param name="ViewModelFactory">A factory function that creates an instance of the page's <see cref="ViewModelBase"/>. Receives an <see cref="IServiceProvider"/> for dependency injection.</param>
    /// <param name="LoadBefore">Plugins/interface pages that must load after this one.</param>
    /// <param name="LoadAfter">Plugins/interface pages that must load before this one.</param>
    /// <param name="DependsOn">Plugins that this page explicitly depends on.</param>
    /// <param name="ConflictsWith">Plugins that cannot be loaded concurrently with this page.</param>    
    public record SettingsPageDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        Type ViewType,
        Type ViewModelType,
        string Group,
        string Name,
        string IconSource,
        Func<IServiceProvider, UserControl> ViewFactory,
        Func<IServiceProvider, BaseViewModel> ViewModelFactory,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
            PluginPageDescriptor(PluginID, InterfaceID, Version, ViewType, ViewModelType, Name, IconSource, ViewFactory, ViewModelFactory, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that provide settings or configuration pages.
    /// Implement this interface to register one or more custom settings pages with the host application.
    /// </summary>
    [DescriptorProvider("GetSettingsPageDescriptors", false)]
    public interface IPluginSettingsPages : IPlugin
    {
        /// <summary>
        /// Gets the list of settings pages provided by the plugin.
        /// Each <see cref="SettingsPageDescriptor"/> describes a single page, including its view, view model, and metadata.
        /// </summary>
        public List<SettingsPageDescriptor> GetSettingsPageDescriptors();
    }
}