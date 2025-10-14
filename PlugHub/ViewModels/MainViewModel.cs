using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Events;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models;
using PlugHub.Shared.ViewModels;
using PlugHub.ViewModels.Pages;
using PlugHub.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PlugHub.ViewModels
{
    public partial class MainViewModel : BaseViewModel, IPageNavigationEvents
    {
        public event EventHandler<PageNavigationChangedEventArgs>? PageNavigationChanged;

        private readonly ILogger<MainViewModel> logger;
        private readonly IConfigService configService;
        private readonly IServiceProvider serviceProvider;

        private readonly ContentItemViewModel? mainPage;
        private readonly ContentItemViewModel? settingsPage;

        [ObservableProperty] private string appName = "PlugHub";
        [ObservableProperty] private IImage? appIcon = null;
        [ObservableProperty] private string appLink = string.Empty;

        [ObservableProperty] private Control? selectedMainPageContent;

        [ObservableProperty] private bool isMaximizeVisible = true;
        [ObservableProperty] private bool isRestoreVisible = false;
        [ObservableProperty] private bool isSettingsVisible = true;
        [ObservableProperty] private bool isHomeVisible = false;

        [ObservableProperty] private WindowIcon? windowIcon = null;
        [ObservableProperty] private string? windowTitle = null;
        [ObservableProperty] private int windowMinWidth = 0;
        [ObservableProperty] private int windowMinHeight = 0;

        public MainViewModel(ILogger<MainViewModel> logger, IServiceProvider serviceProvider, IConfigService configService, SettingsView settingsView, SettingsViewModel settingsViewModel, IEnumerable<IPluginPages> pages, IPluginResolver resolver)
        {
            this.logger = logger;
            this.configService = configService;
            this.serviceProvider = serviceProvider;

            IConfigAccessorFor<AppEnv> appEnv = configService.GetAccessor<AppEnv>();

            this.AppIcon = new Bitmap(AssetLoader.Open(new Uri("avares://PlugHub/Assets/avalonia-logo.ico")));
            this.AppName = appEnv.Get().AppName;
            this.AppLink = "https://enterlucent.com";

            this.WindowIcon = new WindowIcon(AssetLoader.Open(new Uri("avares://PlugHub/Assets/avalonia-logo.ico")));
            this.WindowTitle = this.AppName;
            this.WindowMinWidth = 640;
            this.WindowMinHeight = 480;

            this.settingsPage = new(typeof(SettingsView), typeof(SettingsViewModel), "", "")
            {
                Control = settingsView,
                ViewModel = settingsViewModel
            };

            IReadOnlyList<PluginPageDescriptor> descriptors =
                resolver.ResolveAndOrder<IPluginPages, PluginPageDescriptor>(pages);

            if (descriptors.Count > 0)
            {
                this.mainPage = PluginPageDescriptor.GetItemViewModel(serviceProvider, descriptors[0]);

                this.selectedMainPageContent = this.mainPage?.Control;
            }

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window w)
                this.OnWindowStateChanged(w.WindowState);
        }

        private void OnWindowStateChanged(WindowState state)
        {
            this.IsMaximizeVisible = state != WindowState.Maximized;
            this.IsRestoreVisible = state == WindowState.Maximized;
        }

        [RelayCommand]
        private void AppIconClicked()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = this.AppLink,
                UseShellExecute = true
            });
        }
        [RelayCommand]
        private void Minimize()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window w)
            {
                w.WindowState = WindowState.Minimized;

                this.OnWindowStateChanged(w.WindowState);
            }
        }
        [RelayCommand]
        private void MaximizeRestore()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is Window w)
            {
                w.WindowState = w.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;

                this.OnWindowStateChanged(w.WindowState);
            }
        }
        [RelayCommand]
        private static void Close()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                if (desktop.MainWindow is Window w)
                    w.Close();
        }

        [RelayCommand]
        private void GoHome()
        {
            var previous = this.IsHomeVisible ? this.mainPage : this.settingsPage;
            var next = this.mainPage;

            this.IsSettingsVisible = true;
            this.IsHomeVisible = false;

            this.SelectedMainPageContent = next?.Control;
            this.PageNavigationChanged?.Invoke(this, new PageNavigationChangedEventArgs(previous, next));
        }
        [RelayCommand]
        private void OpenSettings()
        {
            var previous = this.IsHomeVisible ? this.mainPage : this.settingsPage;
            var next = this.settingsPage;

            this.IsSettingsVisible = false;
            this.IsHomeVisible = true;

            this.SelectedMainPageContent = next?.Control;
            this.PageNavigationChanged?.Invoke(this, new PageNavigationChangedEventArgs(previous, next));
        }
    }
}