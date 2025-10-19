using Microsoft.Extensions.DependencyInjection;
using PlugHub.Plugin.Mock.Services;
using PlugHub.Plugin.Mock.ViewModels;
using PlugHub.Plugin.Mock.Views;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Mock.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration.Parameters;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Plugin.Mock
{
    /// <summary>
    /// Configuration model for the mock plugin demonstrating type-safe plugin configuration.
    /// </summary>
    public class PluginMockConfig
    {
        public int AnswerToEverything { get; init; } = 42;
    }

    /// <summary>
    /// Demonstration plugin showcasing PlugHub's multi-interface architecture.
    /// Implements branding (application identity override), configuration (token-secured configuration), and dependency injection (service provision to other plugins) simultaneously.
    /// </summary>
    public class PluginMock : PluginBase, IPluginDependencyInjection, IPluginAppConfig, IPluginAppEnv, IPluginConfiguration, IPluginResourceInclusion, IPluginStyleInclusion, IPluginPages, IPluginSettingsPages
    {
        private static Token owner;

        public PluginMock()
        {
            if (owner == Guid.Empty)
                owner = Token.New();
        }

        #region PluginMock: Key Fields

        public new static Guid PluginID { get; } = Guid.Parse("8e4c7077-e57a-46fc-b4fa-15ebba04ee65");
        public new static string IconSource { get; } = "avares://PlugHub.Plugin.Mock/Assets/ic_fluent_chat_24_regular.png";
        public new static string Name { get; } = "Plughub: Mock Service";
        public new static string Description { get; } = "A mock plugin that will test the plugin service features.";
        public new static string Version { get; } = "0.2.0";
        public new static string Author { get; } = "Enterlucent";
        public new static List<string> Categories { get; } =
        [
            "Services",
            "Configuration",
            "Pages",
        ];

        #endregion

        #region PluginMock: Metadata

        public new static List<string> Tags { get; } =
        [
            "Plugin:Mock",
            "EchoService",
        ];
        public new static string DocsLink { get; } = "https://github.com/enterlucent/plughub/wiki/";
        public new static string SupportLink { get; } = "https://support.enterlucent.com/plughub/";
        public new static string SupportContact { get; } = "contact@enterlucent.com";
        public new static string License { get; } = "GNU Lesser General Public License v3";
        public new static string ChangeLog { get; } = "https://github.com/enterlucent/plughub/releases/";

        #endregion

        #region PluginMock: IPluginDependencyInjector

        /// <summary>
        /// Provides IEchoService with handler-based extensibility - other plugins can implement 
        /// IEchoSuccessHandler or IEchoErrorHandler to extend service behavior.
        /// </summary>
        public IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors()
        {
            return [
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("34834c3e-313f-40b7-a8a1-ea021b74daa1"),
                    Version: Version,
                    InterfaceType: typeof(IEchoService),
                    ImplementationType: typeof(EchoService),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []),
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f3be5c56-1d3e-45a7-a104-487f8484d115"),
                    Version: Version,
                    InterfaceType: typeof(MockPageView),
                    ImplementationType: typeof(MockPageView),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []),
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("8ad501c2-8171-47cb-b861-535307cf6754"),
                    Version: Version,
                    InterfaceType: typeof(MockPageViewModel),
                    ImplementationType: typeof(MockPageViewModel),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []),
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("b89aa1d6-9415-4194-98ee-4dcb8f608757"),
                    Version: Version,
                    InterfaceType: typeof(MockSettingsView),
                    ImplementationType: typeof(MockSettingsView),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []),
            ];
        }

        #endregion

        #region PluginMock: IPluginAppConfig

        /// <summary>
        /// Demonstrates application configuration changes see: <see cref="AppConfig"/>.
        /// </summary>
        /// <remarks>
        /// Only applies during the **system-level load phase**
        /// </remarks>
        public IEnumerable<PluginAppConfigDescriptor> GetAppConfigDescriptors()
        {
            return [
                new PluginAppConfigDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f26d3290-c059-4641-8280-d229ee2c2c32"),
                    Version: Version,
                    AppConfig: liveAppConfig => {
                        liveAppConfig.LoggingDirectory =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Logging");
                        liveAppConfig.ConfigDirectory =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Config");
                        liveAppConfig.StorageDirectory =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Storage");
                    },
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: [])
            ];
        }

        #endregion

        #region PluginMock: IPluginAppEnv

        /// <summary>
        /// Demonstrates application environment changes see: <see cref="AppEnv"/>.
        /// </summary>
        public IEnumerable<PluginAppEnvDescriptor> GetAppEnvDescriptors()
        {
            return [
                new PluginAppEnvDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("ba543295-88e8-474a-b370-74594dcc76a8"),
                    Version: Version,
                    AppEnv: liveAppEnv => {
                        liveAppEnv.WindowTitle = "MockHub";
                        liveAppEnv.WindowIconPath = "avares://PlugHub.Plugin.Mock/Assets/alien-bob.png";
                        liveAppEnv.AppName = "MockHub";
                        liveAppEnv.AppIconPath = "avares://PlugHub.Plugin.Mock/Assets/alien-bob.png";
                    },
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: [])
            ];
        }

        #endregion

        #region PluginMock: IPluginConfiguration

        /// <summary>
        /// Demonstrates token-secured configuration where only the Owner token has write access, while others have read or no permissions.
        /// </summary>
        /// <remarks>
        /// The Owner token is created and cached in the plugin constructor for use in configuration access.
        /// </remarks>
        public IEnumerable<PluginConfigurationDescriptor> GetConfigurationDescriptors()
        {
            return [
                new PluginConfigurationDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("ddf7eb3a-6223-4016-bfb8-b4e0bba5a1c9"),
                    Version: Version,
                    ConfigType: typeof(PluginMockConfig),
                    ConfigServiceParams: ts => new ConfigFileParams(
                        Owner: owner,
                        Read: Token.Public,
                        Write: Token.Blocked),
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: [])
            ];
        }

        #endregion

        #region PluginMock: IPluginResourceInclusion

        /// <summary>
        /// Demonstrates how a plugin can contribute XAML resources (icons, themes, control templates) that are merged into the host application.
        /// This allows plugins to visually extend or customize the UI consistently.
        /// </summary>
        public IEnumerable<PluginResourceIncludeDescriptor> GetResourceIncludeDescriptors()
        {
            return [
                new PluginResourceIncludeDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f9e88050-b8c3-43b3-b76e-79f16595acff"),
                    Version: Version,
                    "avares://PlugHub.Plugin.Mock/Themes/FluentAvalonia/Theme.axaml",
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []
                ),
            ];
        }

        #endregion

        #region PluginMock: IPluginStyleInclusion

        /// <summary>
        /// Demonstrates how a plugin can contribute XAML styles that are merged into the host application.
        /// This allows plugins to visually extend or customize the UI consistently.
        /// </summary>
        public IEnumerable<PluginStyleIncludeDescriptor> GetStyleIncludeDescriptors()
        {
            return [
                new PluginStyleIncludeDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f9e88050-b8c3-43b3-b76e-79f16595acff"),
                    Version: Version,
                    "avares://PlugHub.Plugin.Mock/Themes/FluentAvalonia/Style.axaml",
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []
                ),
            ];
        }

        #endregion

        #region PluginMock: IPluginPages

        /// <summary>
        /// Describes a plugin-provided page.
        /// </summary>
        /// <remarks>
        /// - Plugin authors may provide explicit <see cref="ViewFactory"/> and <see cref="ViewModelFactory"/> delegates.  <br/>
        /// - If a factory is <c>null</c>, the Bootstrapper will attempt to resolve <see cref="ViewType"/> or <see cref="ViewModelType"/> from
        ///   the DI container.  <br/>
        /// - To support DI resolution, types must be registered via <see cref="PluginInjectorDescriptor"/>.  <br/>
        /// - If neither a factory nor a DI registration exists, the page will be skipped and an error logged.<br/>
        /// This dual-path model preserves backward compatibility while supporting DI-based resolution.<br/>
        /// </remarks>
        public IEnumerable<PluginPageDescriptor> GetPageDescriptors()
        {
            return [
                new PluginPageDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("f9e88050-b8c3-43b3-b76e-79f16595acff"),
                    Version: Version,
                    ViewType: typeof(MockPageView),
                    ViewModelType: typeof(MockPageViewModel),
                    Name: "Mock Page",
                    IconSource: "tab_desktop_new_page_regular",
                    ViewFactory: null,
                    ViewModelFactory: null!,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []
                )
            ];
        }

        #endregion

        #region PluginMock: IPluginSettingsPages

        /// <summary>
        /// Integrates plugin‑specific configuration views into the global settings experience of the host application. 
        /// This enables end users to edit and persist plugin configuration (e.g., <see cref="PluginMockConfig"/>) through a familiar settings UI rather than editing files manually.
        /// </summary>
        public List<SettingsPageDescriptor> GetSettingsPageDescriptors()
        {
            return [
                new SettingsPageDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("d9d8b10c-3bb2-42ca-9fd3-853f2d631e0d"),
                    Version: Version,
                    ViewType: typeof(MockSettingsView),
                    ViewModelType: typeof(MockSettingsViewModel),
                    Group: "Mock Settings",
                    Name: "General",
                    IconSource: "book_question_mark_regular",
                    ViewFactory: null,
                    ViewModelFactory: provider => {

                        IConfigService configService = provider.GetRequiredService<IConfigService>();
                        IConfigAccessorFor<PluginMockConfig> accessor = configService.GetAccessor<PluginMockConfig>(owner: owner);

                        return new MockSettingsViewModel(accessor);
                    },
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: []
                )
            ];
        }

        #endregion
    }
}