using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Services;
using PlugHub.Shared;
using PlugHub.Shared.Interfaces;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Mock.Interfaces;


namespace PlugHub.UnitTests.Services
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

            System.Threading.Thread.Sleep(100);

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
            IEnumerable<Shared.Models.Plugin> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            // Assert
            Assert.IsTrue(plugins.Any(), "No plugins were found");
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldFindPluginMock()
        {
            // Arrange
            IEnumerable<Shared.Models.Plugin> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            Shared.Models.Plugin? foundPlugin = null;

            // Act
            foreach (Shared.Models.Plugin plugin in plugins)
                if (plugin.AssemblyName == "PlugHub.Plugin.Mock")
                    foundPlugin = plugin;

            // Assert
            Assert.IsNotNull(foundPlugin, "The mock plugin was not found");
            Assert.IsInstanceOfType<Shared.Models.Plugin>(foundPlugin, "Instance was not of the mock plugin type");
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldThrowIOException_ForMissingDirectory()
        {
            // Arrange
            string nonExistentDirectory = @"Z:\Definitely\Not\A\Real\Path";

            // Act & Assert
            var exception = Assert.ThrowsException<DirectoryNotFoundException>(() =>
            {
                var plugins = this.pluginService!.Discover(nonExistentDirectory);
            });
        }

        [TestMethod]
        [TestCategory("Discovery")]
        public void Discovery_ShouldThrow_OnNullInput()
        {
            // Arrange & Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                this.pluginService!.Discover(null!);
            });
        }


        #endregion

        #region PluginServiceTests: Instantiate

        [TestMethod]
        [TestCategory("Instantiate")]
        public void Instantiate_ShouldReturnPluginBase()
        {
            // Arrange
            IEnumerable<Shared.Models.Plugin> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            Shared.Models.Plugin? foundPlugin = null;

            // Act
            foreach (Shared.Models.Plugin plugin in plugins)
                if (plugin.AssemblyName == "PlugHub.Plugin.Mock")
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
            IEnumerable<Shared.Models.Plugin> plugins =
                this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

            Shared.Models.Plugin? foundPlugin = null;

            // Act
            foreach (Shared.Models.Plugin plugin in plugins)
                if (plugin.AssemblyName == "PlugHub.Plugin.Mock")
                    foundPlugin = plugin;

            IPluginConfiguration? configInterface =
                this.pluginService.GetLoadedInterface<IPluginConfiguration>(foundPlugin!.Interfaces.FirstOrDefault()!);

            // Assert
            Assert.IsNotNull(configInterface, "The mock plugin's configuration descriptor provider was not found");
            Assert.IsInstanceOfType<IPluginConfiguration>(configInterface, "Instance was not of the configuration descriptor interface");
        }

        [TestMethod]
        [TestCategory("Instantiate")]
        public void GetLoadedPlugin_ShouldReturnNull_ForUnknownInterface()
        {
            // Arrage
            Shared.Models.PluginInterface fakeInterface = new(
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
        public void GetLoadedInterface_ShouldReturnNull_IfInterfaceNotPresent()
        {
            // Arrange
            IEnumerable<Shared.Models.Plugin> plugins = this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);
            Shared.Models.Plugin? foundPlugin = plugins.FirstOrDefault();

            if (foundPlugin == null)
                Assert.Inconclusive("No plugins available for negative test.");

            Shared.Models.PluginInterface fakeInterface = new(
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
        public void DISetup_ShouldResolve_IEchoService()
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
        public void EchoService_ShouldRaise_MessageReceived_Event_OnEcho()
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
        public void EchoService_ShouldRaise_MessageError_Event_OnEmptyInput()
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


        private IEchoService GetEchoService(bool forceRebuild = false)
        {
            if (this.serviceProvider == null || this.serviceCollection == null || forceRebuild)
            {
                (this.serviceProvider as IDisposable)?.Dispose();
                this.serviceCollection = this.msTestHelpers.CreateTempServiceCollection();

                IEnumerable<Shared.Models.Plugin> plugins =
                    this.pluginService!.Discover(this.msTestHelpers.PluginDirectory);

                Shared.Models.PluginInterface mockPluginInterface = plugins
                    .First(p => p.AssemblyName == "PlugHub.Plugin.Mock")
                    .Interfaces
                    .First();

                IPluginDependencyInjector? injectgor =
                    this.pluginService.GetLoadedInterface<IPluginDependencyInjector>(mockPluginInterface);

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