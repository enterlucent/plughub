using PlugHub.Shared.ViewModels;

namespace PlugHub.Shared.Interfaces.Events
{
    /// <summary>
    /// Provides data for a page navigation change event,
    /// containing the old page and the new page involved in the navigation.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="PageNavigationChangedEventArgs"/> class.
    /// </remarks>
    /// <param name="previousPageItem">The page before the change.</param>
    /// <param name="newPageItem">The page after the change.</param>
    public class PageNavigationChangedEventArgs(ContentItemViewModel? previousPageItem, ContentItemViewModel? newPageItem) : EventArgs
    {
        /// <summary>
        /// Gets the previous page before the navigation.
        /// </summary>
        public ContentItemViewModel? PreviousPageItem { get; } = previousPageItem;

        /// <summary>
        /// Gets the new page after the navigation.
        /// </summary>
        public ContentItemViewModel? NewPageItem { get; } = newPageItem;
    }

    /// <summary>
    /// Defines an interface that exposes an event for page navigation changes,
    /// providing old and new page information to subscribers.
    /// </summary>
    public interface IPageNavigationEvents
    {
        /// <summary>
        /// Occurs when the current page navigation changes.
        /// Subscribers get notified with the old and new page navigation data.
        /// </summary>
        public event EventHandler<PageNavigationChangedEventArgs>? PageNavigationChanged;
    }
}