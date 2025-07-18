using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PlugHub.Models;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using System.Text.Json;

namespace PlugHub.UnitTests.Services.Configuration
{
    [TestClass]
    public sealed class FileConfigServiceTests
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private TokenService? tokenService;
        private ConfigService? configService;
        private FileConfigServiceParams fileParams = new();
        private Token ownerToken;
        private Token readToken;
        private Token writeToken;
        private ITokenSet tokenSet = new TokenSet();


        internal class UnitTestConfigItem(string name = "Unknown")
        {
            public UnitTestConfigItem() : this("Unknown") { }
            public string Name { get; set; } = name;
        }

        internal class UnitTestAConfig
        {
            public int FieldA { get; set; } = 50;
            public bool FieldB { get; set; } = false;
            public float FieldC { get; } = 2.71828f;
            public List<UnitTestConfigItem> SampleList { get; set; } =
            [
                new UnitTestConfigItem("TestValueA1"),
                new UnitTestConfigItem("TestValueA2"),
                new UnitTestConfigItem("TestValueA3")
            ];

        }
        internal class UnitTestBConfig
        {
            public required string FieldA { get; set; } = "plughub";
            public int FieldB { get; set; } = 100;
            public float FieldC { get; } = 3.14f;

            public Dictionary<string, UnitTestConfigItem> SampleDictionary { get; set; } =
                new Dictionary<string, UnitTestConfigItem>
                {
                    { "Key1", new UnitTestConfigItem("DictValue1") },
                    { "Key2", new UnitTestConfigItem("DictValue2") },
                    { "Key3", new UnitTestConfigItem("DictValue3") }
                };
        }


        [TestInitialize]
        public void Setup()
        {
            this.tokenService = new TokenService(new NullLogger<ITokenService>());

            this.ownerToken = this.tokenService.CreateToken();
            this.readToken = this.tokenService.CreateToken();
            this.writeToken = this.tokenService.CreateToken();
            this.tokenSet = this.tokenService.CreateTokenSet(this.ownerToken, this.readToken, this.writeToken);

            this.fileParams
                = new FileConfigServiceParams(
                    Owner: this.ownerToken,
                    Read: this.readToken,
                    Write: this.writeToken);

            this.configService = new ConfigService(
                [
                    new FileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService),
                    new UserFileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService),
                ],
                [],
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
            UserConfigServiceParams attackerParams =
                new(Owner: this.tokenService!.CreateToken(),
                    Read: this.tokenService!.CreateToken(),
                    Write: this.tokenService!.CreateToken());

            this.configService!.RegisterConfigs([typeof(UnitTestAConfig)], this.fileParams);

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() =>
                this.configService!.RegisterConfigs([typeof(UnitTestAConfig)], attackerParams));
        }


        [TestMethod]
        [TestCategory("Registration")]
        public void RegisterConfigs_ValidTypes_RegistersSuccessfully()
        {
            // Arrange
            List<Type> configTypes = [typeof(UnitTestAConfig), typeof(UnitTestBConfig)];

            string typeAlphaConfigPath =
                Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.json");
            string typeBetaConfigPath =
                Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestBConfig.json");

            // Act
            this.msTestHelpers.CreateTempFile("{\r\n  \"FieldA\": 80,\r\n  \"FieldB\": true\r\n}", typeAlphaConfigPath);
            this.msTestHelpers.CreateTempFile("{\r\n  \"FieldA\": \"Yazza!\",\r\n  \"FieldB\": 60\r\n}", typeBetaConfigPath);

            this.configService!.RegisterConfigs(configTypes, this.fileParams);

            // Assert
            Assert.AreEqual(80, this.configService!.GetSetting<int>(typeof(UnitTestAConfig), "FieldA", this.tokenSet));
            Assert.AreEqual("Yazza!", this.configService!.GetSetting<string>(typeof(UnitTestBConfig), "FieldA", this.tokenSet));
        }


        [TestMethod]
        [TestCategory("Registration")]
        public void RegisterConfigs_InvalidConfig_ThrowsIOException()
        {
            // Arrnage
            List<Type> configTypes = [typeof(UnitTestAConfig)];

            string typeAlphaConfigPath =
                Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.json");

            // Act
            this.msTestHelpers.CreateTempFile("INVALID_JSON", typeAlphaConfigPath);

            // Assert
            IOException ex = Assert.ThrowsException<IOException>(() =>
                this.configService!.RegisterConfigs(configTypes, this.fileParams));

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
                Path.Combine(this.msTestHelpers.TempDirectory!, "UnitTestAConfig.json");

            if (duringRegistration)
            {
                // Act
                this.msTestHelpers.CreateTempFile("INVALIDJSON", typeConfigPath);

                // Assert
                IOException ex = Assert.ThrowsException<IOException>(() =>
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams));
                StringAssert.Contains(ex.Message, "Failed to build");
            }
            else
            {
                // Act
                this.msTestHelpers.CreateTempFile("{ \"FieldA\": 100 }", typeConfigPath);
                this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);
                this.msTestHelpers.CreateTempFile("INVALIDJSON", typeConfigPath);

                // Assert
                UnitTestAConfig config =
                    (UnitTestAConfig)this.configService!.GetConfigInstance(typeof(UnitTestAConfig), this.tokenSet);
                Assert.AreEqual(100, config.FieldA); Assert.IsFalse(config.FieldB);
            }
        }


        [TestMethod]
        [TestCategory("Registration")]
        public void UnregisterConfig_ValidType_RemovesConfigAndSettings()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Act
            object obj = this.configService!.GetConfigInstance(typeof(UnitTestAConfig), readToken: this.fileParams.Read);
            this.configService!.UnregisterConfig(typeof(UnitTestAConfig), this.tokenSet);

            // Assert
            Assert.IsInstanceOfType<UnitTestAConfig>(obj);

            Assert.ThrowsException<KeyNotFoundException>(() =>
                this.configService!.GetConfigInstance(typeof(UnitTestAConfig), this.tokenSet));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void UnregisterConfigs_MultipleTypes_RemovesAll()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();

            Type[] types = [typeof(UnitTestAConfig), typeof(UnitTestBConfig)];

            // Act
            this.configService!.RegisterConfigs(types, this.fileParams);
            this.configService!.UnregisterConfigs(types, this.tokenSet);

            // Assert
            foreach (Type type in types)
                Assert.ThrowsException<KeyNotFoundException>(() =>
                        this.configService!.GetConfigInstance(type, this.tokenSet));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void UnregisterConfig_InvalidToken_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            Token token = this.tokenService!.CreateToken();
            Token attackerToken = this.tokenService.CreateToken();

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                this.configService!.UnregisterConfig(typeof(UnitTestAConfig), attackerToken));
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
            KeyNotFoundException ex = Assert.ThrowsException<KeyNotFoundException>(() =>
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

            IConfiguration envConfig = ConfigService.GetEnvConfig();
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

            IConfiguration envConfig1 = ConfigService.GetEnvConfig();
            string? actualValue1 = envConfig1[testKey];
            Assert.AreEqual(initialValue, actualValue1);

            Environment.SetEnvironmentVariable(testKey, updatedValue);

            IConfiguration envConfig2 = ConfigService.GetEnvConfig();
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
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);
            this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", "test-value", this.tokenSet);

            if (useValidToken)
            {
                string? result = this.configService!.GetSetting<string>(typeof(UnitTestAConfig), "FieldA", this.tokenSet);

                Assert.AreEqual(expectedValue, result);
            }
            else
            {
                Assert.ThrowsException<UnauthorizedAccessException>(() =>
                    this.configService!.GetSetting<string>(typeof(UnitTestAConfig), "FieldA", readToken: token)
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
            this.configService!.RegisterConfigs([typeof(UnitTestAConfig)], this.fileParams);
            this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", 85, this.tokenSet);

            int value = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), "FieldA", this.tokenSet);

            // Assert
            Assert.AreEqual(85, value);
            Assert.IsTrue(eventFired, "Setting changes should fire events");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void SetSetting_UnregisteredType_ThrowsInvalidOperationException()
        {
            KeyNotFoundException ex = Assert.ThrowsException<KeyNotFoundException>(() =>
                this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", 80));

            StringAssert.Contains(ex.Message, "Configuration for");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void SetSetting_TypeConversion_HandlesCorrectlyAndFiresEvent()
        {
            // Arrange
            bool eventFired = false;
            this.configService!.SettingChanged += (s, e) => eventFired = true;

            // Act
            this.configService!.RegisterConfigs([typeof(UnitTestAConfig)], this.fileParams);
            this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", 80, this.tokenSet);
            this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", "90", this.tokenSet);

            // Assert
            string value = this.configService!.GetSetting<string>(typeof(UnitTestAConfig), "FieldA", this.tokenSet);

            Assert.IsTrue(eventFired, "Type change should fire event");
            Assert.AreEqual("90", value);
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void GetBaseConfigFileContents_FileNotFound_ThrowsInvalidOperationException()
        {
            // Arrnage
            Token token = this.tokenService!.CreateToken();

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestBConfig), this.fileParams);

            // Assert
            Assert.ThrowsException<KeyNotFoundException>(() =>
                this.configService!.GetDefaultConfigFileContents(typeof(UnitTestAConfig), this.tokenSet));
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void SetSetting_ListProperty_UpdatesListAndFiresEvent()
        {
            // Arrange
            bool eventFired = false;
            this.configService!.SettingChanged += (s, e) => eventFired = true;
            List<string> testList = ["one", "two", "three"];

            // Act
            this.configService!.RegisterConfigs([typeof(UnitTestAConfig)], this.fileParams);
            this.configService!.SetSetting(typeof(UnitTestAConfig), "SampleList", testList, this.tokenSet);

            List<string> value = this.configService!.GetSetting<List<string>>(typeof(UnitTestAConfig), "SampleList", this.tokenSet);

            // Assert
            CollectionAssert.AreEqual(testList, value);
            Assert.IsTrue(eventFired, "Setting changes should fire events");
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public void SetSetting_BindsDictionaryPropertyCorrectly()
        {
            // Arrange
            Dictionary<string, UnitTestConfigItem> expectedDict = new()
            {
                { "Key1", new UnitTestConfigItem("NewValue1") },
                { "Key2", new UnitTestConfigItem("NewValue2") },
                { "Key3", new UnitTestConfigItem("NewValue3") }
            };

            this.configService!.RegisterConfigs([typeof(UnitTestBConfig)], this.fileParams);

            // Act
            this.configService!.SetSetting(typeof(UnitTestBConfig), "SampleDictionary", expectedDict, this.tokenSet);
            Dictionary<string, UnitTestConfigItem> actualDict = this.configService!.GetSetting<Dictionary<string, UnitTestConfigItem>>(typeof(UnitTestBConfig), "SampleDictionary", this.tokenSet);

            // Assert
            Assert.AreEqual(3, actualDict.Count);
            Assert.AreEqual("NewValue2", actualDict["Key2"].Name);
        }

        #endregion

        #region ConfigServiceTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveConfigInstance_ValidToken_UpdatesSettings()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Act
            UnitTestAConfig preSave = (UnitTestAConfig)this.configService!.GetConfigInstance(typeof(UnitTestAConfig), this.tokenSet);
            preSave.FieldA = 200;
            preSave.FieldB = true;

            await this.configService!.SaveConfigInstanceAsync(typeof(UnitTestAConfig), preSave, this.tokenSet);

            UnitTestAConfig postSave = (UnitTestAConfig)this.configService!.GetConfigInstance(typeof(UnitTestAConfig), this.tokenSet);

            // Assert
            Assert.AreEqual(200, postSave.FieldA);
            Assert.IsTrue(postSave.FieldB);
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveConfigInstance_NullConfig_ThrowsArgumentNullException()
        {
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () =>
                await this.configService!.SaveConfigInstanceAsync(typeof(UnitTestAConfig), null!, this.tokenSet)
            );
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveSettingsAsync_UnregisteredConfigType_ThrowsNotFoundException()
        {
            KeyNotFoundException ex = await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() =>
                this.configService!.SaveSettingsAsync(typeof(UnitTestAConfig)));

            StringAssert.Contains(ex.Message, "Configuration for UnitTestAConfig is not registered.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task GetBaseConfigFileContents_GenericType_SavesAndRetrievesContentSuccessfully()
        {
            // Arrange
            string contents = "{ \"FieldA\": 99, \"FieldB\": true }";

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            await this.configService!.SaveDefaultConfigFileContentsAsync(typeof(UnitTestAConfig), contents, this.tokenSet);

            string actual = this.configService!.GetDefaultConfigFileContents(typeof(UnitTestAConfig), this.tokenSet);

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
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            if (deleteDirectory && Directory.Exists(this.msTestHelpers.TempDirectory))
                Directory.Delete(this.msTestHelpers.TempDirectory, recursive: true);
            else
                Directory.CreateDirectory(this.msTestHelpers.TempDirectory);

            string contents = "{ \"FieldA\": 1, \"FieldB\": false }";
            string settingPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.json");

            // Act
            await this.configService!.SaveDefaultConfigFileContentsAsync(typeof(UnitTestAConfig), contents, this.tokenSet);

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

            UserConfigServiceParams userConfigServiceParams =
                new(Owner: this.ownerToken, Read: this.readToken, Write: this.writeToken, ReloadOnChange: true);

            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.json");

            this.msTestHelpers.CreateTempFile("{\"FieldA\": 80, \"FieldB\": true}", configPath);
            this.configService!.RegisterConfigs(configTypes, userConfigServiceParams);

            TaskCompletionSource<bool> reloadCompleted = new();

            this.configService!.ConfigReloaded += (sender, e) =>
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

            Assert.AreEqual(70, this.configService!.GetSetting<int>(typeof(UnitTestAConfig), "FieldA", this.tokenSet));
        }

        [TestMethod]
        [TestCategory("Events")]
        public async Task OnConfigChanged_MultipleReloads_ProcessesSequentially()
        {
            // Arrange
            List<Type> configTypes = [typeof(UnitTestAConfig)];

            UserConfigServiceParams userConfigServiceParams =
                new(Owner: this.ownerToken, Read: this.readToken, Write: this.writeToken, ReloadOnChange: true);

            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.json");
            this.msTestHelpers.CreateTempFile("{\"FieldA\": 80}", configPath);
            this.configService!.RegisterConfigs(configTypes, userConfigServiceParams);

            int reloadCount = 0;
            List<int> data = [];

            Queue<TaskCompletionSource<bool>> reloadQueue = new();

            this.configService!.ConfigReloaded += (s, e) =>
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

                data.Add(this.configService!.GetSetting<int>(typeof(UnitTestAConfig), "FieldA", this.tokenSet));
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
            UserConfigServiceParams userConfigServiceParams =
                new(Owner: this.ownerToken, Read: this.readToken, Write: this.writeToken, ReloadOnChange: true);

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), userConfigServiceParams);

            int initial = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.tokenSet);

            string defaultPath = Path.Combine(this.msTestHelpers.TempDirectory, $"{nameof(UnitTestAConfig)}.json");

            // Corrupt the file that the watcher is monitoring.
            this.msTestHelpers.CreateTempFile("}{ not: json", defaultPath);

            // Give the watcher a moment to react.
            await Task.Delay(800);

            int after = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.tokenSet);

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
            ITokenSet invalidTokenSet = this.tokenService!.CreateTokenSet();

            switch (operationType)
            {
                case "SaveConfigInstance":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);
                    UnitTestAConfig updatedConfig = new() { FieldA = 200, FieldB = true };

                    // Assert
                    await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
                        this.configService!.SaveConfigInstanceAsync(typeof(UnitTestAConfig), updatedConfig, invalidTokenSet));
                    break;

                case "GetBaseConfigFileContents":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);
                    await this.configService!.SaveDefaultConfigFileContentsAsync(typeof(UnitTestAConfig), "{}", this.tokenSet);

                    // Assert
                    Assert.ThrowsException<UnauthorizedAccessException>(() =>
                        this.configService!.GetDefaultConfigFileContents(typeof(UnitTestAConfig), invalidTokenSet));
                    break;

                case "SaveDefaultConfigFileContents":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestBConfig), this.fileParams);

                    // Assert
                    await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() =>
                        this.configService!.SaveDefaultConfigFileContentsAsync(typeof(UnitTestBConfig), "{}", invalidTokenSet));
                    break;

                case "SetSetting":
                    // Act
                    this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

                    // Assert
                    Assert.ThrowsException<UnauthorizedAccessException>(() =>
                        this.configService!.SetSetting(typeof(UnitTestAConfig), "ApiKey", "new-value", invalidTokenSet));
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

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IEnumerable<Task> writers = Enumerable.Range(0, writerTasks).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    this.configService!.SetSetting(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), i, this.tokenSet);
            }));

            IEnumerable<Task> readers = Enumerable.Range(0, readerTasks).Select(_ => Task.Run(() =>
            {
                for (int i = 0; i < iterations; i++)
                    _ = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.tokenSet);
            }));

            await Task.WhenAll(writers.Concat(readers));

            // Final sanity read – must succeed without throwing.
            _ = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), this.tokenSet);
        }

        #endregion
    }
}