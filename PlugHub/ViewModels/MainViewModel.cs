using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Events;
using PlugHub.Shared.ViewModels;
using PlugHub.ViewModels.Pages;
using PlugHub.Views;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;


namespace PlugHub.ViewModels
{
    public partial class MainViewModel : BaseViewModel, IPageNavigationEvents
    {
        public event EventHandler<PageNavigationChangedEventArgs>? PageNavigationChanged;

        private readonly ILogger<MainViewModel> logger;
        private readonly ContentItemViewModel? settingsPage;


        [ObservableProperty]
        private bool isPaneOpen = false;

        [ObservableProperty]
        private bool isModalOverlayVisible = false;

        [ObservableProperty]
        private ContentItemViewModel? selectedMainPageItem;

        [ObservableProperty]
        private Control? selectedMainPageContent;

        [ObservableProperty]
        private ObservableCollection<ContentItemViewModel> mainPageItems;


        public MainViewModel(ILogger<MainViewModel> logger, SettingsView settingsView, SettingsViewModel settingsViewModel)
        {
            this.logger = logger;
            this.settingsPage = new(typeof(SettingsView), typeof(SettingsViewModel), "", "")
            {
                Control = settingsView,
                ViewModel = settingsViewModel
            };
            this.mainPageItems = [];

            if (this.MainPageItems.Count > 0)
                this.OnSelectedMainMenuViewModelChanged(this.MainPageItems[0]);
        }

        public void AddMainPageItem(ContentItemViewModel mainPageItem)
        {
            this.MainPageItems.Add(mainPageItem);

            if (this.MainPageItems.Count == 1)
                this.OnSelectedMainMenuViewModelChanged(this.MainPageItems[0]);
        }

        public void OnSelectedMainMenuViewModelChanged(ContentItemViewModel? mainPageItem)
        {
            if (mainPageItem == null) return;

            ContentItemViewModel? previousPageItem = this.SelectedMainPageItem;

            this.SelectedMainPageContent = mainPageItem.Control ?? new TextBlock { Text = "Unable to find content" };
            this.SelectedMainPageItem = mainPageItem;

            PageNavigationChanged?.Invoke(this, new PageNavigationChangedEventArgs(previousPageItem, mainPageItem));
        }
        public void OnSelectedSettingsViewModelChanged()
        {
            this.SelectedMainPageContent = this.settingsPage?.Control ?? new TextBlock { Text = "Unable to find content" };
        }

        [RelayCommand]
        private static void AppIconClicked()
        {
            //TODO: Get from Branding AppConfig
            string url = "https://enterlucent.com";

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
    }
}