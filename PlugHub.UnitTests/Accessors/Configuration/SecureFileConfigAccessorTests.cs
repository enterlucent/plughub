using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Accessors.Configuration;
using PlugHub.Models;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Services.Configuration;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;

namespace PlugHub.UnitTests.Accessors.Configuration
{
    [TestClass]
    public class SecureFileConfigAccessorTests
    {
        private readonly MSTestHelpers msTestHelpers = new();

        private TokenService tokenService = null!;
        private ConfigService configService = null!;
        private InsecureStorage secureStorage = null!;
        private EncryptionService encryptionService = null!;
        private IEncryptionContext encryptionContext = null!;
        private IConfigServiceParams? secureParams;

        private ITokenSet tokenSet = new TokenSet();


        internal class UnitTestSecureAConfig
        {
            public required Guid FieldA { get; set; } = Guid.Empty;

            [Secure]
            public string FieldB { get; set; } = "Secrets!";

            public SecureValue FieldC { get; set; } = new SecureValue("More Secrets!");
        }
        internal class UnitTestSecureBConfig
        {
            [Secure]
            public required string FieldA { get; set; } = "plughub";

            public int FieldB { get; set; } = 100;

            [Secure]
            public float FieldC { get; } = 3.14f;
        }


        [TestInitialize]
        public void Setup()
        {
            this.tokenService = new TokenService(new NullLogger<ITokenService>());
            this.secureStorage = new InsecureStorage(new NullLogger<ISecureStorage>());
            this.secureStorage.Initialize(this.msTestHelpers.TempDirectory, false, false);
            this.encryptionService = new EncryptionService(new NullLogger<IEncryptionService>(), this.secureStorage);
            this.encryptionContext = this.encryptionService.GetEncryptionContext(typeof(SecureFileConfigAccessorTests), new("EFFFFFFF-FFFF-EEEE-FFFF-FFFFFFFFFFFE"));
            this.configService = new ConfigService(
                [
                    new FileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService),
                    new SecureFileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService, this.encryptionService),
                    new UserFileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService),
                    new SecureUserFileConfigService(new NullLogger<IConfigServiceProvider>(), this.tokenService, this.encryptionService),
                ],
                [
                    new FileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService !),
                    new SecureFileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService),
                ],
                new NullLogger<IConfigService>(),
                this.tokenService,
                this.msTestHelpers.TempDirectory,
                this.msTestHelpers.TempDirectory);

            this.tokenSet = this.tokenService.CreateTokenSet(
                this.tokenService.CreateToken(),
                this.tokenService.CreateToken(),
                this.tokenService.CreateToken());

            this.secureParams =
                new SecureFileConfigServiceParams(
                    EncryptionContext: this.encryptionContext!,
                    Owner: this.tokenSet.Owner,
                    Read: this.tokenSet.Read,
                    Write: this.tokenSet.Write);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.msTestHelpers!.Dispose();
        }


        #region SecureAccessorTests: Registration

        [TestMethod]
        [TestCategory("Registration")]
        public void For_ValidType_ReturnsSecureAccessorFor()
        {
            // Arrange
            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor =
                new SecureFileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act & Assert
            Assert.IsInstanceOfType(accessor, typeof(ISecureFileConfigAccessorFor<UnitTestSecureAConfig>));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void For_InvalidType_ThrowsTypeAccessException()
        {
            // Arrange
            ISecureFileConfigAccessor accessor =
                new SecureFileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet);

            // Act & Assert
            Assert.ThrowsException<InvalidOperationException>(() => accessor.For<UnitTestSecureBConfig>());
        }

        #endregion

        #region SecureAccessorTests ‑ KeyHandling

        [TestMethod]
        [TestCategory("KeyHandling")]
        public void Set_WithUnknownKey_ThrowsKeyNotFoundException()
        {
            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor =
                new SecureFileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            Assert.ThrowsException<KeyNotFoundException>(() => accessor.Set("DoesNotExist", 123));
        }

        [TestMethod]
        [TestCategory("KeyHandling")]
        public void Get_WithUnconvertibleType_ReturnsEmptyGuid()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor =
                new SecureFileConfigAccessor(new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            accessor.Set(nameof(UnitTestSecureAConfig.FieldA), "not-a-guid");

            // Act
            Guid value = accessor.Get<Guid>(nameof(UnitTestSecureAConfig.FieldA));

            // Assert
            Assert.AreEqual(Guid.Empty, value, "An unconvertible value should yield Guid.Empty.");
        }

        #endregion

        #region SecureAccessorTests: Accessors

        [TestMethod]
        [TestCategory("Accessors")]
        public void Get_WithInvalidReadToken_Throws()
        {
            // Arrange            
            Token badRead = this.tokenService.CreateToken();

            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            // Act
            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(badRead, badRead, badRead)
                    .For<UnitTestSecureAConfig>();

            // Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() => accessor.Get<string>("FieldB"));
        }

        [TestMethod]
        [TestCategory("Accessors")]
        public void Get_WithUnknownProperty_Throws()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act & Assert
            Assert.ThrowsException<KeyNotFoundException>(() => accessor.Get<string>("DoesNotExist"));
        }

        #endregion

        #region SecureAccessorTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task Persistence_SetGet_NonSecureValue_RoundTrips()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act
            Guid testValue = Guid.NewGuid();
            accessor.Set("FieldA", testValue);
            Guid result = accessor.Get<Guid>("FieldA");

            await accessor.SaveAsync();

            // Assert
            string configFilePath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.json");
            string fileContents = File.ReadAllText(configFilePath);

            Assert.AreEqual(testValue, result);
            Assert.IsTrue(fileContents.Contains(testValue.ToString()), "Config file should store the value in plaintext.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task Set_And_Get_SensitiveValue_RoundTrips()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act
            string testValue = "UltraSecret";
            accessor.Set("FieldB", testValue);
            string result = accessor.Get<string>("FieldB");

            await accessor.SaveAsync();

            // Assert
            string configFilePath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.json");
            string fileContents = File.ReadAllText(configFilePath);

            Assert.AreEqual(testValue, result);
            Assert.IsFalse(fileContents.Contains(testValue), "Config file should not store the sensitive value in plaintext.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_WithModifiedInstance_PersistsChanges()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act
            UnitTestSecureAConfig config = accessor.Get();
            config.FieldB = "VerySecret";

            await accessor.SaveAsync(config);

            UnitTestSecureAConfig roundTrip = accessor.Get();

            // Assert
            Assert.AreEqual("VerySecret", roundTrip.FieldB);
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_WithModifiedSettings_PersistsToDisk()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.tokenService!, this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act
            Guid updated = Guid.NewGuid();
            accessor.Set(nameof(UnitTestSecureAConfig.FieldA), updated);

            await accessor.SaveAsync();

            // Assert
            string path = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.json");
            string json = File.ReadAllText(path);

            Assert.IsTrue(json.Contains(updated.ToString()),
                "User settings should include the updated FieldA value.");
        }

        #endregion
    }

}