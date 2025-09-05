using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.ViewModels;
using PlugHub.ViewModels;
using PlugHub.ViewModels.Pages;
using System.Linq;


namespace PlugHub.Views
{
    public partial class SettingsView : UserControl
    {
        protected readonly ILogger<SettingsView>? Logger;

        public SettingsView()
        {
            this.InitializeComponent();
        }
        public SettingsView(ILogger<SettingsView> logger, SettingsViewModel settingViewModel)
            : this()
        {
            this.Logger = logger;
            this.DataContext = settingViewModel;
        }

        private void ListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                if (listBox.SelectedItem is ContentItemViewModel selected)
                {
                    if (this.DataContext is SettingsViewModel viewModel)
                    {
                        viewModel.OnSelectedSettingsItemChanged(selected);
                    }
                }
            }
        }
        private void TextBlock_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                ItemsControl? itemsControl = textBlock.FindAncestorOfType<ItemsControl>();

                if (itemsControl?.DataContext is SettingsViewModel settingsViewModel)
                {
                    ContentItemGroupViewModel? groupViewModel = itemsControl.Items
                        .OfType<ContentItemGroupViewModel>()
                        .FirstOrDefault(group => group.GroupName == textBlock.Text);

                    if (groupViewModel != null)
                        settingsViewModel.OnSettingsGroupPointerReleased(groupViewModel);
                }
            }
        }
    }
}