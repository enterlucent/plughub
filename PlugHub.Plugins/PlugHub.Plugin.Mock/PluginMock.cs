using Microsoft.Extensions.DependencyInjection;
using PlugHub.Plugin.Mock.Services;
using PlugHub.Shared;
using PlugHub.Shared.Interfaces;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Mock;
using PlugHub.Shared.Mock.Interfaces;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;


namespace PlugHub.Plugin.Mock
{
    public class PluginMockConfig
    {
        public int AnswerToEverything { get; init; } = 42;
    }

    public class PluginMock : PluginBase, ISharedMock, IPluginBranding, IPluginConfiguration, IPluginDependencyInjector
    {
        protected static ITokenSet? TokenSet { get; set; }

        #region PluginMock: Key Fields

        public new static Guid PluginID { get; } = Guid.Parse("25bd2f8d-7840-469f-9e57-af23e8ae0755");
        public new static string IconSource { get; } = "plugin.mock.svg";
        public new static string Name { get; } = "Plughub:Mock";
        public new static string Description { get; } = "A mock plugin that will test the usability to the plugin echo system.";
        public new static string Version { get; } = "1.0.0";
        public new static string Author { get; } = "Entercluent";
        public new static List<string> Categories { get; } =
        [
            "TestHarness",
            "Diagnostics",
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

        #region PluginMock: IPluginBranding

        public IEnumerable<PluginBrandingDescriptor> GetBrandingDescriptors()
        {
            return [
                new PluginBrandingDescriptor(
                    PluginID: PluginID,
                    InterfaceID: Guid.Parse("f26d3290-c058-4641-8280-d229ee2c2c62"),
                    Version: Version,
                    BrandConfiguration: (IConfigAccessorFor<AppConfig> appConfig) => {
                        AppConfig liveAppConfig = appConfig.Get();

                        liveAppConfig.AppName = "👽 MockHub 👽";
                        liveAppConfig.LoggingDirectory =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Logging");
                        liveAppConfig.ConfigDirectory =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Config");
                        liveAppConfig.StorageFolderPath =
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MockHub", "Storage");

                        appConfig.Save(liveAppConfig);
                    },
                    BrandServices: (IServiceProvider provider) => {
                        // access services for configuration needs: includes registered views and viewmodels 
                    },
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: [])
            ];
        }

        #endregion

        #region PluginMock: IPluginConfiguration

        public IEnumerable<PluginConfigurationDescriptor> GetConfigurationDescriptors(ITokenService tokenService)
        {
            TokenSet ??=
                tokenService.CreateTokenSet(
                    Token.New(),
                    Token.Public,
                    Token.Blocked);

            return [
                new PluginConfigurationDescriptor(
                    PluginID: PluginID,
                    InterfaceID: Guid.Parse("dde7eb3a-6223-4016-bfb8-b3e0bba5a1c9"),
                    Version: Version,
                    ConfigType: typeof(PluginMockConfig),
                    ConfigServiceParams:
                        new FileConfigServiceParams(
                            Owner: TokenSet.Owner,
                            Read:TokenSet.Read,
                            Write:TokenSet.Write),
                    LoadBefore: [],
                    LoadAfter: [],
                    ConflictsWith: [],
                    DependsOn: [])
            ];
        }

        #endregion

        #region PluginMock: IPluginDependencyInjector

        public IEnumerable<PluginInjectorDescriptor> GetInjectionDescriptors()
        {
            return [
                new PluginInjectorDescriptor(
                    PluginID: PluginID,
                    InterfaceID: Guid.Parse("34834c2e-313f-40b7-a8a1-ea021b74daa1"),
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
    }
}