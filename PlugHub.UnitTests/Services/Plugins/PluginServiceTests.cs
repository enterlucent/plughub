using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Services.Plugins;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Mock.Interfaces.Services;
using PlugHub.Shared.Models.Plugins;


namespace PlugHub.UnitTests.Services.Plugins
{
    [TestClass]
    public sealed class PluginServiceTests
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private PluginService? pluginService;
        private ServiceCollection? serviceCollection;
        private ServiceProvider? serviceProvider;

        [TestInitialize]
        public void Setup()
        {
            this.pluginService = new PluginService(new NullLogger<IPluginService>());
        }

        [TestCleanup]
        public void Cleanup()
        {
            (this.serviceProvider as IDisposable)?.Dispose();

            Thread.Sleep(100);

            Serilog.Log.CloseAndFlush();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            this.msTestHelpers!.Dispose();
        }


        #region PluginServiceTests: Discovery

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldReturnAnyValidPlugins()
        {
            // Arrange & Act
            IEnumerable<PluginReference> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            // Assert
            Assert.IsTrue(plugins.Any(), "No plugins were found");
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldFindPluginMock()
        {
            // Arrange
            IEnumerable<PluginReference> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            PluginReference? foundPlugin = null;

            // Act
            foreach (PluginReference plugin in plugins)
                if (plugin.AssemblyName == "PlugHub.Plugin.Mock")
                    foundPlugin = plugin;

            // Assert
            Assert.IsNotNull(foundPlugin, "The mock plugin was not found");
            Assert.IsInstanceOfType<PluginReference>(foundPlugin, "Instance was not of the mock plugin type");
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldThrowIOExceptionForMissingDirectory()
        {
            // Arrange
            string nonExistentDirectory = @"Z:\Definitely\Not\A\Real\Path";

            // Act & Assert
            DirectoryNotFoundException exception = Assert.ThrowsException<DirectoryNotFoundException>(() =>
            {
                IEnumerable<PluginReference> plugins = this.pluginService!.Discover(nonExistentDirectory);
            });
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldThrowOnNullInput()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                this.pluginService!.Discover(null!);
            });
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldNotReturnPluginClassesWithoutInterfaces()
        {
            // Arrange && Act
            IEnumerable<PluginReference> plugins = this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            bool hasNoInterfacePlugin = plugins.Any(p => p.Type.FullName == "PlugHub.Plugin.Mock.PluginMockNoInterfaces");

            // Assert
            Assert.IsFalse(hasNoInterfacePlugin, "Plugins missing interfaces should not be returned in discovery");
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldRejectPluginsWithDuplicatePluginIDs()
        {
            // Arrange && Act
            IEnumerable<PluginReference> plugins = this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            Guid duplicatePluginID = Guid.Parse("45bc53be-bff0-4f46-ad13-d483004cd8c8");

            List<PluginReference> groupedPlugins = [.. plugins.Where(p => p.Metadata.PluginID == duplicatePluginID)];

            // Assert
            Assert.AreEqual(1, groupedPlugins.Count, "Duplicate plugins with same metadata should be filtered out");
        }

        #endregion

        #region PluginServiceTests: Instantiate

        [TestMethod]
        [TestCategory("Instantiate")]
        public void Instantiate_ShouldReturnPluginBase()
        {
            // Arrange
            IEnumerable<PluginReference> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            PluginReference? foundPlugin = null;

            // Act
            foreach (PluginReference plugin in plugins)
                if (plugin.AssemblyName == "PlugHub.Plugin.Mock" && plugin.Interfaces.Any())
                    foundPlugin = plugin;

            PluginBase? mockPlugin = this.pluginService.GetLoadedPlugin<PluginBase>(foundPlugin!.Interfaces.FirstOrDefault()!);

            // Assert
            Assert.IsNotNull(mockPlugin, "The mock plugin was not found");
            Assert.IsInstanceOfType<PluginBase>(mockPlugin, "Instance was not of the plugin base");
        }

        [TestMethod]
        [TestCategory("Instantiate")]
        public void Instantiate_ShouldReturnConfigInterface()
        {
            // Arrange
            IEnumerable<PluginReference> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);
            PluginReference? foundPlugin = null;
            PluginInterface? interfaceToTest;

            // Act
            foundPlugin = plugins
                .FirstOrDefault(plugin => plugin.AssemblyName == "PlugHub.Plugin.Mock" && plugin.Interfaces.Any());

            interfaceToTest = foundPlugin!.Interfaces
                .FirstOrDefault(i => i.InterfaceType == typeof(IPluginConfiguration));

            Assert.IsNotNull(interfaceToTest, "Expected interface IPluginConfiguration not found in plugin interfaces");

            IPluginConfiguration? configInterface =
                this.pluginService.GetLoadedInterface<IPluginConfiguration>(interfaceToTest!);

            // Assert
            Assert.IsNotNull(configInterface, "The mock plugin's configuration descriptor provider was not found");
            Assert.IsInstanceOfType(configInterface, typeof(IPluginConfiguration), "Instance was not of the configuration descriptor interface");
        }


        [TestMethod]
        [TestCategory("Instantiate")]
        public void GetLoadedPlugin_ShouldReturnNullForUnknownInterface()
        {
            // Arrage
            PluginInterface fakeInterface = new(
                Assembly: typeof(object).Assembly,
                ImplementationType: typeof(object),
                InterfaceType: typeof(IDisposable));

            // Act
            PluginBase? plugin = this.pluginService!.GetLoadedPlugin<PluginBase>(fakeInterface);

            // Assert
            Assert.IsNull(plugin, "Should return null for a plugin interface not present.");
        }

        [TestMethod]
        [TestCategory("Instantiate")]
        public void GetLoadedInterface_ShouldReturnNullIfInterfaceNotPresent()
        {
            // Arrange
            IEnumerable<PluginReference> plugins = this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);
            PluginReference? foundPlugin = plugins.FirstOrDefault();

            if (foundPlugin == null)
                Assert.Inconclusive("No plugins available for negative test.");

            PluginInterface fakeInterface = new(
                Assembly: typeof(object).Assembly,
                ImplementationType: typeof(object),
                InterfaceType: typeof(IDisposable)
            );

            // Act
            IDisposable? iface = this.pluginService!.GetLoadedInterface<IDisposable>(fakeInterface);

            // Assert
            Assert.IsNull(iface, "Should return null for interface contract not present in plugin.");
        }

        #endregion

        #region PluginServiceTests: Plugin Services

        [TestMethod]
        [TestCategory("PluginServices")]
        public void DISetup_ShouldResolveIEchoService()
        {
            // Arrange
            string input = "Test DI Echo";

            // Act
            IEchoService echoService = this.GetEchoService();

            string output = echoService.Echo(input);

            // Assert
            Assert.AreEqual(input, output);
        }

        [TestMethod]
        [TestCategory("PluginServices")]
        public void EchoService_ShouldRaiseMessageReceived_Event_OnEcho()
        {
            // Arrange
            IEchoService echoService = this.GetEchoService();
            string input = "Hello, events!";
            string? receivedMessage = null;

            echoService.MessageReceived += (sender, args) =>
            {
                receivedMessage = args.Message;
            };

            // Act
            echoService.Echo(input);

            // Assert
            Assert.IsNotNull(receivedMessage, "MessageReceived event did not fire.");
            Assert.AreEqual(input, receivedMessage, "MessageReceived event did not deliver the correct message.");
        }

        [TestMethod]
        [TestCategory("PluginServices")]
        public void EchoService_ShouldRaiseMessageErrorEventOnEmptyInput()
        {
            // Arrange
            IEchoService echoService = this.GetEchoService();
            string input = "";
            string? errorMsg = null;
            string? errorValue = null;

            echoService.MessageError += (sender, args) =>
            {
                errorMsg = args.Message;
                errorValue = args.Error;
            };

            // Act
            echoService.Echo(input);

            // Assert
            Assert.IsNotNull(errorMsg, "MessageError event did not fire.");
            Assert.AreEqual(input, errorMsg, "MessageError event did not capture the empty input.");
            Assert.AreEqual("The message provided was emtpy.", errorValue, "MessageError event did not convey the expected error message.");
        }

        #endregion

        #region PluginServiceTests: Descriptor Attributes

        [TestMethod]
        [TestCategory("DescriptorAttributes")]
        public void GetDescriptorAttribute_ShouldReturnAttribute_ForValidInterface()
        {
            // Arrange
            string interfaceName = "PlugHub.Shared.Interfaces.Plugins.IPluginAppConfig";

            // Act
            Shared.Attributes.DescriptorProviderAttribute? attribute =
                this.pluginService!.GetDescriptorProviderAttribute(interfaceName);

            // Assert
            Assert.IsNotNull(attribute, "Expected to find DescriptorAttribute on the interface.");
            Assert.AreEqual("GetAppConfigDescriptors", attribute.DescriptorAccessorName);
        }

        [TestMethod]
        [TestCategory("DescriptorAttributes")]
        public void GetDescriptorAttribute_ShouldReturnNull_ForNonExistentInterface()
        {
            // Arrange
            string interfaceName = "Non.Existent.Namespace.INonExistentInterface";

            // Act
            Shared.Attributes.DescriptorProviderAttribute? attribute = this.pluginService!.GetDescriptorProviderAttribute(interfaceName);

            // Assert
            Assert.IsNull(attribute, "Expected null for a non-existent interface.");
        }

        [TestMethod]
        [TestCategory("DescriptorAttributes")]
        public void GetDescriptorAttribute_ShouldThrow_OnNullOrEmptyInput()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                this.pluginService!.GetDescriptorProviderAttribute(string.Empty);
            });
        }

        #endregion


        private IEchoService GetEchoService(bool forceRebuild = false)
        {
            if (this.serviceProvider == null || this.serviceCollection == null || forceRebuild)
            {
                (this.serviceProvider as IDisposable)?.Dispose();
                this.serviceCollection = this.msTestHelpers.CreateTempServiceCollection();

                IEnumerable<PluginReference> plugins =
                    this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

                PluginInterface mockPluginInterface = plugins
                    .First(p => p.AssemblyName == "PlugHub.Plugin.Mock")
                    .Interfaces
                    .First();

                IPluginDependencyInjection? injectgor =
                    this.pluginService.GetLoadedInterface<IPluginDependencyInjection>(mockPluginInterface);

                foreach (PluginInjectorDescriptor descriptor in injectgor!.GetInjectionDescriptors())
                {
                    if (descriptor.ImplementationType != null)
                    {
                        this.serviceCollection.Add(
                            new ServiceDescriptor(
                                descriptor.InterfaceType,
                                descriptor.ImplementationType,
                                descriptor.Lifetime));
                    }
                    else if (descriptor.Instance != null)
                    {
                        this.serviceCollection.AddSingleton(
                            descriptor.InterfaceType,
                            descriptor.Instance);
                    }
                }

                this.serviceProvider = this.serviceCollection.BuildServiceProvider();
            }

            return this.serviceProvider.GetService<IEchoService>()!;
        }
    }
}