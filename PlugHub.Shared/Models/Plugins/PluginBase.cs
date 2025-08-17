namespace PlugHub.Shared.Models.Plugins
{
    /// <summary>
    /// Provides a base class for all plugins in a PlugHub-based application.
    /// <para>
    /// <b>Required:</b> All plugins must override the static properties in the "Key Fields" region
    /// (ID, Name, etc.) using <c>static</c> and <c>new</c>.
    /// This allows the host to gather plugin metadata via reflection <b>without instantiating the plugin class</b>.
    /// </para>
    /// <para>
    /// <b>Optional:</b> All other metadata properties (such as DocsLink, SupportLink, Tags, License, etc.) and
    /// dependency management properties are optional, but encouraged for richer plugin discovery and management.
    /// </para>
    /// <para>
    /// <b>Example:</b>
    /// <code>
    /// public class MyPlugin : PluginBase
    /// {
    ///     public new static Guid ID { get; } = new Guid("...");
    ///     public new static string Name { get; } = "My Plugin";
    ///     // ...etc
    /// }
    /// </code>
    /// </para>
    /// </summary>
    public abstract class PluginBase
    {
        #region PluginBase: Key Fields

        /// <summary>
        /// Unique identifier for the plugin. This ID should be unique across all plugins. (e.g., GUID)
        /// This ID is used in <see cref="Dependencies"/> and <see cref="Conflicts"/> which require a <see cref="PluginInterfaceReference"/> 
        /// including both the ID and version range, as well as in <see cref="LoadBefore"/> and <see cref="LoadAfter"/> which only use the ID.
        /// </summary>
        public static Guid PluginID { get; } = Guid.Empty;

        /// <summary>
        /// Path, URI, or resource key for the plugin icon.
        /// 
        /// - If the value starts with the "avares://" prefix, it is treated as an Avalonia resource URI (e.g., "avares://YourApp/Assets/plugin-icon.png").
        /// - If the value is a standard URI (e.g., "http://..." or "https://..."), it is used as a remote image source.
        /// - If the value does not have the "avares://" prefix and is not a URI, it is treated as a resource key in the application's resources.
        /// - If null or empty, the default icon will be used.
        /// </summary>
        public static string IconSource { get; } = string.Empty;

        /// <summary>
        /// Name of the plugin.
        /// </summary>
        public static string Name { get; } = string.Empty;

        /// <summary>
        /// Description of the plugin.
        /// </summary>
        public static string Description { get; } = string.Empty;

        /// <summary>
        /// Version of the plugin.
        /// </summary>
        public static string Version { get; } = "0.0.0";

        /// <summary>
        /// Author of the plugin.
        /// </summary>
        public static string Author { get; } = string.Empty;

        /// <summary>
        /// List of category names for this plugin. Used for grouping and filtering plugins in the UI.
        /// Plugins can belong to multiple categories. Default: ["Uncategorized"].
        /// </summary>
        public static List<string> Categories { get; } = [];

        #endregion

        #region PluginBase: Metadata

        /// <summary>
        /// Tags associated with the plugin. Tags can be used for additional filtering or categorization.
        /// </summary>
        public static List<string> Tags { get; } = [];

        /// <summary>
        /// URL to the documentation for the plugin.
        /// </summary>
        public static string DocsLink { get; } = string.Empty;

        /// <summary>
        /// URL to the support page for the plugin.
        /// </summary>
        public static string SupportLink { get; } = string.Empty;

        /// <summary>
        /// Contact information for support related to the plugin.
        /// </summary>
        public static string SupportContact { get; } = string.Empty;

        /// <summary>
        /// License information for the plugin.
        /// </summary>
        public static string License { get; } = string.Empty;

        /// <summary>
        /// URL to the change log for the plugin.
        /// </summary>
        public static string ChangeLog { get; } = string.Empty;

        #endregion
    }
}