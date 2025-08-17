using FluentAvalonia.UI.Windowing;

namespace PlugHub.Views.Windows
{
    /// <summary>
    /// Represents the main application window for PlugHub, with custom title bar and developer tools support.
    /// </summary>
    public partial class MainWindow : AppWindow
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// Configures the window to use a fully custom title bar by extending content into the title bar area
        /// and enabling complex hit testing for custom window controls. Also attaches Avalonia developer tools.
        /// </summary>
        public MainWindow()
        {
            this.TitleBar.ExtendsContentIntoTitleBar = true;
            this.TitleBar.TitleBarHitTestType = TitleBarHitTestType.Complex;

            this.InitializeComponent();
        }
        public MainWindow(MainView mainView)
            : this()
        {
            this.Content = mainView;
        }
    }
}