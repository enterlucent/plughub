using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.ViewModels;
using PlugHub.Views;
using Serilog;
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

        [ObservableProperty] private ObservableCollection<ContentItemGroupViewModel> settingsPageItems = [];
        [ObservableProperty] private ObservableCollection<ContentItemGroupViewModel> settingsPageItemSource = [];
        [ObservableProperty] private ObservableCollection<string> searchSuggestions = [];
        [ObservableProperty] private string searchText = string.Empty;
        [ObservableProperty] private ContentItemViewModel? selectedSettingsPageItem;
        [ObservableProperty] private Control? selectedSettingsPageContent;

        public SettingsViewModel(ILogger<SettingsViewModel> logger, SettingsPluginsView? settingsPluginsView, SettingsPluginsViewModel? settingPluginsViewModel, IServiceProvider provider, IPluginResolver pluginResolver, IEnumerable<IPluginSettingsPages> settingsProviders)
        {
            this.logger = logger;

            this.AddPluginSettingsPage(settingsPluginsView, settingPluginsViewModel);

            IReadOnlyList<SettingsPageDescriptor> orderedDescriptors =
                pluginResolver.ResolveAndOrder<IPluginSettingsPages, SettingsPageDescriptor>(settingsProviders);

            foreach (SettingsPageDescriptor descriptor in orderedDescriptors)
            {
                ContentItemViewModel? page = SettingsPageDescriptor.GetItemViewModel(provider, descriptor);

                if (page == null)
                {
                    Log.Error("[SettingsViewModel] Could not resolve settings page {PageName}, skipping.", descriptor.Name);

                    continue;
                }

                this.AddSettingsPage(descriptor.Group, page);
            }

            Log.Information("[SettingsViewModel] PluginsSettingPages completed: added {SettingsPageCount} plugin-provided settings pages, grouped appropriately.", orderedDescriptors.Count);

            this.UpdateSetting();
        }

        public void AddSettingsPage(string groupName, ContentItemViewModel item)
        {
            ContentItemGroupViewModel? group = this.SettingsPageItems.FirstOrDefault(g => g.GroupName == groupName);

            if (group == null)
            {
                group = new ContentItemGroupViewModel
                {
                    GroupName = groupName,
                    Items = []
                };
                this.SettingsPageItems.Add(group);
            }

            if (group.Items.Any(i => i.Label == item.Label))
            {
                this.logger.LogWarning("Item with label '{ItemLabel}' already exists in the group '{GroupName}'.", item.Label, groupName);
                return;
            }

            group.Items.Add(item);
            this.UpdateSetting();
        }
        private void AddPluginSettingsPage(SettingsPluginsView? settingsPluginsView, SettingsPluginsViewModel? settingPluginsViewModel)
        {
            if (settingsPluginsView == null || settingPluginsViewModel == null)
            {
                this.logger.LogWarning("[SettingsViewModel] Plugin settings view or viewmodel is null. Plugin UI may not function correctly.");
                return;
            }

            ContentItemViewModel item = new(typeof(SettingsPluginsView), typeof(SettingsPluginsViewModel), "Plugin Editor", "plug_disconnected_regular")
            {
                Control = settingsPluginsView,
                ViewModel = settingPluginsViewModel
            };

            ContentItemGroupViewModel group = new()
            {
                GroupName = "General Settings",
                Items = [item]
            };

            this.SettingsPageItems.Add(group);
            this.OnSelectedSettingsItemChanged(item);
        }

        public void OnSelectedSettingsItemChanged(ContentItemViewModel viewModel)
        {
            if (viewModel == null) return;

            this.SelectedSettingsPageItem = viewModel;
            this.SelectedSettingsPageContent = viewModel.Control ?? new TextBlock { Text = "Unable to find content" };

            // Option B: MVVM‑pure
            //this.SelectedSettingsPageKey = viewModel.TemplateKey;
        }
        public void OnSettingsGroupPointerReleased(ContentItemGroupViewModel viewModel)
        {
            ContentItemGroupViewModel? original = this.SettingsPageItems
                .FirstOrDefault(g => g.GroupName == viewModel.GroupName);

            if (original == null) return;

            original.IsCollapsed = !original.IsCollapsed;

            this.UpdateSetting();
        }
        partial void OnSearchTextChanged(string value)
        {
            _ = DebouncedUpdateSettingsPageItemSource();
        }

        private async Task DebouncedUpdateSettingsPageItemSource()
        {
            this.searchDebounceCts?.Cancel();
            this.searchDebounceCts = new CancellationTokenSource();
            CancellationToken token = this.searchDebounceCts.Token;

            try
            {
                await Task.Delay(300, token);
                if (!token.IsCancellationRequested)
                    this.UpdateSetting();
            }
            catch (TaskCanceledException) { /* swallow */ }
        }

        private void UpdateSetting()
        {
            this.UpdateSettingsPageItemSource();
            this.UpdateSearchSuggestions();
        }
        private void UpdateSettingsPageItemSource()
        {
            bool isSearchTextEmpty = string.IsNullOrEmpty(this.SearchText);

            var filteredGroups = this.SettingsPageItems
                .Select(group =>
                {
                    IEnumerable<ContentItemViewModel> matchingItems = isSearchTextEmpty
                        ? group.Items
                        : group.Items.Where(item =>
                            item.Label.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase) ||
                            group.GroupName.Contains(this.SearchText, StringComparison.OrdinalIgnoreCase));

                    return new { Group = group, Items = matchingItems.ToList() };
                })
                // Keep headers even when collapsed; the ListBox height will be 0 when IsCollapsed is true
                .Where(x => x.Items.Count > 0);

            this.SettingsPageItemSource.Clear();

            foreach (var entry in filteredGroups)
            {
                this.SettingsPageItemSource.Add(new ContentItemGroupViewModel
                {
                    GroupName = entry.Group.GroupName,
                    IsCollapsed = entry.Group.IsCollapsed,
                    Items = new ObservableCollection<ContentItemViewModel>(entry.Items)
                });
            }
        }
        private void UpdateSearchSuggestions()
        {
            this.SearchSuggestions.Clear();

            foreach (string suggestion in this.SettingsPageItems
                .SelectMany(group => group.Items)
                .Select(item => item.Label)
                .Distinct()
                .OrderBy(label => label))
            {
                this.SearchSuggestions.Add(suggestion);
            }
        }
    }
}