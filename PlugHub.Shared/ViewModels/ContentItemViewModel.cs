using Avalonia;
using Avalonia.Controls;

namespace PlugHub.Shared.ViewModels
{
    /// <summary>
    /// Represents a navigation item and its associated page for use in a NavigationView.
    /// </summary>
    public class ContentItemViewModel : BaseViewModel
    {
        /// <summary>
        /// Initializes a new navigation item and page descriptor.
        /// </summary>
        /// <param name="viewType">The type of the page view (UserControl).</param>
        /// <param name="viewModelType">The type of the page's view model.</param>
        /// <param name="label">The display label for the navigation item.</param>
        /// <param name="iconName">The resource key for the navigation icon.</param>
        public ContentItemViewModel(Type viewType, Type viewModelType, string label, string iconName)
        {
            #region PluginPageDescriptor: Resolve Icon

            object? iconData = null;

            if (!string.IsNullOrWhiteSpace(iconName))
            {
                if (iconName.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
                {
                    iconData = new Avalonia.Media.Imaging.Bitmap(iconName);
                }
                else
                {
                    Application? app = Application.Current;

                    if (app != null && app.TryFindResource(iconName, out object? resource))
                        iconData = resource;
                }
            }

            #endregion

            this.ViewType = viewType;
            this.ViewModelType = viewModelType;
            this.Label = label;
            this.Icon = iconData;
        }


        /// <summary>
        /// Gets the type of the page view (UserControl) to display when this navigation item is selected.
        /// </summary>
        public Type ViewType { get; }

        /// <summary>
        /// Gets the type of the view model associated with the page.
        /// </summary>
        public Type ViewModelType { get; }


        /// <summary>
        /// Gets the label to display in the navigation menu.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Gets the icon geometry for the navigation item.
        /// </summary>
        public object? Icon { get; }


        /// <summary>
        /// Gets or sets the instantiated control (page) for this navigation item.
        /// </summary>
        public Control? Control { get; set; }

        /// <summary>
        /// Gets or sets the instantiated view model for the page.
        /// </summary>
        public BaseViewModel? ViewModel { get; set; }
    }
}
