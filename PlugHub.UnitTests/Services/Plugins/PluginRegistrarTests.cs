using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Accessors.Configuration;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Services.Configuration.Providers;
using PlugHub.Services.Plugins;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Models.Plugins;
using System.Collections.Concurrent;
using System.Reflection;


namespace PlugHub.UnitTests.Services.Plugins
{
    [TestClass]
    public sealed class PluginRegistrarTests
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private TokenService? tokenService;
        private ConfigService? configService;
        private FileConfigServiceParams fileParams = new();
        private IConfigAccessorFor<PluginManifest>? manifest;
        private PluginRegistrar? pluginRegistrar;
        private PluginService? pluginService;
        private PluginCache? pluginCache;

        private Token ownerToken;
        private Token readToken;
        private Token writeToken;

        private readonly Assembly enabledAssembly = typeof(List<>).Assembly;
        private readonly Type enabledImplementationType = typeof(List<string>);
        private readonly Type enabledInterfaceType = typeof(IList<string>);
        private readonly Assembly disabledAssembly = typeof(ConcurrentStack<>).Assembly;
        private readonly Type disabledImplementationType = typeof(Dictionary<int, string>);
        private readonly Type disabledInterfaceType = typeof(IDictionary<int, string>);

        [TestInitialize]
        public async Task Setup()
        {
            this.tokenService = new TokenService(new NullLogger<ITokenService>());

            this.ownerToken = this.tokenService.CreateToken();
            this.readToken = this.tokenService.CreateToken();
            this.writeToken = this.tokenService.CreateToken();

            this.fileParams =
                new FileConfigServiceParams(
                    Owner: this.ownerToken,
                    Read: this.readToken,
                    Write: this.writeToken);

            this.configService = new ConfigService(
                [new FileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService)],
                [new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)],
                new NullLogger<IConfigService>(),
                this.tokenService,
                this.msTestHelpers.TempDirectory,
                this.msTestHelpers.TempDirectory);

            this.pluginService = new PluginService(new NullLogger<IPluginService>());
            this.configService.RegisterConfig(this.fileParams, out this.manifest);

            PluginManifest pluginManifest = new()
            {
                InterfaceStates =
                [
                    new(
                        pluginId: Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                        assemblyName: this.enabledAssembly.GetName().Name!,
                        className: this.enabledImplementationType.FullName!,
                        interfaceName: this.enabledInterfaceType.FullName!,
                        enabled: true,
                        loadOrder: 1),
                    new(
                        pluginId: Guid.Parse("87654321-4321-4321-4321-cba987654321"),
                        assemblyName: this.disabledAssembly.GetName().Name!,
                        className: this.disabledImplementationType.FullName!,
                        interfaceName: this.disabledInterfaceType.FullName!,
                        enabled: false,
                        loadOrder: 2)
                ]
            };

            this.pluginCache = new PluginCache([]);

            await this.manifest.SaveAsync(pluginManifest);

            this.pluginRegistrar =
                new PluginRegistrar(
                    new NullLogger<IPluginRegistrar>(),
                    this.manifest,
                    this.pluginService,
                    this.pluginCache);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Thread.Sleep(100);
            Serilog.Log.CloseAndFlush();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            this.msTestHelpers.Dispose();
        }

        #region PluginRegistrarTests: Registration

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange & Act
            PluginRegistrar registrar = new(
                new NullLogger<IPluginRegistrar>(),
                this.manifest!,
                this.pluginService!,
                this.pluginCache!);

            // Assert
            Assert.IsInstanceOfType<PluginRegistrar>(registrar, "PluginRegistrar should be created successfully");
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(null!, this.manifest!, this.pluginService!, this.pluginCache!));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullPluginManifest_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(new NullLogger<IPluginRegistrar>(), null!, this.pluginService!, this.pluginCache!));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullPluginService_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(new NullLogger<IPluginRegistrar>(), this.manifest!, null!, this.pluginCache!));
        }

        #endregion

        #region PluginRegistrarTests: Accessors

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_ExistingEnabledPlugin_ReturnsTrue()
        {
            // Arrange
            Guid pluginId = Guid.Parse("12345678-1234-1234-1234-123456789abc");

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(pluginId, this.enabledInterfaceType);

            // Assert
            Assert.IsTrue(result, "Should return true for enabled plugin");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_ExistingDisabledPlugin_ReturnsFalse()
        {
            // Arrange
            Guid pluginId = Guid.Parse("87654321-4321-4321-4321-cba987654321");

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(pluginId, this.enabledInterfaceType);

            // Assert
            Assert.IsFalse(result, "Should return false for disabled plugin");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_NonExistentPlugin_ReturnsFalse()
        {
            // Arrange
            Guid pluginId = Guid.NewGuid();

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(pluginId, typeof(ISet<int>));

            // Assert
            Assert.IsFalse(result, "Should return false for non-existent plugin");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public async Task GetEnabled_EmptyInterfaceStates_ReturnsFalse()
        {
            // Arrange
            PluginManifest manifestWithEmptyStates = new() { InterfaceStates = [] };
            await this.manifest!.SaveAsync(manifestWithEmptyStates);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(Guid.NewGuid(), typeof(object));

            // Assert
            Assert.IsFalse(result, "Should return false when InterfaceStates is empty");
        }

        #endregion

        #region PluginRegistrarTests: Mutators

        [TestMethod]
        [TestCategory("SetEnabled")]
        public void SetEnabled_ValidPlugin_EnablesPlugin()
        {
            // Arrange
            Guid pluginId = Guid.Parse("87654321-4321-4321-4321-cba987654321");

            // Act
            this.pluginRegistrar!.SetEnabled(pluginId, this.disabledInterfaceType);

            // Assert            
            PluginLoadState updatedState = this.manifest!.Get().InterfaceStates.First(x => x.PluginId == pluginId);
            Assert.IsTrue(updatedState.Enabled, "Plugin should be enabled after SetEnabled call");
        }

        [TestMethod]
        [TestCategory("SetDisabled")]
        public void SetDisabled_ValidPlugin_DisablesPlugin()
        {
            // Arrange
            Guid pluginId = Guid.Parse("12345678-1234-1234-1234-123456789abc");

            // Act
            this.pluginRegistrar!.SetEnabled(pluginId, this.enabledInterfaceType, false);

            // Assert
            PluginLoadState updatedState = this.manifest!.Get().InterfaceStates.First(x => x.PluginId == pluginId);
            Assert.IsFalse(updatedState.Enabled, "Plugin should be disabled after SetDisabled call");
        }

        #endregion
    }
}