using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using PlugHub.Services;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Services;
using System.Text.Json.Serialization;
using System.Text.Json;
using PlugHub.Shared.Models;

namespace PlugHub.UnitTests.Services
{
    [TestClass]
    public class ConfigServiceTests
    {
        private Mock<ILogger<ConfigService>>? loggerMock;
        private ITokenService? tokenService;
        private string? testConfigPath;


        // Dummy classes for testing
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
            Mock<ILogger<TokenService>> loggerToken = new Mock<ILogger<TokenService>>();

            this.loggerMock = new Mock<ILogger<ConfigService>>();
            this.tokenService = new TokenService(loggerToken.Object);
            this.testConfigPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(this.testConfigPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (this.testConfigPath as string != null)
                Directory.Delete(this.testConfigPath, true);
        }


        #region ConfigServiceTests: RegisterPluginConfiguration

        [TestMethod]
        public void RegisterPluginConfigurationsValidTypesRegistersSuccessfully()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            List<Type> configTypes = [typeof(AlphaPluginConfig), typeof(BetaPluginConfig)];

            string pluginAlphaConfigPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.BaseSettings.json");
            string pluginBetaConfigPath = Path.Combine(this.testConfigPath, "Config", "BetaPluginConfig.UserSettings.json");

            Directory.CreateDirectory(Path.Combine(this.testConfigPath, "Config"));

            File.WriteAllText(pluginAlphaConfigPath, "{\r\n  \"FieldA\": 80,\r\n  \"FieldB\": true\r\n}");
            File.WriteAllText(pluginBetaConfigPath, "{\r\n  \"FieldA\": \"1920x1080\",\r\n  \"FieldB\": 60\r\n}");

            // Act
            configService.RegisterConfigs(configTypes);

            // Assert
            // Verify settings retrieval
            Assert.AreEqual(80, configService.GetSetting<int>(typeof(AlphaPluginConfig), "FieldA"));
            Assert.AreEqual("1920x1080", configService.GetSetting<string>(typeof(BetaPluginConfig), "FieldA"));
        }

        [TestMethod]
        public void RegisterPluginConfigurationsInvalidConfigThrowsException()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            List<Type> configTypes = [typeof(AlphaPluginConfig)];

            string pluginAlphaConfigPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.BaseSettings.json");

            Directory.CreateDirectory(Path.Combine(this.testConfigPath, "Config"));

            // Create invalid JSON file
            File.WriteAllText(pluginAlphaConfigPath, "INVALID_JSON");

            // Act & Assert
            IOException ex = Assert.ThrowsException<IOException>(() =>
                configService.RegisterConfigs(configTypes));

            // Verify inner exception is JSON-related
            Assert.IsInstanceOfType(ex, typeof(IOException));
        }

        #endregion

        #region ConfigServiceTests: OnPluginConfigurationChanged

        [TestMethod]
        public async Task OnPluginConfigurationChangedValidTypeReloadsSettings()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            List<Type> configTypes = [typeof(AlphaPluginConfig)];

            // Create proper plugin directory
            string pluginAlphaConfigPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.BaseSettings.json");

            Directory.CreateDirectory(Path.Combine(this.testConfigPath, "Config"));

            // Initial config
            File.WriteAllText(pluginAlphaConfigPath, "{\"FieldA\": 80, \"FieldB\": true}");
            configService.RegisterConfigs(configTypes);

            // Set up event completion source
            TaskCompletionSource<bool> reloadCompleted = new();
            configService.ConfigReloaded += (sender, e) =>
            {
                if (e.ConfigType == typeof(AlphaPluginConfig))
                    reloadCompleted.SetResult(true);
            };

            // Update config
            File.WriteAllText(pluginAlphaConfigPath, "{\"FieldA\": 70, \"FieldB\": true}");

            // Wait for reload event with timeout
            Task completedTask = await Task.WhenAny(reloadCompleted.Task, Task.Delay(2000));
            if (completedTask != reloadCompleted.Task)
                Assert.Fail("PluginConfigurationReloaded event not raised within timeout");

            // Assert
            Assert.AreEqual(70, configService.GetSetting<int>(typeof(AlphaPluginConfig), "FieldA"));
        }

        [TestMethod]
        public async Task MultipleReloadsProcessSequentially()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            List<Type> configTypes = [typeof(AlphaPluginConfig)];

            // This matches the path logic in ConfigService
            string pluginAlphaConfigPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.BaseSettings.json");
            Directory.CreateDirectory(Path.Combine(this.testConfigPath, "Config"));

            // Initial config
            File.WriteAllText(pluginAlphaConfigPath, "{\"FieldA\": 80}");
            configService.RegisterConfigs(configTypes);

            int reloadCount = 0;
            Queue<TaskCompletionSource<bool>> reloadQueue = new();

            configService.ConfigReloaded += (s, e) =>
            {
                if (e.ConfigType == typeof(AlphaPluginConfig))
                {
                    reloadCount++;
                    if (reloadQueue.Count > 0)
                        reloadQueue.Dequeue().SetResult(true);
                }
            };

            for (int i = 0; i < 3; i++)
            {
                TaskCompletionSource<bool> reloadCompletion = new();
                reloadQueue.Enqueue(reloadCompletion);

                int newFieldA = 70 + i;
                File.WriteAllText(pluginAlphaConfigPath, $"{{\"FieldA\": {newFieldA}}}");

                await Task.WhenAny(reloadCompletion.Task);

                Assert.AreEqual(
                    newFieldA,
                    configService.GetSetting<int>(typeof(AlphaPluginConfig), "FieldA"),
                    $"FieldA mismatch after reload {i + 1}"
                );
            }

            Assert.AreEqual(3, reloadCount, "Should have triggered 3 reload events");
        }

        #endregion

        #region ConfigServiceTests: GetEnvironmentSettings

        [TestMethod]
        public void GetEnvironmentSettingsReturnsValidEnvironmentConfiguration()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Set test environment variables
            string testKey = "TEST_ENV_VAR_" + Guid.NewGuid().ToString("N");
            string expectedValue = "test_value_123";
            Environment.SetEnvironmentVariable(testKey, expectedValue);

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Act
            IConfiguration envConfig = configService.GetEnvConfig();
            string? actualValue = envConfig[testKey];

            // Cleanup environment
            Environment.SetEnvironmentVariable(testKey, null);

            // Assert
            Assert.AreEqual(expectedValue, actualValue);
        }

        [TestMethod]
        public void GetEnvironmentSettingsReflectsEnvironmentVariableChangeAfterRebuild()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Set initial environment variable
            string testKey = "TEST_ENV_VAR_" + Guid.NewGuid().ToString("N");
            string initialValue = "initial_value";
            string updatedValue = "updated_value";
            Environment.SetEnvironmentVariable(testKey, initialValue);

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Act 1: Read initial value
            IConfiguration envConfig1 = configService.GetEnvConfig();
            string? actualValue1 = envConfig1[testKey];
            Assert.AreEqual(initialValue, actualValue1);

            // Change the environment variable at runtime
            Environment.SetEnvironmentVariable(testKey, updatedValue);

            // Act 2: Rebuild the environment settings (simulate a refresh)
            IConfiguration envConfig2 = configService.GetEnvConfig();
            string? actualValue2 = envConfig2[testKey];

            // Cleanup
            Environment.SetEnvironmentVariable(testKey, null);

            // Assert
            Assert.AreEqual(updatedValue, actualValue2);
        }


        #endregion

        #region ConfigServiceTests: GetAppSetting

        [TestMethod]
        public void GetAppSettingKeyExistsAndTypeMatchesReturnsValue()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Set up appSettings dictionary via reflection
            System.Reflection.FieldInfo? appSettingsField = typeof(ConfigService).GetField("appSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (appSettingsField == null)
                return;

            appSettingsField.SetValue(configService, new Dictionary<string, object?>
            {
                { "TestInt", 42 },
                { "TestString", "hello" },
                { "TestBool", true }
            });

            // Act & Assert
            Assert.AreEqual(42, configService.GetAppSetting<int>("TestInt"));
            Assert.AreEqual("hello", configService.GetAppSetting<string>("TestString"));
            Assert.IsTrue(configService.GetAppSetting<bool>("TestBool"));
        }

        [TestMethod]
        public void GetAppSettingKeyDoesNotExistThrowsKeyNotFoundException()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Ensure appSettings is empty
            System.Reflection.FieldInfo? appSettingsField = typeof(ConfigService).GetField("appSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (appSettingsField == null)
                return;

            appSettingsField.SetValue(configService, new Dictionary<string, object?>());

            // Act & Assert
            KeyNotFoundException ex = Assert.ThrowsException<KeyNotFoundException>(() =>
                configService.GetAppSetting<int>("NonExistentKey"));
            StringAssert.Contains(ex.Message, "Application setting with key 'NonExistentKey' not found");
        }

        [TestMethod]
        public void GetAppSettingTypeMismatchThrowsInvalidCastException()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Set up appSettings with type mismatch
            System.Reflection.FieldInfo? appSettingsField = typeof(ConfigService).GetField("appSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (appSettingsField == null)
                return;

            appSettingsField.SetValue(configService, new Dictionary<string, object?>
            {
                { "TestKey", "string_value" }
            });

            // Act & Assert
            InvalidCastException ex = Assert.ThrowsException<InvalidCastException>(() =>
                configService.GetAppSetting<int>("TestKey"));
            StringAssert.Contains(ex.Message, "cannot be cast to type 'System.Int32'");
        }

        #endregion

        #region ConfigServiceTests: SetAppSetting

        [TestMethod]
        public void SetAppSettingNewKeyAddsKeyAndFiresEvent()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            bool eventFired = false;
            ConfigServiceAppSettingChangeEventArgs? eventArgs = null;
            configService.AppSettingChanged += (s, e) =>
            {
                eventFired = true;
                eventArgs = e;
            };

            // Act
            configService.SetAppSetting("NewKey", 42);

            // Assert
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventFired, "Event should be triggered for new key");
            Assert.AreEqual("NewKey", eventArgs.Key);
            Assert.IsNull(eventArgs.OldValue);
            Assert.AreEqual(42, eventArgs.NewValue);
            Assert.AreEqual(42, configService.GetAppSetting<int>("NewKey"));
        }

        [TestMethod]
        public void SetAppSettingExistingKeySameValueNoChangeOrEvent()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.SetAppSetting("ExistingKey", 100); // Initial value

            bool eventFired = false;
            configService.AppSettingChanged += (s, e) => eventFired = true;

            // Act
            configService.SetAppSetting("ExistingKey", 100); // Same value

            // Assert
            Assert.IsFalse(eventFired, "Event should not fire for same value");
            Assert.AreEqual(100, configService.GetAppSetting<int>("ExistingKey"));
        }

        [TestMethod]
        public void SetAppSettingExistingKeyNewValueUpdatesAndFiresEvent()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.SetAppSetting("UpdateKey", "OldValue");

            bool eventFired = false;
            ConfigServiceAppSettingChangeEventArgs? eventArgs = null;
            configService.AppSettingChanged += (s, e) =>
            {
                eventFired = true;
                eventArgs = e;
            };

            // Act
            configService.SetAppSetting("UpdateKey", "NewValue");

            // Assert
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventFired, "Event should trigger for changed value");
            Assert.AreEqual("UpdateKey", eventArgs.Key);
            Assert.AreEqual("OldValue", eventArgs.OldValue);
            Assert.AreEqual("NewValue", eventArgs.NewValue);
            Assert.AreEqual("NewValue", configService.GetAppSetting<string>("UpdateKey"));
        }

        [TestMethod]
        public void SetAppSettingTypeConversionFailureUpdatesAndFiresEvent()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Set up appSettings with type mismatch via reflection
            System.Reflection.FieldInfo? appSettingsField = typeof(ConfigService).GetField("appSettings",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (appSettingsField == null)
                return;

            appSettingsField.SetValue(configService, new Dictionary<string, object?>
            {
                { "TestKey", "string_value" } // Will fail conversion to int
            });

            bool eventFired = false;
            configService.AppSettingChanged += (s, e) => eventFired = true;

            // Act
            configService.SetAppSetting<int>("TestKey", 123);

            // Assert
            Assert.IsTrue(eventFired, "Event should fire despite conversion error");
            Assert.AreEqual(123, configService.GetAppSetting<int>("TestKey"));
        }

        [TestMethod]
        public void SetAppSettingNullValueHandlesCorrectly()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            bool eventFired = false;
            configService.AppSettingChanged += (s, e) => eventFired = true;

            // Act
            configService.SetAppSetting<string>("NullKey", null!);

            // Assert
            Assert.IsTrue(eventFired, "Event should fire for null value");
            Assert.IsNull(configService.GetAppSetting<string>("NullKey"));
        }

        [TestMethod]
        public void SetAppSettingMultipleThreadsThreadSafe()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            int eventCount = 0;
            configService.AppSettingChanged += (s, e) => Interlocked.Increment(ref eventCount);

            // Act
            Parallel.For(0, 100, i =>
            {
                configService.SetAppSetting($"Key_{i % 10}", i);
            });

            // Assert
            Assert.AreEqual(100, eventCount, "All sets should trigger events");
            for (int i = 0; i < 10; i++)
            {
                int finalValue = configService.GetAppSetting<int>($"Key_{i}");
                Assert.IsTrue(finalValue >= 0 && finalValue < 100, "Value within expected range");
            }
        }

        #endregion

        #region ConfigServiceTests: SetSetting

        [TestMethod]
        public void SetSettingUpdatesValue()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfigs([typeof(AlphaPluginConfig)]);

            bool eventFired = false;
            configService.SettingChanged += (s, e) => eventFired = true;

            // Act
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 85);

            // Assert
            int value = configService.GetSetting<int>(typeof(AlphaPluginConfig), "FieldA");

            Assert.AreEqual(85, value);
            Assert.IsTrue(eventFired, "Setting changes should fire events");
        }

        [TestMethod]
        public void SetSettingUserSettingNewValueUpdatesAndFiresEvent()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Create proper plugin directory
            string pluginAlphaConfigPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.BaseSettings.json");

            Directory.CreateDirectory(Path.Combine(this.testConfigPath, "Config"));

            // Initial config
            File.WriteAllText(pluginAlphaConfigPath, "{\"FieldA\": 80, \"FieldB\": true}");

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfigs([typeof(AlphaPluginConfig)]);

            bool eventFired = false;
            ConfigServiceSettingChangeEventArgs? eventArgs = null;
            configService.SettingChanged += (s, e) =>
            {
                eventFired = true;
                eventArgs = e;
            };

            // Act
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 90);

            // Assert
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventFired, "User setting changes should fire events");
            Assert.AreEqual(typeof(AlphaPluginConfig), eventArgs.ConfigType);
            Assert.AreEqual("FieldA", eventArgs.Key);
            Assert.IsNotNull(eventArgs.OldValue);
            Assert.AreEqual(90, eventArgs.NewValue);
            Assert.AreEqual(90, configService.GetSetting<int>(typeof(AlphaPluginConfig), "FieldA"));
        }

        [TestMethod]
        public void SetSettingUserSettingEqualsBaseValueRemovesOverrideAndFiresEvent()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfigs([typeof(AlphaPluginConfig)]);

            // Set base value
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 80);

            // Set user override
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 85);

            bool eventFired = false;
            configService.SettingChanged += (s, e) => eventFired = true;

            // Act
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 80); // Revert to base value

            // Assert
            Assert.IsTrue(eventFired, "Reverting to base value should fire event");
            Assert.AreEqual(80, configService.GetSetting<int>(typeof(AlphaPluginConfig), "FieldA"));
        }

        [TestMethod]
        public void SetSettingUserSettingDifferentValueUpdatesAndFiresEvent()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfigs([typeof(AlphaPluginConfig)]);

            // Set initial user value
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 75);

            bool eventFired = false;
            ConfigServiceSettingChangeEventArgs? eventArgs = null;
            configService.SettingChanged += (s, e) =>
            {
                eventFired = true;
                eventArgs = e;
            };

            // Act
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 85);

            // Assert
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventFired, "Value change should fire event");
            Assert.AreEqual(75, eventArgs.OldValue);
            Assert.AreEqual(85, eventArgs.NewValue);
            Assert.AreEqual(85, configService.GetSetting<int>(typeof(AlphaPluginConfig), "FieldA"));
        }

        [TestMethod]
        public void SetSettingUnregisteredPluginTypeThrowsException()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Act & Assert
            InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(() =>
                configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 80));
            StringAssert.Contains(ex.Message, "Plugin configuration not registered");
        }

        [TestMethod]
        public void SetSettingNullValueHandlesCorrectly()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfigs([typeof(BetaPluginConfig)]);

            bool eventFired = false;
            configService.SettingChanged += (s, e) => eventFired = true;

            // Act
            configService.SetSetting<string>(typeof(BetaPluginConfig), "FieldA", null!);

            // Assert
            Assert.IsTrue(eventFired, "Null value should fire event");
            Assert.IsNull(configService.GetSetting<string>(typeof(BetaPluginConfig), "FieldA"));
        }

        [TestMethod]
        public void SetSettingTypeConversionHandlesCorrectly()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfigs([typeof(AlphaPluginConfig)]);

            // Set initial value as int
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", 80);

            bool eventFired = false;
            configService.SettingChanged += (s, e) => eventFired = true;

            // Act & Assert
            // Change to string - should work despite type difference
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldA", "90");

            Assert.IsTrue(eventFired, "Type change should fire event");
            Assert.AreEqual("90", configService.GetSetting<string>(typeof(AlphaPluginConfig), "FieldA"));
        }

        #endregion

        #region ConfigServiceTests: SaveAppSettings

        [TestMethod]
        public async Task SaveAppSettingsWritesAppSettingsToFileAsync()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null ||
                this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService,
                                              this.testConfigPath, this.testConfigPath);
            configService.SetAppSetting("TestKey", 123);
            string appSettingsPath = Path.Combine(this.testConfigPath, "appsettings.json");

            // Act
            await configService.SaveAppSettingsAsync();

            // Assert
            Assert.IsTrue(File.Exists(appSettingsPath), "appsettings.json file should be created");
            string json = File.ReadAllText(appSettingsPath);
            Assert.IsTrue(json.Contains("TestKey") && json.Contains("123"),
                         "File should contain saved key and value");
        }

        [TestMethod]
        public async Task SaveAppSettingsWhenSaveFailsThrowsIOException()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null ||
                this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService,
                                              this.testConfigPath, this.testConfigPath);
            configService.SetAppSetting("TestKey", 123);

            // Create a read-only file to force IOException
            string appSettingsPath = Path.Combine(this.testConfigPath, "appsettings.json");
            File.WriteAllText(appSettingsPath, "{}");
            File.SetAttributes(appSettingsPath, FileAttributes.ReadOnly);

            try
            {
                // Act & Assert
                IOException ex = await Assert.ThrowsExceptionAsync<IOException>(
                    () => configService.SaveAppSettingsAsync()
                );
                StringAssert.Contains(ex.Message, "Failed to save application settings.");
            }
            finally
            {
                // Cleanup
                File.SetAttributes(appSettingsPath, FileAttributes.Normal);
                File.Delete(appSettingsPath);
            }
        }

        [TestMethod]
        public void SaveAppSettingsSerializesEmptyDictionary()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            string appSettingsPath = Path.Combine(this.testConfigPath, "appsettings.json");

            // Act
            configService.SaveAppSettingsAsync();

            // Assert
            Assert.IsTrue(File.Exists(appSettingsPath), "appsettings.json file should be created");
            string json = File.ReadAllText(appSettingsPath);
            Assert.IsTrue(json.Trim() == "{}" || json.Trim() == "{\r\n}", "File should contain an empty JSON object");
        }

        #endregion

        #region ConfigServiceTests: SaveSettings

        [TestMethod]
        public async Task SaveSettingsValidConfigTypeSavesFilesAsync()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null ||
                this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService,
                                              this.testConfigPath, this.testConfigPath);
            configService.RegisterConfig(typeof(AlphaPluginConfig));

            // Set sample settings
            configService.SetSetting(typeof(AlphaPluginConfig), "FieldB", true);

            string userSettingsPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.UserSettings.json");

            // Act
            await configService.SaveSettingsAsync(typeof(AlphaPluginConfig));

            // Assert
            Assert.IsTrue(File.Exists(userSettingsPath), "User settings file should be created");
            string userJson = File.ReadAllText(userSettingsPath);
            Assert.IsTrue(userJson.Contains("\"FieldB\": true") || userJson.Contains("\"FieldB\":true"),
                "User settings should contain test value");
        }

        [TestMethod]
        public async Task SaveSettingsUnregisteredConfigTypeThrowsExceptionAsync()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null ||
                this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            // Act & Assert
            IOException ex = await Assert.ThrowsExceptionAsync<IOException>(() =>
                configService.SaveSettingsAsync(typeof(AlphaPluginConfig)));
            StringAssert.Contains(ex.Message, "Failed to save settings for 'AlphaPluginConfig'");
        }

        [TestMethod]
        public async Task SaveSettingsWhenFileWriteFailsThrowsIOExceptionAsync()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null ||
                this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService,
                                              this.testConfigPath, this.testConfigPath);
            configService.RegisterConfig(typeof(AlphaPluginConfig));

            // Create and set files as read-only
            string userSettingsPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.UserSettings.json");
            string baseSettingsPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.BaseSettings.json");

            Directory.CreateDirectory(Path.GetDirectoryName(userSettingsPath) ?? "");
            File.WriteAllText(userSettingsPath, "{}");
            File.WriteAllText(baseSettingsPath, "{}");

            // Set files (not directory) as read-only
            new FileInfo(userSettingsPath).IsReadOnly = true;
            new FileInfo(baseSettingsPath).IsReadOnly = true;

            try
            {
                // Act & Assert
                IOException ex = await Assert.ThrowsExceptionAsync<IOException>(() =>
                    configService.SaveSettingsAsync(typeof(AlphaPluginConfig)));
                StringAssert.Contains(ex.Message, "Failed to save settings for 'AlphaPluginConfig'");
            }
            finally
            {
                // Cleanup
                new FileInfo(userSettingsPath).IsReadOnly = false;
                new FileInfo(baseSettingsPath).IsReadOnly = false;
                File.Delete(userSettingsPath);
                File.Delete(baseSettingsPath);
            }
        }


        [TestMethod]
        public void SaveSettingsEmptySettingsSavesValidJson()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfig(typeof(BetaPluginConfig));

            string baseSettingsPath = Path.Combine(this.testConfigPath, "Config", "BetaPluginConfig.BaseSettings.json");
            string userSettingsPath = Path.Combine(this.testConfigPath, "Config", "BetaPluginConfig.UserSettings.json");

            // Act
            configService.SaveSettingsAsync(typeof(BetaPluginConfig));

            // Assert
            string baseJson = File.ReadAllText(baseSettingsPath);
            string userJson = File.ReadAllText(userSettingsPath);


            Assert.AreEqual(typeof(BetaPluginConfig).SerializeToJson(new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.WriteAsString,
            }), baseJson.Trim(), "Empty user settings should save the default");

            Assert.AreEqual("{}", userJson.Trim(), "Empty base settings should save as {}");
        }

        #endregion

        #region ConfigServiceTests: ITokenService

        [TestMethod]
        public void GetSettingWithValidTokenReturnsValue()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            Token token = this.tokenService.CreateToken();
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfig(typeof(AlphaPluginConfig), readToken: token);
            configService.SetSetting(typeof(AlphaPluginConfig), "ApiKey", "test-value", token: token);

            // Act
            string result = configService.GetSetting<string>(typeof(AlphaPluginConfig), "ApiKey", token);

            // Assert
            Assert.AreEqual("test-value", result);
        }

        [TestMethod]
        public void GetSettingWithoutTokenThrowsWhenTokenRequired()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            Token token = this.tokenService.CreateToken();
            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            configService.RegisterConfig(typeof(AlphaPluginConfig), readToken: token);

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                configService.GetSetting<string>(typeof(AlphaPluginConfig), "FieldA"));
        }

        [TestMethod]
        public void SetSettingWithInvalidTokenThrowsUnauthorized()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            Token validToken = this.tokenService.CreateToken();
            Token invalidToken = this.tokenService.CreateToken();

            ConfigService configService = new(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);

            configService.RegisterConfig(typeof(AlphaPluginConfig), writeToken: validToken);

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                configService.SetSetting(typeof(AlphaPluginConfig), "ApiKey", "new-value", token: invalidToken));
        }

        #endregion

        #region ConfigServiceTests: BaseConfig

        [TestMethod]
        public async Task SaveAndGetBaseConfigFileContentsSucceeds()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new ConfigService(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            Token writeToken = this.tokenService.CreateToken();
            Token readToken = this.tokenService.CreateToken();
            configService.RegisterConfig(typeof(AlphaPluginConfig), readToken: readToken, writeToken: writeToken);

            string expectedContents = "{ \"FieldA\": 123, \"FieldB\": true }";

            // Act
            await configService.SaveBaseConfigFileContentsAsync(typeof(AlphaPluginConfig), expectedContents, writeToken);
            string actualContents = configService.GetBaseConfigFileContents(typeof(AlphaPluginConfig), readToken);

            // Assert
            Assert.AreEqual(expectedContents, actualContents);
        }

        [TestMethod]
        public async Task SaveBaseConfigFileContents_InvalidToken_ThrowsUnauthorizedAsync()
        {
            // Arrange
            if (this.loggerMock?.Object == null || this.testConfigPath == null || this.tokenService == null)
                return;

            var configService = new ConfigService(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            var validToken = this.tokenService.CreateToken();
            var invalidToken = this.tokenService.CreateToken();

            configService.RegisterConfig(typeof(BetaPluginConfig), writeToken: validToken);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(async () =>
                await configService.SaveBaseConfigFileContentsAsync(typeof(BetaPluginConfig), "{}", invalidToken));
        }

        [TestMethod]
        public void GetBaseConfigFileContentsInvalidTokenThrowsUnauthorized()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new ConfigService(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            Token validToken = this.tokenService.CreateToken();
            Token invalidToken = this.tokenService.CreateToken();
            configService.RegisterConfig(typeof(AlphaPluginConfig), readToken: validToken, writeToken: validToken);

            // Write a config file first
            configService.SaveBaseConfigFileContentsAsync(typeof(AlphaPluginConfig), "{}", validToken);

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                configService.GetBaseConfigFileContents(typeof(AlphaPluginConfig), invalidToken));
        }

        [TestMethod]
        public void GetBaseConfigFileContentsFileNotFoundThrows()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new ConfigService(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            Token token = this.tokenService.CreateToken();
            configService.RegisterConfig(typeof(BetaPluginConfig), readToken: token);

            // Act & Assert
            Assert.ThrowsException<FileNotFoundException>(() =>
                configService.GetBaseConfigFileContents(typeof(AlphaPluginConfig), token));
        }

        [TestMethod]
        public async Task SaveBaseConfigFileContentsCreatesDirectoryIfNotExists()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;

            // Arrange
            ConfigService configService = new ConfigService(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            Token token = this.tokenService.CreateToken();
            configService.RegisterConfig(typeof(AlphaPluginConfig), writeToken: token);

            // Simulate non-existent directory by deleting it
            Directory.Delete(this.testConfigPath, recursive: true);

            // Act
            string contents = "{ \"FieldA\": 1, \"FieldB\": false }";
            await configService.SaveBaseConfigFileContentsAsync(typeof(AlphaPluginConfig), contents, token);

            // Assert
            // Directory and file should exist now
            string settingPath = Path.Combine(this.testConfigPath, "Config", "AlphaPluginConfig.BaseSettings.json"); ;

            Assert.IsTrue(File.Exists(settingPath));
            Assert.AreEqual(contents, File.ReadAllText(settingPath));
        }

        [TestMethod]
        public async Task GenericOverloadsWorkAsExpected()
        {
            if (this.loggerMock == null || this.loggerMock.Object == null || this.testConfigPath as string == null || this.tokenService == null)
                return;
            // Arrange
            ConfigService configService = new ConfigService(this.loggerMock.Object, this.tokenService, this.testConfigPath, this.testConfigPath);
            Token token = this.tokenService.CreateToken();
            configService.RegisterConfig(typeof(AlphaPluginConfig), readToken: token, writeToken: token);

            string contents = "{ \"FieldA\": 99, \"FieldB\": true }";

            // Act
            await configService.SaveBaseConfigFileContentsAsync<AlphaPluginConfig>(contents, token);
            string actual = configService.GetBaseConfigFileContents<AlphaPluginConfig>(token);

            // Assert
            Assert.AreEqual(contents, actual);
        }

        #endregion
    }
}