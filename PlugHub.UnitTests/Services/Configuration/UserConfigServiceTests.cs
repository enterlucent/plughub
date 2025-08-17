using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Models;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Services.Configuration.Providers;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;

namespace PlugHub.UnitTests.Services.Configuration
{
    [TestClass]
    public sealed class UserConfigServiceTests
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private TokenService? tokenService;
        private ConfigService? configService;
        private UserConfigServiceParams userParams = new();
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

            this.userParams
                = new UserConfigServiceParams(
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

        #endregion

        #region ConfigServiceTests: Accessors

        #endregion

        #region ConfigServiceTests: Mutators

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

            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.json");
            this.msTestHelpers.CreateTempFile("{\"FieldA\": 80, \"FieldB\": true}", configPath);

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.userParams);

            if (initialUserValue.HasValue)
                this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", initialUserValue.Value, this.tokenSet);

            this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldA", setValue, this.tokenSet);

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

            int currentValue = this.configService!.GetSetting<int>(typeof(UnitTestAConfig), "FieldA", this.tokenSet);
            Assert.AreEqual(expectedNewValue, currentValue, $"Current value mismatch in scenario: {scenario}");
        }

        [TestMethod]
        [TestCategory("UserOverrides")]
        public void UserOverride_ListProperty_OverridesDefaultValue()
        {
            // Arrange
            List<UnitTestConfigItem> defaultList = [new UnitTestConfigItem("Default1"), new UnitTestConfigItem("Default2"), new UnitTestConfigItem("Default3")];
            List<UnitTestConfigItem> userList = [new UnitTestConfigItem("UserOne"), new UnitTestConfigItem("UserTwo")];

            this.configService!.RegisterConfigs([typeof(UnitTestAConfig)], this.userParams);
            this.configService!.SetDefault(typeof(UnitTestAConfig), "SampleList", defaultList, this.tokenSet);

            List<UnitTestConfigItem> initialResult =
                this.configService!
                    .GetSetting<List<UnitTestConfigItem>>(typeof(UnitTestAConfig), "SampleList", this.tokenSet);

            CollectionAssert.AreEqual(defaultList, initialResult, "Should return default value initially.");

            // Act
            this.configService!.SetSetting(typeof(UnitTestAConfig), "SampleList", userList, this.tokenSet);

            List<UnitTestConfigItem> result = this.configService!.GetSetting<List<UnitTestConfigItem>>(typeof(UnitTestAConfig), "SampleList", this.tokenSet);

            // Assert
            CollectionAssert.AreEqual(userList, result, "User override should take precedence over the default list.");

            List<UnitTestConfigItem> defaultCheck = this.configService!.GetDefault<List<UnitTestConfigItem>>(typeof(UnitTestAConfig), "SampleList", this.tokenSet);
            CollectionAssert.AreEqual(defaultList, defaultCheck, "Default value should still be preserved.");
        }


        [TestMethod]
        [TestCategory("UserOverrides")]
        public void UserOverride_DictionaryProperty_OverridesDefaultValue()
        {
            // Arrange
            Dictionary<string, UnitTestConfigItem> defaultDict = new()
            {
                { "Key1", new UnitTestConfigItem("DefaultVal1") },
                { "Key2", new UnitTestConfigItem("DefaultVal2") },
                { "Key3", new UnitTestConfigItem("DefaultVal3") }
            };
            Dictionary<string, UnitTestConfigItem> userDict = new()
            {
                { "Key1", new UnitTestConfigItem("UserVal1") },
                { "Key3", new UnitTestConfigItem("UserVal3") },
                { "KeyX", new UnitTestConfigItem("UserValX") }
            };

            this.configService!.RegisterConfigs([typeof(UnitTestBConfig)], this.userParams);

            // Act
            this.configService!.SetSetting(typeof(UnitTestBConfig), "SampleDictionary", userDict, this.tokenSet);

            Dictionary<string, UnitTestConfigItem> result =
                this.configService!.GetSetting<Dictionary<string, UnitTestConfigItem>>(typeof(UnitTestBConfig), "SampleDictionary", this.tokenSet);

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("UserValX", result["KeyX"].Name);
            Assert.AreEqual("UserVal1", result["Key1"].Name);
            Assert.AreEqual("UserVal3", result["Key3"].Name);
        }


        #endregion

        #region ConfigServiceTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveSettingsAsync_ValidConfigType_SavesToFiles()
        {
            // Arrange
            string configPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestAConfig.user.json");

            // Act
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.userParams);
            this.configService!.SetSetting(typeof(UnitTestAConfig), "FieldB", true, this.tokenSet);

            await this.configService!.SaveSettingsAsync(typeof(UnitTestAConfig), this.tokenSet);

            // Assert
            Assert.IsTrue(File.Exists(configPath), "User settings file should be created");
            string userJson = File.ReadAllText(configPath);
            Assert.IsTrue(userJson.Contains("\"FieldB\": true") || userJson.Contains("\"FieldB\":true"),
                "User settings should contain test value");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveSettingsAsync_EmptySettings_SavesValidJson()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestBConfig), this.userParams);

            string defaultPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestBConfig.json");
            string userPath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestBConfig.user.json");

            // Act
            await this.configService!.SaveSettingsAsync(typeof(UnitTestBConfig), writeToken: this.userParams.Write);

            string defaultJson = File.ReadAllText(defaultPath);
            string userJson = File.ReadAllText(userPath);

            // Assert
            Assert.AreEqual(typeof(UnitTestBConfig).SerializeToJson(), defaultJson.Trim(), "Empty user settings should save the default");
            Assert.AreEqual("{}", userJson.Trim(), "Empty deafult settings should save as {}");
        }

        #endregion

        #region ConfigServiceTests: Events

        #endregion

        #region ConfigServiceTests: Security

        #endregion

        #region ConfigServiceTests: Concurrency

        #endregion
    }
}