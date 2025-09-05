using Avalonia.Controls;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.ViewModels;
using PlugHub.ViewModels;

namespace PlugHub.Views
{
    /// <summary>
    /// Represents the main view user control containing the application's primary navigation.
    /// </summary>
    public partial class MainView : UserControl
    {
        protected readonly ILogger<MainView>? Logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainView"/> class.
        /// </summary>
        public MainView()
        {
            this.InitializeComponent();
        }
        public MainView(ILogger<MainView> logger, MainViewModel mainViewModel)
            : this()
        {
            this.Logger = logger;
            this.DataContext = mainViewModel;
        }

        /// <summary>
        /// Handles selection changes in the NavigationView, updating the main or settings view model accordingly.
        /// </summary>
        /// <param name="sender">The source of the event (expected to be a <see cref="NavigationView"/>).</param>
        /// <param name="e">Event data for the navigation selection change.</param>
        private void OnNavigationView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (this.DataContext is MainViewModel vm)
            {
                if (e.IsSettingsSelected)
                    vm.OnSelectedSettingsViewModelChanged();
                else
                    vm.OnSelectedMainMenuViewModelChanged((ContentItemViewModel)e.SelectedItem);
            }
        }
    }
}