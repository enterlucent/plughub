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
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="ViewType">The concrete type of the Avalonia <see cref="UserControl"/> representing the page's view.</param>
    /// <param name="ViewModelType">The type of the associated view model, typically derived from <see cref="ViewModelBase"/>.</param>
    /// <param name="Group">The logical group for this support page.</param>
    /// <param name="Name">The display name for the support page, as shown in the UI.</param>
    /// <param name="IconSource">A resource key or file path for the icon representing this page.</param>
    /// <param name="ViewFactory">A factory function that creates an instance of the page's <see cref="UserControl"/>. Receives an <see cref="IServiceProvider"/> for dependency injection.</param>
    /// <param name="ViewModelFactory">A factory function that creates an instance of the page's <see cref="ViewModelBase"/>. Receives an <see cref="IServiceProvider"/> for dependency injection.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record SettingsPageDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Type ViewType,
        Type ViewModelType,
        string Group,
        string Name,
        string IconSource,
        Func<IServiceProvider, UserControl>? ViewFactory = null,
        Func<IServiceProvider, BaseViewModel>? ViewModelFactory = null,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null) : PluginPageDescriptor(PluginID, DescriptorID, Version, ViewType, ViewModelType, Name, IconSource, ViewFactory, ViewModelFactory, LoadBefore, LoadAfter, DependsOn, ConflictsWith)
    {
        /// <summary>
        /// Creates a <see cref="ContentItemViewModel"/> from the specified
        /// <see cref="SettingsPageDescriptor"/> using the provided <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="provider">The service provider used to resolve the view, view model, and their dependencies.</param>
        /// <param name="descriptor">The descriptor containing type and factory information for the settings page.</param>
        /// <returns>
        /// A fully constructed <see cref="ContentItemViewModel"/> with its view and view model instantiated and bound, or <c>null</c> if either cannot be resolved.
        /// </returns>
        public static ContentItemViewModel? GetItemViewModel(IServiceProvider provider, SettingsPageDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(descriptor);

            return PluginPageDescriptor.GetItemViewModel(provider, descriptor);
        }
    }

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