using CommunityToolkit.Mvvm.ComponentModel;

namespace PlugHub.Shared.ViewModels
{
    public partial class PluginDescriptorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private Type? type = null;

        [ObservableProperty]
        private bool isEnabled;

        [ObservableProperty]
        private bool isSystem;

        public event Action? IsEnabledChanged;

        public PluginDescriptorViewModel(string name, Type? type, bool isEnabled = false, bool isSystem = false)
        {
            this.Name = name;
            this.Type = type;
            this.IsEnabled = isEnabled;
            this.IsSystem = isSystem;
        }

        partial void OnIsEnabledChanged(bool value)
        {
            IsEnabledChanged?.Invoke();
        }
    }
}
