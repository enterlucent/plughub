using Microsoft.Extensions.DependencyInjection;
using PlugHub.Plugin.Mock.Services;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Mock.Interfaces;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
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
    /// Implements branding (application identity override), configuration (token-secured configuration), 
    /// and dependency injection (service provision to other plugins) simultaneously.
    /// </summary>
    public class PluginMock : PluginBase, IPluginDependencyInjection, IPluginAppConfig, IPluginConfiguration
    {
        #region PluginMock: Key Fields

        public new static Guid PluginID { get; } = Guid.Parse("8e4c7077-e57a-46fc-b4fa-15ebba04ee65");
        public new static string IconSource { get; } = "avares://PlugHub.Plugin.Mock/Assets/ic_fluent_chat_24_regular.png";
        public new static string Name { get; } = "Plughub: Mock Service";
        public new static string Description { get; } = "A mock plugin that will test the plugin service features.";
        public new static string Version { get; } = "0.0.1";
        public new static string Author { get; } = "Enterlucent";
        public new static List<string> Categories { get; } =
        [
            "TestHarness",
            "Diagnostics",
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
                    InterfaceID: Guid.Parse("34834c3e-313f-40b7-a8a1-ea021b74daa1"),
                    Version: Version,
                    InterfaceType: typeof(IEchoService),
                    ImplementationType: typeof(EchoService),
                    Lifetime: ServiceLifetime.Singleton,
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: [])
            ];
        }

        #endregion

        #region PluginMock: IPluginAppConfig

        /// <summary>
        /// Demonstrates complete application rebranding - transforms PlugHub into "MockHub" 
        /// with custom paths and identity.
        /// </summary>
        public IEnumerable<PluginAppConfigDescriptor> GetAppConfigDescriptors()
        {
            return [
                new PluginAppConfigDescriptor(
                    PluginID: PluginID,
                    InterfaceID: Guid.Parse("f26d3290-c059-4641-8280-d229ee2c2c32"),
                    Version: Version,
                    AppConfiguration: (AppConfig liveAppConfig) => {
                        liveAppConfig.AppName = "👽 MockHub 👽";
                        liveAppConfig.LoggingDirectory =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Logging");
                        liveAppConfig.ConfigDirectory =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Config");
                        liveAppConfig.StorageFolderPath =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Storage");
                    },
                    AppServices: (IServiceProvider provider) => {
                        // Access services for branding needs: includes registered views and viewmodels 
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
        /// Demonstrates token-secured configuration management with Owner/Read/Write permissions.
        /// </summary>
        public IEnumerable<PluginConfigurationDescriptor> GetConfigurationDescriptors()
        {
            Token owner = Token.New();

            return [
                new PluginConfigurationDescriptor(
                    PluginID: PluginID,
                    DescriptorID: Guid.Parse("ddf7eb3a-6223-4016-bfb8-b4e0bba5a1c9"),
                    Version: Version,
                    ConfigType: typeof(PluginMockConfig),
                    ConfigServiceParams: ts => new FileConfigServiceParams(
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
    }
}