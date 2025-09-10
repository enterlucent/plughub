using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.ViewModels;
using PlugHub.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.ViewModels.Pages
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private CancellationTokenSource? searchDebounceCts;
        private readonly ILogger<SettingsViewModel> logger;

        [ObservableProperty]
        private ObservableCollection<ContentItemGroupViewModel> settingsPageItems = [];

        [ObservableProperty]
        private ObservableCollection<ContentItemGroupViewModel> settingsPageItemSource = [];

        [ObservableProperty]
        private ObservableCollection<string> searchSuggestions = [];

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private ContentItemViewModel? selectedSettingsPageItem;

        [ObservableProperty]
        private Control? selectedSettingsPageContent;


        public SettingsViewModel(ILogger<SettingsViewModel> logger, SettingsPluginsView? settingsPluginsView, SettingsPluginsViewModel? settingPluginsViewModel)
        {
            this.logger = logger;

            this.AddPluginSettingsPage(settingsPluginsView, settingPluginsViewModel);

            this.UpdateSetting();
        }


        public void AddSettingsPage(string groupName, ContentItemViewModel item)
        {
            ContentItemGroupViewModel? group = this.SettingsPageItems.FirstOrDefault(g => g.GroupName == groupName);

            group ??= new ContentItemGroupViewModel
            {
                GroupName = groupName,
                Items = []
            };

            if (group.Items.Any(i => i.Label == item.Label))
            {
                this.logger.LogWarning("Item with label '{ItemLabel}' already exists in the group '{GroupName}'.", item.Label, groupName);

                return;
            }

            if (this.SettingsPageItems.FirstOrDefault(g => g.GroupName == groupName) == null)
                this.SettingsPageItems.Add(group);

            group.Items.Add(item);

            this.UpdateSetting();
        }
        private void AddPluginSettingsPage(SettingsPluginsView? settingsPluginsView, SettingsPluginsViewModel? settingPluginsViewModel)
        {
            if (settingsPluginsView == null)
            {
                this.logger.LogWarning("[SettingsViewModel] SettingsPluginsView is null. Plugin UI may not function correctly.");

                return;
            }

            if (settingPluginsViewModel == null)
            {
                this.logger.LogWarning("[SettingsViewModel] SettingsPluginsViewModel is null. Plugin UI may not function correctly.");

                return;
            }

            ContentItemViewModel item = new(typeof(SettingsPluginsView), typeof(SettingsPluginsViewModel), "Plugin Editor", "plug_disconnected_regular")
            {
                Control = settingsPluginsView,
                ViewModel = settingPluginsViewModel
            };

            this.SettingsPageItems.Add(new ContentItemGroupViewModel()
            {
                GroupName = "General Settings",
                Items = [item]
            });

            this.OnSelectedSettingsItemChanged(item);
        }


        public void OnSelectedSettingsItemChanged(ContentItemViewModel viewModel)
        {
            if (viewModel == null) return;

            this.SelectedSettingsPageItem = viewModel;
            this.SelectedSettingsPageContent = viewModel.Control ?? new TextBlock { Text = "Unable to find content" };
        }
        public void OnSettingsGroupPointerReleased(ContentItemGroupViewModel viewModel)
        {
            ContentItemGroupViewModel? groupViewModel = this.SettingsPageItems
                .OfType<ContentItemGroupViewModel>()
                .FirstOrDefault(group => group.GroupName == viewModel.GroupName);

            if (groupViewModel != null)
            {
                viewModel.IsCollapsed = !viewModel.IsCollapsed;

                groupViewModel.IsCollapsed = viewModel.IsCollapsed;
            }
        }


        partial void OnSearchTextChanged(string value)
        {
            DebouncedUpdateSettingsPageItemSource();
        }
        private async void DebouncedUpdateSettingsPageItemSource()
        {
            this.searchDebounceCts?.Cancel();
            this.searchDebounceCts = new CancellationTokenSource();
            CancellationToken token = this.searchDebounceCts.Token;

            try
            {
                await Task.Delay(300, token);

                if (!token.IsCancellationRequested)
                {
                    this.UpdateSettingsPageItemSource();
                }
            }
            catch (TaskCanceledException) { }
        }


        private void UpdateSetting()
        {
            this.UpdateSettingsPageItemSource();
            this.UpdateSearchSuggestions();
        }
        private void UpdateSettingsPageItemSource()
        {
            bool isSearchTextEmpty = string.IsNullOrEmpty(this.SearchText);

            List<ContentItemGroupViewModel> searchItems = [.. this.SettingsPageItems
                .Select(group =>
                {
                    if (!isSearchTextEmpty)
                    {
                        group.IsCollapsed = !group.Items.Any(item =>
                            item.Label.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ||
                            group.GroupName.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase)) && group.IsCollapsed;
                    }

                    return new ContentItemGroupViewModel
                    {
                        GroupName = group.GroupName,
                        IsCollapsed = group.IsCollapsed,
                        Items = isSearchTextEmpty
                            ? [.. group.Items]
                            : [.. group.Items.Where(item =>
                                    item.Label.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ||
                                    group.GroupName.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase))]
                    };
                })
                .Where(group => isSearchTextEmpty || !group.IsCollapsed)
                .Where(group => group.Items.Count > 0)];

            this.SettingsPageItemSource.Clear();

            foreach (ContentItemGroupViewModel group in searchItems)
                this.SettingsPageItemSource.Add(group);

            this.UpdateSearchSuggestions();
        }
        private void UpdateSearchSuggestions()
        {
            this.SearchSuggestions.Clear();

            foreach (string? suggestion in this.SettingsPageItems
                .SelectMany(group => group.Items)
                .Select(item => item.Label)
                .Distinct()
                .OrderBy(label => label)) this.SearchSuggestions.Add(suggestion);
        }
    }
}