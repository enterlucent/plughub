using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Events;
using PlugHub.Shared.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;


namespace PlugHub.ViewModels
{
    public partial class MainViewModel : BaseViewModel, IPageNavigationEvents
    {
        public event EventHandler<PageNavigationChangedEventArgs>? PageNavigationChanged;


        private readonly ILogger<MainViewModel> logger;


        [ObservableProperty]
        private bool isPaneOpen = false;

        [ObservableProperty]
        private bool isModalOverlayVisible = false;

        [ObservableProperty]
        private ContentItemViewModel? selectedMainPageItem;

        [ObservableProperty]
        private Control? selectedMainPageContent;


        private ObservableCollection<ContentItemViewModel> MainPageItems { get; } = [];
        public ReadOnlyObservableCollection<ContentItemViewModel> MainPageItemSource { get; }


        public MainViewModel(ILogger<MainViewModel> logger)
        {
            this.logger = logger;

            this.MainPageItemSource =
                new ReadOnlyObservableCollection<ContentItemViewModel>(this.MainPageItems);
        }

        public void AddMainPageItem(ContentItemViewModel mainPageItem)
        {
            this.MainPageItems.Add(mainPageItem);
        }


        public void OnSelectedMainMenuViewModelChanged(ContentItemViewModel? mainPageItem)
        {
            if (mainPageItem == null) return;

            ContentItemViewModel? previousPageItem = this.SelectedMainPageItem;

            this.SelectedMainPageContent = mainPageItem.Control ?? new TextBlock { Text = "Unable to find content" };

            PageNavigationChanged?.Invoke(this, new PageNavigationChangedEventArgs(previousPageItem, mainPageItem));
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