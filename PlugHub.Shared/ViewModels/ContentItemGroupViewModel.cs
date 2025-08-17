using CommunityToolkit.Mvvm.ComponentModel;
using PlugHub.Shared.ViewModels;
using System.Collections.ObjectModel;

namespace PlugHub.ViewModels
{
    /// <summary>
    /// Represents a group of content items with a name and collapsed state.
    /// </summary>
    public partial class ContentItemGroupViewModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        [ObservableProperty]
        private string groupName = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the group is collapsed.
        /// </summary>
        [ObservableProperty]
        private bool isCollapsed = false;

        /// <summary>
        /// Gets or sets the collection of content items in this group.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ContentItemViewModel> items = [];
    }
}
