using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.Logging;
using PlugHub.ViewModels;

namespace PlugHub.Views
{
    public partial class MainView : UserControl
    {
        protected readonly ILogger<MainView>? Logger;

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

        private void HeaderBorder_OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint((Visual)sender!).Properties.IsLeftButtonPressed)
                if (TopLevel.GetTopLevel(this) is Window w)
                    w.BeginMoveDrag(e);
        }
    }
}