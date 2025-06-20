using Microsoft.Extensions.Logging;
using Moq;
using PlugHub.Services;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;

namespace PlugHub.UnitTests.Services
{
    [TestClass]
    public class ConfigAccessorTests
    {
        private Mock<ILogger<ConfigService>>? loggerMock;
        private ITokenService? tokenService;
        private string? testConfigPath;
        private ConfigService? configService;

        // Test configuration classes
        public class AlphaPluginConfig
        {
            public int FieldA { get; set; } = 50;
            public bool FieldB { get; set; } = false;
        }

        public class BetaPluginConfig
        {
            public required string FieldA { get; set; } = "";
            public int FieldB { get; set; } = 120;
        }

        [TestInitialize]
        public void Setup()
        {
            Mock<ILogger<TokenService>> tokenLoggerMock = new Mock<ILogger<TokenService>>();
            this.loggerMock = new Mock<ILogger<ConfigService>>();
            this.tokenService = new TokenService(tokenLoggerMock.Object);
            this.testConfigPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(this.testConfigPath);

            this.configService = new ConfigService(
                this.loggerMock.Object,
                this.tokenService,
                this.testConfigPath,
                this.testConfigPath
            );
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (this.testConfigPath != null && Directory.Exists(this.testConfigPath))
                Directory.Delete(this.testConfigPath, true);
        }

        [TestMethod]
        public void ConfigAccessorForRegisteredTypeReturnsAccessor()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();
            this.CreateConfigFile("AlphaPluginConfig.BaseSettings.json",
                "{\"FieldA\": 100, \"FieldB\": true}");

            this.configService!.RegisterConfigs([typeof(AlphaPluginConfig)]);

            // Act
            IConfigAccessor accessor = this.configService.CreateAccessor(typeof(AlphaPluginConfig), token);
            IConfigAccessorFor<AlphaPluginConfig> typedAccessor = accessor.For<AlphaPluginConfig>();

            // Assert
            Assert.IsNotNull(typedAccessor);
        }

        [TestMethod]
        public void ConfigAccessorGetValueReturnsCorrectValue()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();
            this.CreateConfigFile("AlphaPluginConfig.BaseSettings.json",
                "{\"FieldA\": 100, \"FieldB\": true}");

            this.configService!.RegisterConfigs([typeof(AlphaPluginConfig)]);
            IConfigAccessor accessor = this.configService.CreateAccessor(typeof(AlphaPluginConfig), token);
            IConfigAccessorFor<AlphaPluginConfig> typedAccessor = accessor.For<AlphaPluginConfig>();

            // Act
            int value = typedAccessor.Get<int>("FieldA");

            // Assert
            Assert.AreEqual(100, value);
        }

        [TestMethod]
        public void ConfigAccessorSetValueUpdatesConfiguration()
        {
            if (this.configService == null)
                return;

            // Arrange
            Token token = this.tokenService!.CreateToken();
            this.CreateConfigFile("BetaPluginConfig.UserSettings.json",
                "{\"FieldA\": \"test\", \"FieldB\": 200}");

            IConfigAccessor accessor = this.configService.CreateAccessor(typeof(BetaPluginConfig), token);
            IConfigAccessorFor<BetaPluginConfig> typedAccessor = accessor.For<BetaPluginConfig>();

            // Act
            typedAccessor.Set("FieldA", "updated");
            string updatedValue = typedAccessor.Get<string>("FieldA");

            // Assert
            Assert.AreEqual("updated", updatedValue);
        }

        [TestMethod]
        public async Task ConfigAccessorSavePersistsChangesAsync()
        {
            if (this.testConfigPath == null)
                return;

            // Arrange
            Token token = this.tokenService!.CreateToken();
            this.CreateConfigFile("BetaPluginConfig.UserSettings.json",
                "{\"FieldA\": \"test\", \"FieldB\": 200}");

            this.configService!.RegisterConfigs([typeof(BetaPluginConfig)]);
            IConfigAccessor accessor = this.configService.CreateAccessor(typeof(BetaPluginConfig));
            IConfigAccessorFor<BetaPluginConfig> typedAccessor = accessor.For<BetaPluginConfig>();

            // Act
            typedAccessor.Set("FieldA", "updated");
            await typedAccessor.SaveAsync();  // Added await

            // Assert
            string filePath = Path.Combine(this.testConfigPath, "Config", "BetaPluginConfig.UserSettings.json");
            string fileContent = File.ReadAllText(filePath);
            Assert.IsTrue(fileContent.Contains("\"FieldA\":\"updated\""),
                $"File content: {fileContent}");
        }

        [TestMethod]
        public void RegisterConfig_WithAttackerToken_ThrowsWhenOverwriting()
        {
            // Arrange
            Token ownerToken = tokenService!.CreateToken();
            Token attackerToken = tokenService!.CreateToken();
            CreateConfigFile("AlphaPluginConfig.BaseSettings.json", "{}");

            // First registration (owner)
            configService!.RegisterConfigs([typeof(AlphaPluginConfig)], ownerToken);

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                configService.RegisterConfigs([typeof(AlphaPluginConfig)], attackerToken));
        }

        [TestMethod]
        public void ConfigAccessorForUnregisteredTypeThrowsException()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();
            IConfigAccessor accessor = this.configService!.CreateAccessor(typeof(AlphaPluginConfig), token);

            // Act & Assert
            Assert.ThrowsException<TypeAccessException>(() =>
                accessor.For<BetaPluginConfig>());
        }


        [TestMethod]
        public void ConfigAccessorGet_ReturnsCorrectInstance()
        {
            if (this.testConfigPath == null || this.configService == null)
                return;

            // Arrange
            Token token = this.tokenService!.CreateToken();
            this.CreateConfigFile("BetaPluginConfig.BaseSettings.json",
                "{\"FieldA\":\"base-value\",\"FieldB\":500}");

            this.configService.RegisterConfigs([typeof(BetaPluginConfig)]);
            IConfigAccessor accessor = this.configService.CreateAccessor(typeof(BetaPluginConfig));
            IConfigAccessorFor<BetaPluginConfig> typedAccessor = accessor.For<BetaPluginConfig>();

            // Act
            var config = typedAccessor.Get();

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual("base-value", config.FieldA);
            Assert.AreEqual(500, config.FieldB);
        }

        [TestMethod]
        public async Task ConfigAccessorSaveAsync_WithModifiedConfig_UpdatesFile()
        {
            if (this.testConfigPath == null || this.configService == null)
                return;

            // Arrange
            Token token = this.tokenService!.CreateToken();
            this.CreateConfigFile("BetaPluginConfig.BaseSettings.json",
                "{\"FieldA\":\"initial\",\"FieldB\":100}");

            this.configService.RegisterConfigs([typeof(BetaPluginConfig)]);
            IConfigAccessor accessor = this.configService.CreateAccessor(typeof(BetaPluginConfig));
            IConfigAccessorFor<BetaPluginConfig> typedAccessor = accessor.For<BetaPluginConfig>();

            // Get and modify config
            var config = typedAccessor.Get();
            config.FieldA = "modified";
            config.FieldB = 200;

            // Act
            await typedAccessor.SaveAsync(config);

            // Assert
            string filePath = Path.Combine(this.testConfigPath, "Config", "BetaPluginConfig.UserSettings.json");
            string fileContent = File.ReadAllText(filePath);
            Assert.IsTrue(fileContent.Contains("\"FieldA\":\"modified\""),
                "User settings should contain modified FieldA");
            Assert.IsTrue(fileContent.Contains("\"FieldB\":\"200\""),
                "User settings should contain modified FieldB");
        }

        [TestMethod]
        public async Task ConfigAccessorSaveAsync_RemovesRedundantUserSettings()
        {
            if (this.testConfigPath == null || this.configService == null)
                return;

            // Arrange
            Token token = this.tokenService!.CreateToken();

            // Create base config with value
            this.CreateConfigFile("BetaPluginConfig.BaseSettings.json",
                "{\"FieldA\":\"base-value\",\"FieldB\":100}");

            // Create user config that overrides one value
            this.CreateConfigFile("BetaPluginConfig.UserSettings.json",
                "{\"FieldA\":\"user-override\"}");

            this.configService.RegisterConfigs([typeof(BetaPluginConfig)]);
            IConfigAccessor accessor = this.configService.CreateAccessor(typeof(BetaPluginConfig));
            IConfigAccessorFor<BetaPluginConfig> typedAccessor = accessor.For<BetaPluginConfig>();

            // Get config and set FieldA back to base value
            var config = typedAccessor.Get();
            config.FieldA = "base-value";  // Matches base value

            // Act
            await typedAccessor.SaveAsync(config);

            // Assert
            string userFilePath = Path.Combine(this.testConfigPath, "Config", "BetaPluginConfig.UserSettings.json");
            string userContent = File.ReadAllText(userFilePath);

            // Should remove FieldA from user settings since it matches base
            Assert.IsFalse(userContent.Contains("FieldA"),
                "User settings should not contain FieldA after resetting to base value");
        }


        private void CreateConfigFile(string fileName, string content)
        {
            if (this.testConfigPath == null)
                return;

            string dirPath = Path.Combine(this.testConfigPath, "Config");
            Directory.CreateDirectory(dirPath);
            File.WriteAllText(Path.Combine(dirPath, fileName), content);
        }
    }
}
