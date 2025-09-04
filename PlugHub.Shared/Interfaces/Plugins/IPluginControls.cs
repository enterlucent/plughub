using Avalonia.Controls;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Models.Plugins;
using PlugHub.Shared.ViewModels;

namespace PlugHub.Shared.Interfaces.Plugins
{
    /// <summary>
    /// Describes a plugin-provided UI content/control, including its types, 
    /// display name, icon, and factory methods for instantiation.
    /// </summary>
    /// <param name="viewType">The concrete type of the Avalonia UserControl.</param>
    /// <param name="viewModelType">The type of the associated ViewModel.</param>
    /// <param name="name">Display name for this content/control.</param>
    /// <param name="iconSource">Resource key or path for the icon.</param>
    /// <param name="viewFactory">Factory function to create the UserControl instance.</param>
    /// <param name="viewModelFactory">Factory function to create the ViewModel instance.</param>
    public record ControlDescsriptor(
        Guid PluginID,
        Guid InterfaceID,
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
            PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Interface for plugins that provide UI content/controls to the host application.
    /// </summary>
    [DescriptorProvider("GetControlDescriptors", false)]
    public interface IPluginControls : IPlugin
    {
        /// <summary>
        /// Gets the list of content descriptions (controls) provided by this plugin.
        /// </summary>
        public List<ControlDescsriptor> GetControlDescriptors();
    }
}
