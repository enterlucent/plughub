using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Interfaces.Services.Plugins;
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
            Func<IServiceProvider, UserControl>? ViewFactory = null,
            Func<IServiceProvider, BaseViewModel>? ViewModelFactory = null,

            IEnumerable<PluginDescriptorReference>? LoadBefore = null,
            IEnumerable<PluginDescriptorReference>? LoadAfter = null,
            IEnumerable<PluginDescriptorReference>? DependsOn = null,
            IEnumerable<PluginDescriptorReference>? ConflictsWith = null) : PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith)
    {
        /// <summary>
        /// Creates a <see cref="ContentItemViewModel"/> instance from the given <see cref="PluginPageDescriptor"/> using the provided <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="provider">The service provider used to resolve the view, view model, and any dependencies.</param>
        /// <param name="descriptor">The descriptor containing type and factory information for the page.</param>
        /// <returns>
        /// A fully constructed <see cref="ContentItemViewModel"/> with its <see cref="Controls.UserControl"/> and <see cref="BaseViewModel"/> instantiated and bound together, or <c>null</c> if either cannot be resolved.
        /// </returns>
        public static ContentItemViewModel? GetItemViewModel(IServiceProvider provider, PluginPageDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(descriptor);

            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginPages> pageProviders = provider.GetServices<IPluginPages>();

            IReadOnlyList<PluginPageDescriptor> orderedDescriptors =
                pluginResolver.ResolveAndOrder<IPluginPages, PluginPageDescriptor>(pageProviders);

            UserControl? view;
            BaseViewModel? viewModel;

            #region PluginsPages: Resolve View

            if (descriptor.ViewFactory != null)
            {
                view = descriptor.ViewFactory(provider);
            }
            else if (descriptor.ViewType != null)
            {
                view = provider.GetService(descriptor.ViewType) as UserControl;

                if (view is null) return null;
            }
            else return null;

            #endregion

            #region PluginPages: Resolve ViewModel

            if (descriptor.ViewModelFactory != null)
            {
                viewModel = descriptor.ViewModelFactory(provider);
            }
            else if (descriptor.ViewModelType != null)
            {
                viewModel = provider.GetService(descriptor.ViewModelType) as BaseViewModel;

                if (viewModel is null) return null;
            }
            else return null;

            #endregion

            ContentItemViewModel item = new(descriptor.ViewType!, descriptor.ViewModelType!, descriptor.Name, descriptor.IconSource)
            {
                Control = view,
                ViewModel = viewModel
            };
            view.DataContext = viewModel;

            return item;
        }
    }

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