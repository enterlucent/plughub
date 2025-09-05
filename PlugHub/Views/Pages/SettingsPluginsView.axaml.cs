using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Microsoft.Extensions.Logging;
using PlugHub.ViewModels.Pages;
using System.Linq;


namespace PlugHub.Views
{
    public partial class SettingsPluginsView : UserControl
    {
        protected ILogger<SettingsPluginsView>? Logger;

        public SettingsPluginsView()
        {
            this.InitializeComponent();
        }
        public SettingsPluginsView(ILogger<SettingsPluginsView> logger, SettingsPluginsViewModel settingsPluginsViewModel)
            : this()
        {
            this.Logger = logger;
            this.DataContext = settingsPluginsViewModel;
        }

        private void OnPluginClearButtonClicked(object sender, RoutedEventArgs e)
        {
            if (this.DataContext is SettingsPluginsViewModel vm)
            {
                vm.ClearAllPluginFilters();
                this.PluginSettingsCategoryListBox.UnselectAll();
            }
        }
        private void OnPluginCategorySelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (this.DataContext is SettingsPluginsViewModel vm && sender is ListBox listBox)
            {
                vm.SelectedCategories = [.. listBox.SelectedItems!.Cast<string>()];
                vm.ApplyPluginFilters();
            }
        }

        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                listBox.SelectedItem = null;
            }
        }

        private void UserControl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (this.DataContext is SettingsPluginsViewModel vm)
                {
                    bool handled = false;

                    if (vm.SelectedDescriptor != null)
                    {
                        vm.SelectedDescriptor = null;

                        handled = true;
                    }
                    else if (vm.IsPanelVisible)
                    {
                        vm.SelectedInterface = string.Empty;
                        vm.IsPanelVisible = false;

                        handled = true;
                    }

                    if (handled)
                    {
                        e.Handled = true;
                    }
                }
            }
        }
    }
}