using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Accessors.Configuration;
using PlugHub.Models;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Services.Configuration.Providers;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;

namespace PlugHub.UnitTests.Accessors.Configuration
{
    [TestClass]
    public class FileConfigAccessorTests
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private InsecureStorage? secureStorage;
        private TokenService? tokenService;
        private ConfigService? configService;
        private FileConfigServiceParams fileParams = new();
        private EncryptionService? encryptionService;
        private Token ownerToken;
        private Token readToken;
        private Token writeToken;
        private ITokenSet tokenSet = new TokenSet();


        internal class UnitTestAConfig
        {
            public int FieldA { get; set; } = 50;
            public bool FieldB { get; set; } = false;
            public float FieldC { get; } = 2.71828f;
        }
        internal class UnitTestBConfig
        {
            public string FieldA { get; set; } = "plughub";
            public int FieldB { get; set; } = 100;
            public float FieldC { get; } = 3.14f;
        }


        [TestInitialize]
        public void Setup()
        {
            this.tokenService = new TokenService(new NullLogger<ITokenService>());
            this.secureStorage = new InsecureStorage(new NullLogger<ISecureStorage>());
            this.secureStorage.Initialize(this.msTestHelpers.TempDirectory, false, false);
            this.encryptionService = new EncryptionService(new NullLogger<IEncryptionService>(), this.secureStorage);

            this.ownerToken = this.tokenService.CreateToken();
            this.readToken = this.tokenService.CreateToken();
            this.writeToken = this.tokenService.CreateToken();
            this.tokenSet = this.tokenService.CreateTokenSet(this.ownerToken, this.readToken, this.writeToken);

            this.fileParams =
                new FileConfigServiceParams(
                    Owner: this.ownerToken,
                    Read: this.readToken,
                    Write: this.writeToken);

            this.configService = new ConfigService(
                [
                    new FileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService),
                    new UserFileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService),
                ],
                [
                    new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService),
                    new SecureFileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService, this.encryptionService),
                ],
                new NullLogger<IConfigService>(),
                this.tokenService,
                this.msTestHelpers.TempDirectory,
                this.msTestHelpers.TempDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            (this.configService as IDisposable)?.Dispose();

            this.msTestHelpers.Dispose();
        }


        #region ConfigAccessorTests: Registration

        [TestMethod]
        [TestCategory("Registration")]
        public void For_UnregisteredType_Throws()
        {
            // Arrange & Act
            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            // Assert
            Assert.ThrowsException<InvalidOperationException>(() => accessor.For<UnitTestBConfig>());
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void For_RegisteredType_Succeeds()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            // Act
            IConfigAccessorFor<UnitTestAConfig> stronglyTyped = accessor.For<UnitTestAConfig>();

            // Assert
            Assert.IsInstanceOfType<IConfigAccessorFor<UnitTestAConfig>>(stronglyTyped);
        }

        #endregion

        #region ConfigAccessorTests: Accessors

        [TestMethod]
        [TestCategory("Accessors")]
        public void Get_ReturnsDefaultValue()
        {
            //Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> a = accessor.For<UnitTestAConfig>();

            // Act
            int value = a.Get<int>(nameof(UnitTestAConfig.FieldA));

            // Assert
            Assert.AreEqual(50, value);
        }

        [TestMethod]
        [TestCategory("Accessors")]
        public void Get_ReturnsMergedInstanceWithUserOverrides()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);
            this.configService.SetSetting(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), 99, this.ownerToken, this.writeToken);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> aConfig = accessor.For<UnitTestAConfig>();

            // Act
            UnitTestAConfig instance = aConfig.Get();

            // Assert
            Assert.AreEqual(99, instance.FieldA);
            Assert.IsFalse(instance.FieldB);
            Assert.AreEqual(2.71828f, instance.FieldC);
        }

        [TestMethod]
        [TestCategory("Validation")]
        public void Get_WithNullKey_Throws()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Act
            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> config = accessor.For<UnitTestAConfig>();

            // Assert
            Assert.ThrowsException<ArgumentNullException>(() => config.Get<int>(null!));
        }

        #endregion

        #region ConfigAccessorTests: Mutators

        [TestMethod]
        [TestCategory("Mutators")]
        public void Set_ThenGet_ReturnsUpdatedValue()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> a = accessor.For<UnitTestAConfig>();

            // Act
            a.Set(nameof(UnitTestAConfig.FieldA), 123);

            int value = a.Get<int>(nameof(UnitTestAConfig.FieldA));

            // Assert
            Assert.AreEqual(123, value);
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public async Task Save_ModifiedInstance_PersistsChanges()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> aConfig = accessor.For<UnitTestAConfig>();

            UnitTestAConfig editable = aConfig.Get();
            editable.FieldA = 123;
            editable.FieldB = true;

            // Act
            await aConfig.SaveAsync(editable);

            // Assert
            int storedA = this.configService.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), readToken: this.readToken);
            bool storedB = this.configService.GetSetting<bool>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldB), readToken: this.readToken);

            Assert.AreEqual(123, storedA);
            Assert.IsTrue(storedB);

            UnitTestAConfig reloaded = aConfig.Get();
            Assert.AreEqual(123, reloaded.FieldA);
            Assert.IsTrue(reloaded.FieldB);
        }

        [TestMethod]
        [TestCategory("Mutators")]
        public async Task SaveAsync_WithInvalidToken_Throws()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Act
            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenService!.CreateToken(), this.readToken, this.tokenService!.CreateToken());

            IConfigAccessorFor<UnitTestAConfig> aConfig = accessor.For<UnitTestAConfig>();

            UnitTestAConfig edited = aConfig.Get();
            edited.FieldA = 42;

            // Aassert
            await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
                async () => await aConfig.SaveAsync(edited));
        }

        [TestMethod]
        [TestCategory("Validation")]
        public void Set_WithUnknownProperty_Throws()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Act
            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> config = accessor.For<UnitTestAConfig>();

            // Assert
            Assert.ThrowsException<KeyNotFoundException>(() => config.Set("DoesNotExist", 42));
        }

        #endregion

        #region ConfigAccessorTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_PersistsToConfigService()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            // Act
            IConfigAccessorFor<UnitTestAConfig> a = accessor.For<UnitTestAConfig>();

            a.Set(nameof(UnitTestAConfig.FieldA), 777);

            await a.SaveAsync(CancellationToken.None);

            // Assert
            int stored = this.configService.GetSetting<int>(typeof(UnitTestAConfig), nameof(UnitTestAConfig.FieldA), readToken: this.readToken);

            Assert.AreEqual(777, stored);
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_WithCancelledToken_DoesNotPersistTo_Disk()
        {
            // Arrange
            Token read = this.tokenService!.CreateToken();
            Token write = this.tokenService.CreateToken();

            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> a = accessor.For<UnitTestAConfig>();
            a.Set(nameof(UnitTestAConfig.FieldA), 9001);

            string filePath = Path.Combine(
                this.msTestHelpers.TempDirectory,
                $"{nameof(UnitTestAConfig)}.json");

            // Act & Assert
            using (CancellationTokenSource cts = new())
            {
                cts.Cancel();
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                    () => a.SaveAsync(cts.Token),
                    "SaveAsync should propagate a pre-cancelled token.");
            }

            this.configService!.UnregisterConfig(typeof(UnitTestAConfig), this.tokenSet.Owner);
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            a = accessor.For<UnitTestAConfig>();

            Assert.AreEqual(50, a.Get<int>(nameof(UnitTestAConfig.FieldA)),
                "Cancelled SaveAsync must not create or update the settings file on disk.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveSettingsAsync_RoundTripsThroughFreshConfigService()
        {
            // Arrange
            Token read = this.tokenService!.CreateToken();
            Token write = this.tokenService.CreateToken();
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> a = accessor.For<UnitTestAConfig>();
            a.Set(nameof(UnitTestAConfig.FieldA), 424242);

            // Act
            await a.SaveAsync(CancellationToken.None);

            // Assert
            string filePath = Path.Combine(this.msTestHelpers.TempDirectory, $"{nameof(UnitTestAConfig)}.json");
            string json = await File.ReadAllTextAsync(filePath);

            Assert.IsTrue(File.Exists(filePath), "Settings file should be written to disk.");

            StringAssert.Contains(json, "424242", "Persisted JSON should include the updated value.");

            TokenService newTokenService = new(new NullLogger<ITokenService>());

            using (FileConfigService newConfigService = new(new NullLogger<IConfigServiceProvider>(), newTokenService))
            {
                newConfigService.RegisterConfig(typeof(UnitTestAConfig), this.fileParams, this.configService);

                int roundTripped = newConfigService.GetSetting<int>(
                    typeof(UnitTestAConfig),
                    nameof(UnitTestAConfig.FieldA),
                    this.tokenSet);

                Assert.AreEqual(424242, roundTripped,
                    "A fresh ConfigService should read back the value that was flushed to disk.");
            }
        }

        #endregion

        #region ConfigAccessorTests: Security

        [TestMethod]
        [TestCategory("Security")]
        public void Set_WithInvalidWriteToken_Throws()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            // Act
            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenService!.CreateToken(), this.tokenSet.Read, this.tokenService!.CreateToken());

            IConfigAccessorFor<UnitTestAConfig> a = accessor.For<UnitTestAConfig>();

            // Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => a.Set(nameof(UnitTestAConfig.FieldA), 999));
        }

        #endregion

        #region ConfigAccessorTests: Concurrency

        [TestMethod]
        [TestCategory("Concurrency")]
        public async Task Concurrent_SetGet_DoesNotThrowAndValueRemainsValid()
        {
            // Arrange
            this.configService!.RegisterConfig(typeof(UnitTestAConfig), this.fileParams);

            IConfigAccessor accessor = new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!)
                .SetConfigTypes([typeof(UnitTestAConfig)])
                .SetConfigService(this.configService!)
                .SetAccess(this.tokenSet);

            IConfigAccessorFor<UnitTestAConfig> config = accessor.For<UnitTestAConfig>();

            // Act
            const int iterations = 50;
            IEnumerable<Task> tasks = Enumerable.Range(0, iterations).Select(i =>
                Task.Run(() =>
                {
                    config.Set(nameof(UnitTestAConfig.FieldA), i);
                    _ = config.Get<int>(nameof(UnitTestAConfig.FieldA));
                }));

            await Task.WhenAll(tasks);

            // Assert
            int final = config.Get<int>(nameof(UnitTestAConfig.FieldA));

            Assert.IsTrue(final >= 0 && final < iterations, "Final value should be one of the written values.");
        }

        #endregion
    }
}