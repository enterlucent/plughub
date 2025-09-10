using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PlugHub.Plugin.Mock.ViewModels;

namespace PlugHub.Plugin.Mock.Views
{
    public partial class MockPageView : UserControl
    {
        public MockPageView()
        {
            this.InitializeComponent();
            this.InputTextBox.KeyDown += this.InputTextBox_KeyDown;
        }

        private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Shift) == 0)
            {
                if (this.DataContext is MockPageViewModel vm && vm.SendCommand.CanExecute(null))
                {
                    vm.SendCommand.Execute(null);
                    e.Handled = true;

                    this.ScrollToBottom();
                }
            }
        }

        private void ScrollToBottom()
        {
            Dispatcher.UIThread.Post(() =>
            {
                this.MessagesScrollViewer?.ScrollToEnd();
            }, DispatcherPriority.Background);
        }
    }
}
