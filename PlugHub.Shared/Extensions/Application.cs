using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;


namespace PlugHub.Shared.Extensions
{
    public static class ApplicationExtensions
    {
        /// <summary>
        /// Retrieves the main window of the application.
        /// </summary>
        /// <param name="app">
        /// The <see cref="Avalonia.Application"/> instance from which to retrieve the main window.
        /// </param>
        /// <returns>The main window of the application, if available; otherwise, <c>null</c>.</returns>
        /// <remarks>
        /// This method handles different application lifetime scenarios:
        /// <list type="bullet">
        /// <item><description>If the application uses <see cref="IClassicDesktopStyleApplicationLifetime"/>, it returns the <see cref="IClassicDesktopStyleApplicationLifetime.MainWindow"/>.</description></item>
        /// <item><description>If the application uses <see cref="ISingleViewApplicationLifetime"/>, it returns the <see cref="ISingleViewApplicationLifetime.MainView"/> cast to <see cref="Window"/>.</description></item>
        /// <item><description>If the application lifetime is not recognized, the method returns <c>null</c>.</description></item>
        /// </list>
        /// </remarks>
        public static Window? GetMainWindow(this Avalonia.Application app)
        {
            IApplicationLifetime? lifetime = app.ApplicationLifetime;

            if (lifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            else if (lifetime is ISingleViewApplicationLifetime singleView)
            {
                return singleView.MainView as Window;
            }

            return null;
        }

        /// <summary>
        /// Attempts to find a style resource with the specified key in the current application's styles.
        /// </summary>
        /// <param name="key">The resource key to search for.</param>
        /// <param name="resource">The found resource, if any; otherwise, <c>null</c>.</param>
        /// <returns>
        /// <c>true</c> if the resource was found; otherwise, <c>false</c>.
        /// </returns>
        public static bool TryFindStyleResource(this Avalonia.Application app, object key, out object? resource)
        {
            resource = null;
            if (app?.Styles == null)
                return false;

            foreach (IStyle style in app.Styles)
            {
                if (style is IResourceProvider rp && rp.TryGetResource(key, null, out resource))
                    return true;
            }
            return false;
        }
    }
}
