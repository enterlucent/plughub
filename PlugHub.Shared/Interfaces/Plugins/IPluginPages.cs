using Avalonia.Controls;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;
using PlugHub.Shared.ViewModels;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin-provided page, including its view, view model, icon, 
    /// and dependency relationships relative to other pages.
    /// Contains type and factory information required to create page instances,
    /// as well as dependency, conflict, and ordering relationships relative to other page providers.
    /// </summary>
    /// <param name="PluginID">Unique identifier for the plugin providing this injector.</param>
    /// <param name="DescriptorID">Unique identifier for the descriptor.</param>
    /// <param name="Version">Version of the descriptor.</param>
    /// <param name="ViewType">The concrete type of the Avalonia UserControl for the page.</param>
    /// <param name="ViewModelType">The type of the associated ViewModel.</param>
    /// <param name="Name">Display name for the page.</param>
    /// <param name="IconSource">Resource key or path for the page icon.</param>
    /// <param name="ViewFactory">Factory function to create the UserControl instance.</param>
    /// <param name="ViewModelFactory">Factory function to create the ViewModel instance.</param>
    /// <param name="LoadBefore">Descriptors that should be applied after this one to maintain order.</param>
    /// <param name="LoadAfter">Descriptors that should be applied before this one to maintain order.</param>
    /// <param name="DependsOn">Descriptors that this descriptor explicitly depends on.</param>
    /// <param name="ConflictsWith">Descriptors with which this descriptor cannot coexist.</param>
    public record PluginPageDescriptor(
            Guid PluginID,
            Guid DescriptorID,
            string Version,

            Type ViewType,
            Type ViewModelType,
            string Name,
            string IconSource,
            Func<IServiceProvider, UserControl> ViewFactory,
            Func<IServiceProvider, BaseViewModel> ViewModelFactory,

            IEnumerable<PluginInterfaceReference>? LoadBefore = null,
            IEnumerable<PluginInterfaceReference>? LoadAfter = null,
            IEnumerable<PluginInterfaceReference>? DependsOn = null,
            IEnumerable<PluginInterfaceReference>? ConflictsWith = null) :
                PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that provide pages to the host application.
    /// Provides descriptors for pages the plugin contributes to the application.
    /// </summary>
    [DescriptorProvider("GetPageDescriptors", false)]
    public interface IPluginPages : IPlugin
    {
        /// <summary>
        /// Returns a collection of descriptors detailing the pages
        /// provided by this plugin.
        /// </summary>
        public IEnumerable<PluginPageDescriptor> GetPageDescriptors();
    }
}