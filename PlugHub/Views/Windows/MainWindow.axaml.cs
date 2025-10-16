using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models;
using PlugHub.Shared.ViewModels;
using PlugHub.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Views.Windows
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }
        public MainWindow(IServiceProvider serviceProvider, IConfigService configService)
            : this()
        {
            IPluginResolver pluginResolver = serviceProvider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginMainView> viewProviders = serviceProvider.GetServices<IPluginMainView>();
            IReadOnlyList<PluginMainViewDescriptor> orderedDescriptors = pluginResolver.ResolveAndOrder<IPluginMainView, PluginMainViewDescriptor>(viewProviders);

            IConfigAccessorFor<AppConfig> appConfig = configService.GetAccessor<AppConfig>();
            IConfigAccessorFor<AppEnv> appEnv = configService.GetAccessor<AppEnv>();

            string? mainViewKey = string.IsNullOrWhiteSpace(appConfig.Get().MainViewKey)
                ? appEnv.Get().MainViewKey
                : appConfig.Get().MainViewKey;

            PluginMainViewDescriptor? found = orderedDescriptors
                .FirstOrDefault(d => string.Equals($"{d.ViewType.FullName}:{d.Key}", mainViewKey, StringComparison.OrdinalIgnoreCase));

            UserControl view;
            BaseViewModel viewModel;

            if (found != null)
            {
                view = found.ViewFactory != null
                    ? found.ViewFactory(serviceProvider)
                    : serviceProvider.GetService(found.ViewType) as UserControl
                      ?? (UserControl)ActivatorUtilities.CreateInstance(serviceProvider, found.ViewType);

                viewModel = found.ViewModelFactory != null
                    ? found.ViewModelFactory(serviceProvider)
                    : serviceProvider.GetService(found.ViewModelType) as BaseViewModel
                      ?? (BaseViewModel)ActivatorUtilities.CreateInstance(serviceProvider, found.ViewModelType);
            }
            else
            {
                view = serviceProvider.GetRequiredService<MainView>();
                viewModel = serviceProvider.GetRequiredService<MainViewModel>();
            }

            this.Content = view;
            this.DataContext = viewModel;
        }
    }
}