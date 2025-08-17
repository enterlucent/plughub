using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
using System.Text;

namespace PlugHub.UnitTests.Services
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

            this.configService.RegisterConfig(this.fileParams, out this.manifest);

            PluginManifest pluginManifest = new()
            {
                InterfaceStates =
                [
                    new(
                        pluginId: Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                        assemblyName: this.enabledAssembly.GetName().Name!,
                        implementationName: this.enabledImplementationType.FullName!,
                        interfaceName: this.enabledInterfaceType.FullName!,
                        enabled: true,
                        loadOrder: 1),
                    new(
                        pluginId: Guid.Parse("87654321-4321-4321-4321-cba987654321"),
                        assemblyName: this.disabledAssembly.GetName().Name!,
                        implementationName: this.disabledImplementationType.FullName!,
                        interfaceName: this.disabledInterfaceType.FullName!,
                        enabled: false,
                        loadOrder: 2)
                ]
            };

            await this.manifest.SaveAsync(pluginManifest);

            this.pluginRegistrar =
                new PluginRegistrar(
                    new NullLogger<IPluginRegistrar>(),
                    this.manifest!,
                    []);
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
                []);

            // Assert
            Assert.IsInstanceOfType<PluginRegistrar>(registrar, "PluginRegistrar should be created successfully");
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(null!, this.manifest!, []));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullPluginManifest_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(new NullLogger<IPluginRegistrar>(), null!, []));
        }

        #endregion

        #region PluginRegistrarTests: Accessors

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_ExistingEnabledPlugin_ReturnsTrue()
        {
            // Arrange
            Guid pluginId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
            PluginReference plugin = CreatePlugin(pluginId, this.enabledImplementationType, this.enabledInterfaceType, this.enabledAssembly);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(plugin);

            // Assert
            Assert.IsTrue(result, "Should return true for enabled plugin");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_ExistingDisabledPlugin_ReturnsFalse()
        {
            // Arrange
            Guid pluginId = Guid.Parse("87654321-4321-4321-4321-cba987654321");
            PluginReference plugin = CreatePlugin(pluginId, this.disabledImplementationType, this.disabledInterfaceType, this.disabledAssembly);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(plugin);

            // Assert
            Assert.IsFalse(result, "Should return false for disabled plugin");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_NonExistentPlugin_ReturnsFalse()
        {
            // Arrange
            Guid pluginId = Guid.NewGuid();
            PluginReference plugin = CreatePlugin(pluginId, typeof(HashSet<int>), typeof(ISet<int>), typeof(HashSet<int>).Assembly);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(plugin);

            // Assert
            Assert.IsFalse(result, "Should return false for non-existent plugin");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_PluginInterface_ExistingEnabledInterface_ReturnsTrue()
        {
            // Arrange
            PluginInterface pluginInterface = new(
                Assembly: this.enabledAssembly,
                ImplementationType: this.enabledImplementationType,
                InterfaceType: this.enabledInterfaceType);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(pluginInterface);

            // Assert
            Assert.IsTrue(result, "Should return true for enabled plugin interface");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_PluginInterface_ExistingDisabledInterface_ReturnsFalse()
        {
            // Arrange
            PluginInterface pluginInterface = new(
                Assembly: this.disabledAssembly,
                ImplementationType: this.disabledImplementationType,
                InterfaceType: this.disabledInterfaceType);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(pluginInterface);

            // Assert
            Assert.IsFalse(result, "Should return false for disabled plugin interface");
        }

        [TestMethod]
        [TestCategory("GetEnabled")]
        public async Task GetEnabled_EmptyInterfaceStates_ReturnsFalse()
        {
            // Arrange
            PluginManifest manifestWithEmptyStates = new() { InterfaceStates = [] };
            await this.manifest!.SaveAsync(manifestWithEmptyStates);

            PluginReference plugin = CreatePlugin(Guid.NewGuid(), typeof(StringBuilder), typeof(object), typeof(StringBuilder).Assembly);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(plugin);

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
            PluginReference plugin = CreatePlugin(pluginId, this.disabledImplementationType, this.disabledInterfaceType, this.disabledAssembly);

            // Act
            this.pluginRegistrar!.SetEnabled(plugin);

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
            PluginReference plugin = CreatePlugin(pluginId, this.enabledImplementationType, this.enabledInterfaceType, this.enabledAssembly);

            // Act
            this.pluginRegistrar!.SetDisabled(plugin);

            // Assert
            PluginLoadState updatedState = this.manifest!.Get().InterfaceStates.First(x => x.PluginId == pluginId);
            Assert.IsFalse(updatedState.Enabled, "Plugin should be disabled after SetDisabled call");
        }

        [TestMethod]
        [TestCategory("SetEnabled")]
        public void SetEnabled_PluginInterface_EnablesInterface()
        {
            // Arrange
            PluginInterface pluginInterface = new(
                Assembly: this.disabledAssembly,
                ImplementationType: this.disabledImplementationType,
                InterfaceType: this.disabledInterfaceType);

            // Act
            this.pluginRegistrar!.SetEnabled(pluginInterface);

            // Assert
            PluginLoadState matchingState =
                this.manifest!.Get().InterfaceStates.First(x =>
                    x.AssemblyName == this.disabledAssembly.GetName().Name &&
                    x.ImplementationName == this.disabledImplementationType.FullName &&
                    x.InterfaceName == this.disabledInterfaceType.FullName);

            Assert.IsTrue(matchingState.Enabled, "Plugin interface should be enabled");
        }

        [TestMethod]
        [TestCategory("SetEnabled")]
        public void SetEnabled_PluginInterface_NullInterface_ThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                this.pluginRegistrar!.SetEnabled((PluginInterface)null!));
        }

        #endregion

        #region PluginRegistrarTests: Bootstrapping

        [TestMethod]
        [TestCategory("StaticMethods")]
        public void SynchronizePluginConfig_ValidInputs_AddsNewPluginConfigurations()
        {
            // Arrange 
            Mock<IConfigAccessorFor<PluginManifest>> mockManifest = new();
            PluginManifest testManifest = new() { InterfaceStates = [] };
            mockManifest.Setup(x => x.Get()).Returns(testManifest);

            List<PluginReference> plugins = [
                CreatePlugin(Guid.NewGuid(), typeof(Stack<double>), typeof(IEnumerable<double>), typeof(Stack<double>).Assembly)
            ];

            // Act
            PluginRegistrar.SynchronizePluginConfig(new NullLogger<IPluginRegistrar>(), mockManifest.Object, plugins);

            // Assert
            mockManifest.Verify(x => x.Save(It.IsAny<PluginManifest>()), Times.Once);
            Assert.AreEqual(1, testManifest.InterfaceStates.Count, "Should add new plugin configuration");
            Assert.IsFalse(testManifest.InterfaceStates[0].Enabled, "New plugin should be disabled by default");
        }

        [TestMethod]
        [TestCategory("StaticMethods")]
        public void SynchronizePluginConfig_NullPluginManifest_ThrowsArgumentNullException()
        {
            // Arrange
            List<PluginReference> plugins = [];

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                PluginRegistrar.SynchronizePluginConfig(new NullLogger<IPluginRegistrar>(), null!, plugins));
        }

        [TestMethod]
        [TestCategory("StaticMethods")]
        public void GetEnabledInterfaces_HasEnabledInterfaces_ReturnsOnlyEnabledInterfaces()
        {
            // Arrange
            Mock<IConfigAccessorFor<PluginManifest>> testManifest = new();
            PluginManifest manifest = new()
            {
                InterfaceStates =
                [
                    new(
                        pluginId: Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                        assemblyName: typeof(LinkedList<char>).Assembly.GetName().Name!,
                        implementationName: typeof(LinkedList<char>).FullName!,
                        interfaceName: typeof(IEnumerable<char>).FullName!,
                        enabled: true,
                        loadOrder: 1),
                    new(
                        pluginId: Guid.Parse("87654321-4321-4321-4321-cba987654321"),
                        assemblyName: typeof(SortedSet<int>).Assembly.GetName().Name!,
                        implementationName: typeof(SortedSet<int>).FullName!,
                        interfaceName: typeof(ISet<int>).FullName!,
                        enabled: false,
                        loadOrder: 2)
                ]
            };
            testManifest.Setup(x => x.Get()).Returns(manifest);

            List<PluginReference> allPlugins =
            [
                CreatePlugin(
                    Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                    typeof(LinkedList<char>),
                    typeof(IEnumerable<char>),
                    typeof(LinkedList<char>).Assembly),
                CreatePlugin(
                    Guid.Parse("87654321-4321-4321-4321-cba987654321"),
                    typeof(SortedSet<int>),
                    typeof(ISet<int>),
                    typeof(SortedSet<int>).Assembly)
            ];

            // Act
            IEnumerable<PluginReference> enabledPlugins = PluginRegistrar.GetEnabledInterfaces(
                new NullLogger<IPluginRegistrar>(),
                testManifest.Object,
                allPlugins);

            // Assert
            Assert.AreEqual(1, enabledPlugins.Count(), "Should return only plugins with enabled interfaces");
            PluginReference enabledPlugin = enabledPlugins.First();
            Assert.AreEqual(Guid.Parse("12345678-1234-1234-1234-123456789abc"), enabledPlugin.Metadata.PluginID,
                "Should return the plugin with enabled interfaces");
        }

        #endregion

        private static PluginReference CreatePlugin(Guid pluginId, Type implementationType, Type interfaceType, Assembly assembly)
        {
            PluginInterface pluginInterface = new(
                Assembly: assembly,
                ImplementationType: implementationType,
                InterfaceType: interfaceType);

            PluginMetadata metadata = new(
                pluginId,
                "",
                $"Plugin for {implementationType.Name}",
                $"Test plugin for {implementationType.Name}",
                "1.0",
                "Enterlucent",
                []);

            return new PluginReference(assembly, implementationType, metadata, [pluginInterface]);
        }
    }
}
