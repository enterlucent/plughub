using CommunityToolkit.Mvvm.ComponentModel;

namespace PlugHub.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Welcome to Avalonia!";
}
