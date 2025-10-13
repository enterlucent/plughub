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
using PlugHub.Shared.Models.Configuration.Parameters;
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
        private ConfigFileParams fileParams = new();
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
                new ConfigFileParams(
                    Owner: this.ownerToken,
                    Read: this.readToken,
                    Write: this.writeToken);

            this.configService = new ConfigService(
                [new FileConfigProvider(new NullLogger<IConfigProvider>(), this.tokenService)],
                [new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)],
                new NullLogger<IConfigService>(),
                this.tokenService,
                this.msTestHelpers.TempDirectory,
                this.msTestHelpers.TempDirectory);

            this.pluginService = new PluginService(new NullLogger<IPluginService>());
            this.configService.RegisterConfig(this.fileParams, out this.manifest);

            PluginManifest pluginManifest = new()
            {
                DescriptorStates =
                [
                    new(
                        pluginId: Guid.Parse("12345678-1234-1234-1234-123456789abc"),
                        assemblyName: this.enabledAssembly.GetName().Name!,
                        interfaceName: this.enabledInterfaceType.FullName!,
                        className: this.enabledImplementationType.FullName!,
                        enabled: true,
                        loadOrder: 1),
                    new(
                        pluginId: Guid.Parse("87654321-4321-4321-4321-cba987654321"),
                        assemblyName: this.disabledAssembly.GetName().Name!,
                        interfaceName: this.disabledInterfaceType.FullName!,
                        className: this.disabledImplementationType.FullName!,
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
        public void Constructor_ValidParametersCreatesInstance()
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
        public void Constructor_NullLoggerThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(null!, this.manifest!, this.pluginService!, this.pluginCache!));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullPluginManifestThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(new NullLogger<IPluginRegistrar>(), null!, this.pluginService!, this.pluginCache!));
        }

        [TestMethod]
        [TestCategory("Constructor")]
        public void Constructor_NullPluginServiceThrowsArgumentNullException()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
                new PluginRegistrar(new NullLogger<IPluginRegistrar>(), this.manifest!, null!, this.pluginCache!));
        }

        #endregion

        #region PluginRegistrarTests: Accessors

        [TestMethod]
        [TestCategory("GetEnabled")]
        public void GetEnabled_ExistingEnabledPluginReturnsTrue()
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
        public void GetEnabled_ExistingDisabledPluginReturnsFalse()
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
        public void GetEnabled_NonExistentPluginReturnsFalse()
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
        public async Task GetEnabled_EmptyInterfaceStatesReturnsFalse()
        {
            // Arrange
            PluginManifest manifestWithEmptyStates = new() { DescriptorStates = [] };
            await this.manifest!.SaveAsync(manifestWithEmptyStates);

            // Act
            bool result = this.pluginRegistrar!.IsEnabled(Guid.NewGuid(), typeof(object));

            // Assert
            Assert.IsFalse(result, "Should return false when InterfaceStates is empty");
        }


        [TestMethod]
        [TestCategory("GetManifest")]
        public void GetManifest_ValidManifestReturnsNonNull()
        {
            // Act
            PluginManifest manifest = this.pluginRegistrar!.GetManifest();

            // Assert
            Assert.IsTrue(manifest.DescriptorStates.Count > 0, "Manifest should contain interface states.");
        }

        #endregion

        #region PluginRegistrarTests: Interface Mutators

        [TestMethod]
        [TestCategory("SetEnabled")]
        public void SetEnabled_ValidPluginEnablesPlugin()
        {
            // Arrange
            Guid pluginId = Guid.Parse("87654321-4321-4321-4321-cba987654321");

            // Act
            this.pluginRegistrar!.SetEnabled(pluginId, this.disabledInterfaceType);

            // Assert            
            PluginLoadState updatedState = this.manifest!.Get().DescriptorStates.First(x => x.PluginId == pluginId);

            Assert.IsTrue(updatedState.Enabled, "Plugin should be enabled after SetEnabled call");
        }

        [TestMethod]
        [TestCategory("SetDisabled")]
        public void SetDisabled_ValidPluginDisablesPlugin()
        {
            // Arrange
            Guid pluginId = Guid.Parse("12345678-1234-1234-1234-123456789abc");

            // Act
            this.pluginRegistrar!.SetEnabled(pluginId, this.enabledInterfaceType, false);

            // Assert
            PluginLoadState updatedState = this.manifest!.Get().DescriptorStates.First(x => x.PluginId == pluginId);

            Assert.IsFalse(updatedState.Enabled, "Plugin should be disabled after SetDisabled call");
        }

        #endregion

        #region PluginRegistrarTests: Plugin Mutators

        [TestMethod]
        [TestCategory("SetAllEnabled")]
        public void SetAllEnabled_DisabledPluginGetsEnabled()
        {
            // Arrange
            Guid pluginId = Guid.Parse("87654321-4321-4321-4321-cba987654321");

            // Act
            this.pluginRegistrar!.SetAllEnabled(pluginId, true);

            // Assert
            PluginLoadState updatedState = this.manifest!.Get().DescriptorStates.First(x => x.PluginId == pluginId);

            Assert.IsTrue(updatedState.Enabled, "All descriptors of the plugin should be enabled.");
        }

        [TestMethod]
        [TestCategory("SetAllEnabled")]
        public void SetAllEnabled_EnabledPluginGetsDisabled()
        {
            // Arrange
            Guid pluginId = Guid.Parse("12345678-1234-1234-1234-123456789abc");

            // Act
            this.pluginRegistrar!.SetAllEnabled(pluginId, false);

            // Assert
            PluginLoadState updatedState = this.manifest!.Get().DescriptorStates.First(x => x.PluginId == pluginId);

            Assert.IsFalse(updatedState.Enabled, "All descriptors of the plugin should be disabled.");
        }

        [TestMethod]
        [TestCategory("SetAllEnabled")]
        public void SetAllEnabled_NoChangeDoesNotModifyManifest()
        {
            // Arrange
            Guid pluginId = Guid.Parse("12345678-1234-1234-1234-123456789abc");
            PluginManifest before = this.manifest!.Get();

            // Act
            this.pluginRegistrar!.SetAllEnabled(pluginId, true);

            // Assert
            PluginManifest after = this.manifest!.Get();

            Assert.AreEqual(before.DescriptorStates.Count, after.DescriptorStates.Count, "Manifest should remain unchanged if descriptor states already match.");
        }

        #endregion

        #region PluginRegistrarTests: Manifest Mutators

        [TestMethod]
        [TestCategory("SaveManifest")]
        public async Task SaveManifest_ValidManifestPersistsToAccessor()
        {
            // Arrange
            PluginManifest newManifest = new()
            {
                DescriptorStates =
                [
                    new(pluginId: Guid.NewGuid(),
                        assemblyName: "TestAssembly",
                        interfaceName: typeof(IDisposable).FullName!,
                        className: "TestClass",
                        enabled: true,
                        loadOrder: 1)
                ]
            };

            // Act
            await this.pluginRegistrar!.SaveManifestAsync(newManifest);

            // Assert
            PluginManifest persisted = this.manifest!.Get();

            Assert.AreEqual(newManifest.DescriptorStates.Count, persisted.DescriptorStates.Count,
                "Persisted manifest should match saved manifest.");
        }

        [TestMethod]
        [TestCategory("SaveManifest")]
        public void SaveManifest_InvalidManifestThrowsArgumentException()
        {
            // Arrange
            PluginManifest invalidManifest = new() { DescriptorStates = null! };

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() =>
                this.pluginRegistrar!.SaveManifest(invalidManifest));
        }

        #endregion

        #region PluginRegistrarTests: Decriptor Traversal

        [TestMethod]
        [TestCategory("GetDescriptorsForInterface")]
        public void GetDescriptorsForInterface_NoPluginsReturnsEmptyList()
        {
            // Arrange & Act
            List<PluginDescriptor> result = this.pluginRegistrar!.GetDescriptorsForInterface(typeof(IDisposable));

            // Assert
            Assert.AreEqual(0, result.Count, "Should return empty list when no plugins match.");
        }

        [TestMethod]
        [TestCategory("GetDescriptorsForInterface")]
        public void GetDescriptorsForInterface_WithValidPluginReturnsDescriptors()
        {
            /*
            // Arrange
            // Assumes a test plugin assembly has been built and copied into MSTestHelpers.PluginDirectory
            // that implements IFakePluginInterface decorated with [DescriptorProvider].
            Type fakeInterface = typeof(IFakePluginInterface); // Placeholder test plugin interface type

            this.pluginCache!.Reload(); // Ensure cache discovers plugins in PluginDirectory

            // Act
            List<PluginDescriptor> result = this.pluginRegistrar!.GetDescriptorsForInterface(fakeInterface);

            // Assert
            Assert.IsTrue(result.Count > 0, "Expected at least one plugin descriptor to be returned.");
            */
        }

        [TestMethod]
        [TestCategory("GetDescriptorsForInterface")]
        public void GetDescriptorsForInterface_MissingAttributeLogsWarningAndSkips()
        {
            /*
            // Arrange
            // Requires plugin without [DescriptorProvider] attribute, placed in PluginDirectory
            Type fakeInterface = typeof(IEnumerable<int>); // Example interface with no descriptor

            this.pluginCache!.Reload();

            // Act
            List<PluginDescriptor> result = this.pluginRegistrar!.GetDescriptorsForInterface(fakeInterface);

            // Assert
            Assert.IsTrue(result.Count == 0, "Plugins missing DescriptorProvider attribute should not yield descriptors.");
            */
        }

        #endregion
    }
}