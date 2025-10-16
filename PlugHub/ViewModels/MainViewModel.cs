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

        [ObservableProperty] private Control? selectedMainPageContent;

        [ObservableProperty] private bool isMaximizeVisible = true;
        [ObservableProperty] private bool isRestoreVisible = false;
        [ObservableProperty] private bool isSettingsVisible = true;
        [ObservableProperty] private bool isHomeVisible = false;

        [ObservableProperty] private WindowIcon? windowIcon;
        [ObservableProperty] private string? windowTitle;
        [ObservableProperty] private double? windowMinWidth;
        [ObservableProperty] private double? windowMinHeight;
        [ObservableProperty] private double? windowWidth;
        [ObservableProperty] private double? windowHeight;
        [ObservableProperty] private WindowState? windowStartupState;
        [ObservableProperty] private WindowStartupLocation? windowStartupLocation;
        [ObservableProperty] private WindowTransparencyLevel? transparencyPreference;
        [ObservableProperty] private bool? canResize;
        [ObservableProperty] private bool? showInTaskbar;
        [ObservableProperty] private SystemDecorations? systemDecorations;
        [ObservableProperty] private bool? extendClientAreaToDecorationsHint;
        [ObservableProperty] private int? extendClientAreaTitleBarHeightHint;

        [ObservableProperty] private string? appName;
        [ObservableProperty] private IImage? appIcon;
        [ObservableProperty] private string? appLink;

        public MainViewModel(ILogger<MainViewModel> logger, IConfigService configService, IServiceProvider serviceProvider, SettingsView settingsView, SettingsViewModel settingsViewModel, IPluginResolver resolver, IEnumerable<IPluginPages> pages)
        {
            this.logger = logger;
            this.configService = configService;
            this.serviceProvider = serviceProvider;

            IConfigAccessorFor<AppConfig> appConfig = configService.GetAccessor<AppConfig>();
            IConfigAccessorFor<AppEnv> appEnv = configService.GetAccessor<AppEnv>();

            this.ApplyConfigValues(appConfig.Get(), appEnv.Get());

            this.settingsPage = new(typeof(SettingsView), typeof(SettingsViewModel), "", null!)
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
        private void ApplyConfigValues(AppConfig appConfig, AppEnv appEnv)
        {
            string iconPath = !string.IsNullOrWhiteSpace(appEnv.WindowIconPath) ? appEnv.WindowIconPath : appConfig.WindowIconPath;

            if (!string.IsNullOrWhiteSpace(iconPath))
                this.WindowIcon = new WindowIcon(AssetLoader.Open(new Uri(iconPath)));

            this.WindowTitle = !string.IsNullOrWhiteSpace(appEnv.WindowTitle) ? appEnv.WindowTitle : appConfig.WindowTitle;
            this.WindowMinWidth = appEnv.WindowMinWidth ?? appConfig.WindowMinWidth;
            this.WindowMinHeight = appEnv.WindowMinHeight ?? appConfig.WindowMinHeight;
            this.WindowWidth = appEnv.WindowWidth ?? appConfig.WindowWidth;
            this.WindowHeight = appEnv.WindowHeight ?? appConfig.WindowHeight;

            this.WindowStartupState = appEnv.WindowStartupState ?? appConfig.WindowStartupState;
            this.WindowStartupLocation = appEnv.WindowStartupLocation ?? appConfig.WindowStartupLocation;
            this.TransparencyPreference = appEnv.TransparencyPreference ?? appConfig.TransparencyPreference;
            this.SystemDecorations = appEnv.SystemDecorations ?? appConfig.SystemDecorations;

            this.CanResize = appEnv.CanResize ?? appConfig.CanResize;
            this.ShowInTaskbar = appEnv.ShowInTaskbar ?? appConfig.ShowInTaskbar;
            this.ExtendClientAreaToDecorationsHint = appEnv.ExtendClientAreaToDecorationsHint ?? appConfig.ExtendClientAreaToDecorationsHint;
            this.ExtendClientAreaTitleBarHeightHint = appEnv.ExtendClientAreaTitleBarHeightHint ?? appConfig.ExtendClientAreaTitleBarHeightHint;

            string appIconPath = !string.IsNullOrWhiteSpace(appEnv.AppIconPath) ? appEnv.AppIconPath : appConfig.AppIconPath;

            if (!string.IsNullOrWhiteSpace(appIconPath))
                this.AppIcon = new Bitmap(AssetLoader.Open(new Uri(appIconPath)));

            this.AppName = !string.IsNullOrWhiteSpace(appEnv.AppName) ? appEnv.AppName : appConfig.AppName;
            this.AppLink = !string.IsNullOrWhiteSpace(appEnv.AppLink) ? appEnv.AppLink : appConfig.AppLink;
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
            ContentItemViewModel? previous = this.IsHomeVisible ? this.mainPage : this.settingsPage;
            ContentItemViewModel? next = this.mainPage;

            this.IsSettingsVisible = true;
            this.IsHomeVisible = false;

            this.SelectedMainPageContent = next?.Control;
            this.PageNavigationChanged?.Invoke(this, new PageNavigationChangedEventArgs(previous, next));
        }
        [RelayCommand]
        private void OpenSettings()
        {
            ContentItemViewModel? previous = this.IsHomeVisible ? this.mainPage : this.settingsPage;
            ContentItemViewModel? next = this.settingsPage;

            this.IsSettingsVisible = false;
            this.IsHomeVisible = true;

            this.SelectedMainPageContent = next?.Control;
            this.PageNavigationChanged?.Invoke(this, new PageNavigationChangedEventArgs(previous, next));
        }
    }
}