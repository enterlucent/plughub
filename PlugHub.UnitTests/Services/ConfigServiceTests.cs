using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Services;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlugHub.UnitTests.Services
{
    [TestClass]
    public sealed class ConfigServiceTests
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private TokenService? tokenService;
        private ConfigService? configService;
        private Token readToken;
        private Token writeToken;


        internal class UnitTestAConfig
        {
            public int FieldA { get; set; } = 50;
            public bool FieldB { get; set; } = false;
            public float FieldC { get; } = 2.71828f;
        }
        internal class UnitTestBConfig
        {
            public required string FieldA { get; set; } = "plughub";
            public int FieldB { get; set; } = 100;
            public float FieldC { get; } = 3.14f;
        }


        [TestInitialize]
        public void Setup()
        {
            this.tokenService = new TokenService(new NullLogger<TokenService>());

            this.readToken = this.tokenService.CreateToken();
            this.writeToken = this.tokenService.CreateToken();

            this.configService = new(
                new NullLogger<IConfigService>(),
                this.tokenService,
                this.msTestHelpers.TempDirectory,
                this.msTestHelpers.TempDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (this.configService != null)
                ((IDisposable)this.configService).Dispose();

            this.msTestHelpers!.Dispose();
        }


        #region ConfigServiceTests: Registration

        [TestMethod]
        [TestCategory("Registration")]
        public void RegisterConfigs_WithExistingRegistration_InvalidOperationThrows()
        {
            // Arrange
            Token ownerToken = this.tokenService!.CreateToken();
            Token attackerToken = this.tokenService!.CreateToken();

            this.configService!.RegisterConfigs([typeof(UnitTestAConfig)], writeToken: ownerToken);

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() =>
                this.configService.RegisterConfigs([typeof(UnitTestAConfig)], writeToken: attackerToken));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void RegisterConfigs_ValidTypes_RegistersSuccessfully()
        {
            // Arrange
            List<Type> configTypes = [typeof(UnitTestAConfig), typeof(UnitTestBConfig)];

            string typeAlphaConfigPath =
                Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.DefaultSettings.json");
            string typeBetaConfigPath =
                Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestBConfig.UserSettings.json");

            // Act
            this.msTestHelpers.CreateTempFile("{\r\n  \"FieldA\": 80,\r\n  \"FieldB\": true\r\n}", typeAlphaConfigPath);
            this.msTestHelpers.CreateTempFile("{\r\n  \"FieldA\": \"Yazza!\",\r\n  \"FieldB\": 60\r\n}", typeBetaConfigPath);

            this.configService!.RegisterConfigs(configTypes);

            // Assert
            Assert.AreEqual(80, this.configService.GetSetting<int>(typeof(UnitTestAConfig), "FieldA"));
            Assert.AreEqual("Yazza!", this.configService.GetSetting<string>(typeof(UnitTestBConfig), "FieldA"));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void RegisterConfigs_InvalidConfig_ThrowsIOException()
        {
            // Arrnage
            List<Type> configTypes = [typeof(UnitTestAConfig)];

            string typeAlphaConfigPath =
                Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.DefaultSettings.json");

            // Act
            this.msTestHelpers.CreateTempFile("INVALID_JSON", typeAlphaConfigPath);

            // Assert
            IOException ex = Assert.ThrowsException<IOException>(() =>
                this.configService!.RegisterConfigs(configTypes));

            Assert.IsInstanceOfType(ex, typeof(IOException));
        }

        [TestCategory("Registration")]
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void RegisterConfig_InvalidJson_ThrowsIOException_ThenGetConfigInstance_ReturnsDefault(bool duringRegistration)
        {
            // Arrange
            string typeConfigPath =
                Path.Combine(this.msTestHelpers.TempDirectory!, "UnitTestAConfig.DefaultSettings.json");

            Token token = this.tokenService!.CreateToken();

            if (duringRegistration)
            {
                // Act
                this.msTestHelpers.CreateTempFile("INVALIDJSON", typeConfigPath);

                // Assert
                IOException ex = Assert.ThrowsException<IOException>(() =>
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), readToken: token));
                StringAssert.Contains(ex.Message, "Failed to build");
            }
            else
            {
                // Act
                this.msTestHelpers.CreateTempFile("{ \"FieldA\": 100 }", typeConfigPath);
                this.configService!.RegisterConfig(typeof(UnitTestAConfig), readToken: token);
                this.msTestHelpers.CreateTempFile("INVALIDJSON", typeConfigPath);

                // Assert
                UnitTestAConfig config =
                    (UnitTestAConfig)this.configService.GetConfigInstance(typeof(UnitTestAConfig), readToken: token);
                Assert.AreEqual(100, config.FieldA); Assert.IsFalse(config.FieldB);
            }
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void UnregisterConfig_ValidType_RemovesConfigAndSettings()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), ownerToken: token, readToken: token, writeToken: token);

            // Act
            object obj = this.configService.GetConfigInstance(typeof(UnitTestAConfig), readToken: token);
            this.configService.UnregisterConfig(typeof(UnitTestAConfig), token);

            // Assert
            Assert.IsInstanceOfType<UnitTestAConfig>(obj);

            Assert.ThrowsException<ConfigTypeNotFoundException>(() =>
                this.configService.GetConfigInstance(typeof(UnitTestAConfig), token));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void UnregisterConfigs_MultipleTypes_RemovesAll()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            Type[] types = [typeof(UnitTestAConfig), typeof(UnitTestBConfig)];

            // Act
            this.configService!.RegisterConfigs(types, ownerToken: token);
            this.configService.UnregisterConfigs(types, token);

            // Assert
            foreach (Type type in types)
                Assert.ThrowsException<ConfigTypeNotFoundException>(() =>
                        this.configService.GetConfigInstance(type, token));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void UnregisterConfig_InvalidToken_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();
            Token attackerToken = this.tokenService.CreateToken();

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), ownerToken: token);

            // Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                this.configService.UnregisterConfig(typeof(UnitTestAConfig), attackerToken));
        }


        #endregion

        #region ConfigServiceTests: Accessors

        [TestMethod]
        [TestCategory("Accessors")]
        public void GetConfigInstance_UnregisteredType_ThrowsInvalidOperationException()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            // Act & Assert
            ConfigTypeNotFoundException ex = Assert.ThrowsException<ConfigTypeNotFoundException>(() =>
                this.configService!.GetConfigInstance(typeof(UnitTestAConfig), token)
            );

            StringAssert.Contains(ex.Message, "not registered");
        }

        [TestMethod]
        [TestCategory("Accessors")]
        public void GetEnvConfig_ValidEnvironmentVariable_ReturnsExpectedValue()
        {
            string testKey = "TEST_ENV_VAR_" + Guid.NewGuid().ToString("N");
            string expectedValue = "test_value_123";
            Environment.SetEnvironmentVariable(testKey, expectedValue);

            IConfiguration envConfig = this.configService!.GetEnvConfig();
            string? actualValue = envConfig[testKey];

            Environment.SetEnvironmentVariable(testKey, null);

            Assert.AreEqual(expectedValue, actualValue);
        }

        [TestMethod]
        [TestCategory("Accessors")]
        public void GetEnvConfig_EnvironmentVariableChangedAfterRebuild_ReflectsUpdatedValue()
        {
            string testKey = "TEST_ENV_VAR_" + Guid.NewGuid().ToString("N");
            string initialValue = "initial_value";
            string updatedValue = "updated_value";
            Environment.SetEnvironmentVariable(testKey, initialValue);

            IConfiguration envConfig1 = this.configService!.GetEnvConfig();
            string? actualValue1 = envConfig1[testKey];
            Assert.AreEqual(initialValue, actualValue1);

            Environment.SetEnvironmentVariable(testKey, updatedValue);

            IConfiguration envConfig2 = this.configService.GetEnvConfig();
            string? actualValue2 = envConfig2[testKey];

            Environment.SetEnvironmentVariable(testKey, null);

            Assert.AreEqual(updatedValue, actualValue2);
        }

        [TestCategory("Accessors")]
        [DataTestMethod]
        [DataRow(true, "test-value")]
        [DataRow(false, null)]
        public void GetSetting_ValidAndInvalidToken_EnforcesAccessAndThrowsWhenUnauthorized(bool useValidToken, string? expectedValue)
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), readToken: token, writeToken: token);
            this.configService.SetSetting(typeof(UnitTestAConfig), "FieldA", "test-value", writeToken: token);

            // Assert
            Token? accessToken = useValidToken ? token : Token.Blocked;

            if (useValidToken)
            {
                string? result = this.configService.GetSetting<string>(typeof(UnitTestAConfig), "FieldA", readToken: accessToken);

                Assert.AreEqual(expectedValue, result);
            }
            else
            {
                Assert.ThrowsException<UnauthorizedAccessException>(() =>
                    this.configService.GetSetting<string>(typeof(UnitTestAConfig), "FieldA", accessToken)
                );
            }
        }

        #endregion

        #region ConfigServiceTests: Mutators

        [TestMethod]
        [TestCategory("Mutators")]
        public void SetSetting_RegisteredType_UpdatesValueAndFiresEvent()
        {
            // Arrange
            bool eventFired = false;
            this.configService!.SettingChanged += (s, e) => eventFired = true;

            // Act
            this.configService.RegisterConfigs([typeof(UnitTestAConfig)], writeToken: Token.Public);
            this.configService.SetSetting(typeof(UnitTestAConfig), "FieldA", 85);

            int value = this.configService.GetSetting<int>(typeof(UnitTestAConfig), "FieldA");

            // Assert
            Assert.AreEqual(85, value);
            Assert.IsTrue(eventFired, "Setting changes should fire events");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void SetSetting_UnregisteredType_ThrowsInvalidOperationException()
        {
            ConfigTypeNotFoundException ex = Assert.ThrowsException<ConfigTypeNotFoundException>(() =>
                this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", 80));

            StringAssert.Contains(ex.Message, "Type configuration for");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void SetSetting_TypeConversion_HandlesCorrectlyAndFiresEvent()
        {
            // Arrange
            bool eventFired = false;
            this.configService!.SettingChanged += (s, e) => eventFired = true;

            // Act
            this.configService.RegisterConfigs([typeof(UnitTestAConfig)], writeToken: Token.Public);
            this.configService.SetSetting(typeof(UnitTestAConfig), "FieldA", 80);
            this.configService.SetSetting(typeof(UnitTestAConfig), "FieldA", "90");

            // Assert
            Assert.IsTrue(eventFired, "Type change should fire event");
            Assert.AreEqual("90", this.configService.GetSetting<string>(typeof(UnitTestAConfig), "FieldA"));
        }

        [TestCategory("Mutators")]
        [DataTestMethod]
        [DataRow("NewUserOverride", null, 90, 80, 90, true)]
        [DataRow("RevertToBaseValue", 85, 80, 85, 80, true)]
        [DataRow("ChangeExistingOverride", 75, 85, 75, 85, true)]
        public void SetSetting_FieldAWithVariousUserValues_UpdatesCorrectlyAndFiresEventWithExpectedOldAndNewValues
            (string scenario, int? initialUserValue, int setValue, int? expectedOldValue, int expectedNewValue, bool expectEvent)
        {
            // Arrange
            bool eventFired = false;
            ConfigServiceSettingChangeEventArgs? eventArgs = null;
            this.configService!.SettingChanged += (s, e) =>
            {
                eventFired = true;
                eventArgs = e;
            };

            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.DefaultSettings.json");
            this.msTestHelpers.CreateTempFile("{\"FieldA\": 80, \"FieldB\": true}", configPath);

            // Act
            this.configService.RegisterConfig(typeof(UnitTestAConfig), writeToken: Token.Public);

            if (initialUserValue.HasValue)
                this.configService.SetSetting(typeof(UnitTestAConfig), "FieldA", initialUserValue.Value);

            this.configService.SetSetting(typeof(UnitTestAConfig), "FieldA", setValue);

            // Assert
            if (expectEvent)
            {
                Assert.IsTrue(eventFired, $"Event should fire for scenario: {scenario}");
                Assert.IsNotNull(eventArgs, "Event arguments should not be null");
                Assert.AreEqual(typeof(UnitTestAConfig), eventArgs.ConfigType);
                Assert.AreEqual("FieldA", eventArgs.Key);

                Assert.IsNotNull(eventArgs.OldValue, "OldValue should not be null");
                Assert.IsNotNull(eventArgs.NewValue, "NewValue should not be null");

                string expectedOldStr = expectedOldValue.ToString() ?? string.Empty;
                string actualOldStr = eventArgs.OldValue.ToString() ?? string.Empty;
                string expectedNewStr = expectedNewValue.ToString() ?? string.Empty;
                string actualNewStr = eventArgs.NewValue.ToString() ?? string.Empty;

                Assert.AreEqual(expectedOldStr, actualOldStr, "Old value mismatch");
                Assert.AreEqual(expectedNewStr, actualNewStr, "New value mismatch");
            }
            else
            {
                Assert.IsFalse(eventFired, $"Event should not fire for scenario: {scenario}");
            }

            int currentValue = this.configService.GetSetting<int>(typeof(UnitTestAConfig), "FieldA");
            Assert.AreEqual(expectedNewValue, currentValue, $"Current value mismatch in scenario: {scenario}");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void GetBaseConfigFileContents_FileNotFound_ThrowsInvalidOperationException()
        {
            // Arrnage
            Token token = this.tokenService!.CreateToken();

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestBConfig), writeToken: token, readToken: token);

            // Assert
            Assert.ThrowsException<ConfigTypeNotFoundException>(() =>
                this.configService.GetDefaultConfigFileContents(typeof(UnitTestAConfig), token));
        }

        #endregion

        #region ConfigServiceTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveConfigInstance_ValidToken_UpdatesSettings()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), readToken: token, writeToken: token);

            // Act
            UnitTestAConfig preSave = (UnitTestAConfig)this.configService.GetConfigInstance(typeof(UnitTestAConfig), readToken: token);
            preSave.FieldA = 200;
            preSave.FieldB = true;

            await this.configService.SaveConfigInstanceAsync(typeof(UnitTestAConfig), preSave, writeToken: token);

            UnitTestAConfig postSave = (UnitTestAConfig)this.configService.GetConfigInstance(typeof(UnitTestAConfig), readToken: token);

            // Assert
            Assert.AreEqual(200, postSave.FieldA);
            Assert.IsTrue(postSave.FieldB);
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveConfigInstance_NullConfig_ThrowsArgumentNullException()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), writeToken: token);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                await this.configService.SaveConfigInstanceAsync(typeof(UnitTestAConfig), null!, token)
            );
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveSettingsAsync_ValidConfigType_SavesToFiles()
        {
            // Arrange
            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.UserSettings.json");

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), writeToken: Token.Public);
            this.configService.SetSetting(typeof(UnitTestAConfig), "FieldB", true);

            await this.configService.SaveSettingsAsync(typeof(UnitTestAConfig));

            // Assert
            Assert.IsTrue(File.Exists(configPath), "User settings file should be created");
            string userJson = File.ReadAllText(configPath);
            Assert.IsTrue(userJson.Contains("\"FieldB\": true") || userJson.Contains("\"FieldB\":true"),
                "User settings should contain test value");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveSettingsAsync_UnregisteredConfigType_ThrowsNotFoundException()
        {
            ConfigTypeNotFoundException ex = await Assert.ThrowsExceptionAsync<ConfigTypeNotFoundException>(() =>
                this.configService!.SaveSettingsAsync(typeof(UnitTestAConfig)));

            StringAssert.Contains(ex.Message, "Type configuration for UnitTestAConfig was not registered.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveSettingsAsync_EmptySettings_SavesValidJson()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestBConfig), writeToken: Token.Public);

            string defaultPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestBConfig.DefaultSettings.json");
            string userPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestBConfig.UserSettings.json");

            // Act
            await this.configService.SaveSettingsAsync(typeof(UnitTestBConfig));

            string defaultJson = File.ReadAllText(defaultPath);
            string userJson = File.ReadAllText(userPath);

            // Assert
            Assert.AreEqual(typeof(UnitTestBConfig).SerializeToJson(new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.WriteAsString,
            }), defaultJson.Trim(), "Empty user settings should save the default");

            Assert.AreEqual("{}", userJson.Trim(), "Empty deafult settings should save as {}");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task GetBaseConfigFileContents_GenericType_SavesAndRetrievesContentSuccessfully()
        {
            // Arrange
            ITokenSet tokenSet = this.tokenService!.CreateTokenSet();
            string contents = "{ \"FieldA\": 99, \"FieldB\": true }";

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), tokenSet);

            await this.configService.SaveDefaultConfigFileContentsAsync(typeof(UnitTestAConfig), contents, tokenSet);

            string actual = this.configService.GetDefaultConfigFileContents(typeof(UnitTestAConfig), tokenSet);

            // Assert
            Assert.AreEqual(contents, actual);
        }

        [TestCategory("Persistence")]
        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task SaveDefaultConfigFileContentsAsync_MissingDirectory_CreatesDirectoryAndSavesFile(bool deleteDirectory)
        {
            // Arrange
            ITokenSet tokenSet = this.tokenService!.CreateTokenSet();
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), tokenSet);

            if (deleteDirectory && Directory.Exists(this.msTestHelpers.TempDirectory))
                Directory.Delete(this.msTestHelpers.TempDirectory, recursive: true);
            else
                Directory.CreateDirectory(this.msTestHelpers.TempDirectory);

            string contents = "{ \"FieldA\": 1, \"FieldB\": false }";
            string settingPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.DefaultSettings.json");

            // Act
            await this.configService.SaveDefaultConfigFileContentsAsync(typeof(UnitTestAConfig), contents, tokenSet);

            // Assert
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(settingPath)), "Config directory should exist");
            Assert.IsTrue(File.Exists(settingPath), "Config file should be created");
            Assert.AreEqual(contents, File.ReadAllText(settingPath), "File content should match");
        }

        #endregion

        #region ConfigServiceTests: Events

        [TestMethod]
        [TestCategory("Events")]
        public async Task OnConfigChanged_ValidType_ReloadsSettings()
        {
            // Arrange
            List<Type> configTypes = [typeof(UnitTestAConfig)];

            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.DefaultSettings.json");

            this.msTestHelpers.CreateTempFile("{\"FieldA\": 80, \"FieldB\": true}", configPath);
            this.configService!.RegisterConfigs(configTypes, reloadOnChange: true);

            TaskCompletionSource<bool> reloadCompleted = new();

            this.configService.ConfigReloaded += (sender, e) =>
            {
                if (e.ConfigType == typeof(UnitTestAConfig))
                    reloadCompleted.SetResult(true);
            };

            this.msTestHelpers.CreateTempFile("{\"FieldA\": 70, \"FieldB\": true}", configPath);

            // Act
            Task completedTask = await Task.WhenAny(reloadCompleted.Task, Task.Delay(2000));

            // Assert
            if (completedTask != reloadCompleted.Task)
                Assert.Fail("TypeConfigurationReloaded event not raised within timeout");

            Assert.AreEqual(70, this.configService.GetSetting<int>(typeof(UnitTestAConfig), "FieldA"));
        }

        [TestMethod]
        [TestCategory("Events")]
        public async Task OnConfigChanged_MultipleReloads_ProcessesSequentially()
        {
            // Arrange
            List<Type> configTypes = [typeof(UnitTestAConfig)];

            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.DefaultSettings.json");
            this.msTestHelpers.CreateTempFile("{\"FieldA\": 80}", configPath);
            this.configService!.RegisterConfigs(configTypes, reloadOnChange: true);

            int reloadCount = 0;
            List<int> data = [];

            Queue<TaskCompletionSource<bool>> reloadQueue = new();

            this.configService.ConfigReloaded += (s, e) =>
            {
                if (e.ConfigType == typeof(UnitTestAConfig))
                {
                    reloadCount++;

                    if (reloadQueue.Count > 0)
                        reloadQueue.Dequeue().SetResult(true);
                }
            };

            // Act
            for (int i = 0; i < 3; i++)
            {
                TaskCompletionSource<bool> reloadCompletion = new();
                reloadQueue.Enqueue(reloadCompletion);

                int newFieldA = 70 + i;
                this.msTestHelpers.CreateTempFile($"{{\"FieldA\": {newFieldA}}}", configPath);

                await Task.WhenAny(reloadCompletion.Task, Task.Delay(2000));

                data.Add(this.configService.GetSetting<int>(typeof(UnitTestAConfig), "FieldA"));
            }

            // Assert
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(data[i], 70 + i, $"FieldA mismatch after reload {i + 1}");

            Assert.AreEqual(3, reloadCount, "Should have triggered 3 reload events");
        }

        [TestMethod]
        [TestCategory("Events")]
        public async Task BadReload_MalformedJson_DoesNotOverwriteSettings()
        {
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), reloadOnChange: true);

            int initial = this.configService.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.readToken);

            string defaultPath = Path.Combine(this.msTestHelpers.TempDirectory, $"{nameof(UnitTestAConfig)}.DefaultSettings.json");

            // Corrupt the file that the watcher is monitoring.
            this.msTestHelpers.CreateTempFile("}{ not: json", defaultPath);

            // Give the watcher a moment to react.
            await Task.Delay(800);

            int after = this.configService.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.readToken);

            Assert.AreEqual(initial, after,
                "Settings should remain unchanged when a reload encounters malformed JSON.");
        }

        #endregion

        #region ConfigServiceTests: Security

        [TestCategory("Security")]
        [DataTestMethod]
        [DataRow("SaveConfigInstance")]
        [DataRow("GetBaseConfigFileContents")]
        [DataRow("SaveDefaultConfigFileContents")]
        [DataRow("SetSetting")]
        public async Task SecurityCriticalOperations_InvalidToken_ThrowsUnauthorizedAccessException(string operationType)
        {
            // Arrange
            ITokenSet validTokenSet = this.tokenService!.CreateTokenSet();
            ITokenSet invalidTokenSet = this.tokenService.CreateTokenSet();

            switch (operationType)
            {
                case "SaveConfigInstance":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), validTokenSet);
                    UnitTestAConfig updatedConfig = new() { FieldA = 200, FieldB = true };

                    // Assert
                    await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
                        this.configService.SaveConfigInstanceAsync(typeof(UnitTestAConfig), updatedConfig, invalidTokenSet));
                    break;

                case "GetBaseConfigFileContents":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), validTokenSet);
                    await this.configService.SaveDefaultConfigFileContentsAsync(typeof(UnitTestAConfig), "{}", validTokenSet);

                    // Assert
                    Assert.ThrowsException<UnauthorizedAccessException>(() =>
                        this.configService.GetDefaultConfigFileContents(typeof(UnitTestAConfig), invalidTokenSet));
                    break;

                case "SaveDefaultConfigFileContents":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestBConfig), validTokenSet);

                    // Assert
                    await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
                        this.configService.SaveDefaultConfigFileContentsAsync(typeof(UnitTestBConfig), "{}", invalidTokenSet));
                    break;

                case "SetSetting":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), validTokenSet);

                    // Assert
                    Assert.ThrowsException<UnauthorizedAccessException>(() =>
                        this.configService.SetSetting(typeof(UnitTestAConfig), "ApiKey", "new-value", invalidTokenSet));
                    break;
            }
        }

        #endregion

        #region ConfigServiceTests: Concurrency

        [TestMethod]
        [TestCategory("Concurrency")]
        public async Task ConcurrentReadersWriters_DoNotThrow()
        {
            const int iterations = 200;
            const int readerTasks = 6;
            const int writerTasks = 4;

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), writeToken: Token.Public);

            var writers = Enumerable.Range(0, writerTasks).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    this.configService!.SetSetting(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), i, this.writeToken);
            }));

            var readers = Enumerable.Range(0, readerTasks).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    _ = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.readToken);
            }));

            await Task.WhenAll(writers.Concat(readers));

            // Final sanity read – must succeed without throwing.
            _ = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.readToken);
        }

        #endregion
    }
}