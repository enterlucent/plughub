using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlugHub.Shared.Mock.Interfaces.Services;
using PlugHub.Shared.ViewModels;
using System.Collections.ObjectModel;

namespace PlugHub.Plugin.Mock.ViewModels
{
    public class ChatMessageViewModel : ObservableObject
    {
        public string Sender { get; set; } = "";
        public string MessageText { get; set; } = "";

        public ChatMessageViewModel() { }
        public ChatMessageViewModel(string sender, string messageText)
        {
            this.Sender = sender;
            this.MessageText = messageText;
        }
    }

    public partial class MockPageViewModel(IEchoService? echoService) : BaseViewModel
    {
        private readonly IEchoService? echoService = echoService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendCommand))]
        [NotifyPropertyChangedFor(nameof(CanSend))]
        private string inputMessage = string.Empty;

        public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

        public bool IsEchoServiceAvailable => this.echoService != null;
        public bool IsEchoServiceUnavailable => this.echoService == null;
        public bool CanSend => this.IsEchoServiceAvailable && !string.IsNullOrWhiteSpace(this.InputMessage);

        [RelayCommand(CanExecute = nameof(CanSend))]
        private void Send()
        {
            if (string.IsNullOrWhiteSpace(this.InputMessage))
                return;

            this.Messages.Add(new ChatMessageViewModel("echo", this.echoService?.Echo(this.InputMessage) ?? "Service Error"));

            this.InputMessage = string.Empty;
        }
    }
}
