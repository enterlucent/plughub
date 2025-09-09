using CommunityToolkit.Mvvm.ComponentModel;

namespace PlugHub.Shared.ViewModels
{
    /// <summary>
    /// Base class for all view models in PlugHub, providing property change notification.
    /// </summary>
    public abstract partial class BaseViewModel : ObservableValidator
    {
    }
}
