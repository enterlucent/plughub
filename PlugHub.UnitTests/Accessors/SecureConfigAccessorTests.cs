using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Accessors;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Models;

namespace PlugHub.UnitTests.Accessors
{
    [TestClass]
    public class SecureConfigAccessorTests
    {
        private readonly MSTestHelpers msTestHelpers = new();

        private TokenService tokenService = null!;
        private SecureConfigService configService = null!;
        private ISecureStorage secureStorage = null!;
        private EncryptionService encryptionService = null!;
        private IEncryptionContext encryptionContext = null!;
        private ITokenSet? invalidTokenSet = null;
        private ITokenSet? validTokenSet = null;


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
            this.tokenService = new TokenService(new NullLogger<TokenService>());
            this.configService = new SecureConfigService(new NullLogger<SecureConfigService>(),
                this.tokenService,
                this.msTestHelpers.TempDirectory,
                this.msTestHelpers.TempDirectory);

            this.secureStorage = new InsecureStorage(new NullLogger<InsecureStorage>(), this.tokenService, this.configService);
            this.encryptionService = new EncryptionService(new NullLogger<EncryptionService>(), this.secureStorage);
            this.encryptionContext = this.encryptionService.GetEncryptionContext<SecureConfigAccessorTests>(
                new("EFFFFFFF-FFFF-EEEE-FFFF-FFFFFFFFFFFE"));

            this.validTokenSet = this.tokenService.CreateTokenSet(
                this.tokenService.CreateToken(),
                this.tokenService.CreateToken(),
                this.tokenService.CreateToken());

            this.invalidTokenSet = this.tokenService.CreateTokenSet(
                this.tokenService.CreateToken(),
                this.tokenService.CreateToken(),
                this.tokenService.CreateToken());

            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.encryptionContext, this.validTokenSet);
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
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Act & Assert
            Assert.IsInstanceOfType(accessor, typeof(ISecureConfigAccessorFor<UnitTestSecureAConfig>));
        }

        [TestMethod]
        [TestCategory("Registration")]
        public void For_InvalidType_ThrowsTypeAccessException()
        {
            // Arrange
            ISecureConfigAccessor accessor = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write);

            // Act & Assert
            Assert.ThrowsException<TypeAccessException>(() => accessor.For<UnitTestSecureBConfig>());
        }

        #endregion

        #region SecureAccessorTests ‑ KeyHandling

        [TestMethod]
        [TestCategory("KeyHandling")]
        public void Set_WithUnknownKey_ThrowsKeyNotFoundException()
        {
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext)
                    .For<UnitTestSecureAConfig>();

            Assert.ThrowsException<KeyNotFoundException>(
                () => accessor.Set("DoesNotExist", 123));
        }

        [TestMethod]
        [TestCategory("KeyHandling")]
        public void Get_WithUnconvertibleType_ReturnsEmptyGuid()
        {
            // Arrange
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Inject a user override of the WRONG type – no file-munging required.
            accessor.Set<string>(nameof(UnitTestSecureAConfig.FieldA), "not-a-guid");

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

            this.configService.UnregisterConfig(typeof(UnitTestSecureAConfig), this.validTokenSet!.Owner);
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write);

            // Act
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.tokenService.CreateToken(), badRead, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() => accessor.Get<string>("FieldB"));
        }

        [TestMethod]
        [TestCategory("Accessors")]
        public void Get_WithUnknownProperty_Throws()
        {
            // Arrange
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Act & Assert
            Assert.ThrowsException<KeyNotFoundException>(() => accessor.Get<string>("DoesNotExist"));
        }

        #endregion

        #region SecureAccessorTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task Set_And_Get_NonSecureValue_RoundTrips()
        {
            // Arrange
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Act
            Guid testValue = Guid.NewGuid();
            accessor.Set("FieldA", testValue);
            Guid result = accessor.Get<Guid>("FieldA");

            await accessor.SaveAsync();

            // Assert
            string configFilePath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.UserSettings.json");
            string fileContents = File.ReadAllText(configFilePath);

            Assert.AreEqual(testValue, result);
            Assert.IsTrue(fileContents.Contains(testValue.ToString()), "Config file should store the value in plaintext.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task Set_And_Get_SensitiveValue_RoundTrips()
        {
            // Arrange
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Act
            string testValue = "UltraSecret";
            accessor.Set("FieldB", testValue);
            string result = accessor.Get<string>("FieldB");

            await accessor.SaveAsync();

            // Assert
            string configFilePath = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.UserSettings.json");
            string fileContents = File.ReadAllText(configFilePath);

            Assert.AreEqual(testValue, result);
            Assert.IsFalse(fileContents.Contains(testValue), "Config file should not store the sensitive value in plaintext.");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_NoChanges_WritesEmptyFile()
        {
            // Arrange
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Act
            await accessor.SaveAsync();

            // Assert
            string path = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.UserSettings.json");
            bool exists = File.Exists(path);

            Assert.IsTrue(exists);
            Assert.AreEqual("{}", File.ReadAllText(path).Trim());
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_RevertOverride_RemovesUserEntry()
        {
            // Arrange
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Act
            accessor.Set("FieldA", Guid.NewGuid());
            await accessor.SaveAsync();

            accessor.Set("FieldA", Guid.Empty);
            await accessor.SaveAsync();

            // Assert
            string path = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.UserSettings.json");
            string json = File.ReadAllText(path);

            Assert.IsFalse(json.Contains("FieldA"), "User override should have been removed");
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_WithModifiedInstance_PersistsChanges()
        {
            // Arrange
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
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
            ISecureConfigAccessorFor<UnitTestSecureAConfig> accessor
                = new SecureConfigAccessor(this.configService!)
                    .Init([typeof(UnitTestSecureAConfig)], this.encryptionContext, this.validTokenSet!.Owner, this.validTokenSet!.Read, this.validTokenSet!.Write)
                    .For<UnitTestSecureAConfig>();

            // Act
            Guid updated = Guid.NewGuid();
            accessor.Set(nameof(UnitTestSecureAConfig.FieldA), updated);

            await accessor.SaveAsync();

            // Assert
            string path = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.UserSettings.json");
            string json = File.ReadAllText(path);

            Assert.IsTrue(json.Contains(updated.ToString()),
                "User settings should include the updated FieldA value.");
        }

        #endregion

        #region SecureAccessorTests: Disposal

        [TestMethod]
        [TestCategory("Disposal")]
        public void SecureValue_Dispose_ClearsDecryptedBytesAndPreventsFurtherUse()
        {
            // Arrange
            string original = "SensitiveData";
            SecureValue secureValue = SecureValue.From(original, this.encryptionContext);

            // Act
            string decrypted = secureValue.As<string>(this.encryptionContext);
            Assert.AreEqual(original, decrypted, "Decryption must yield the original value before disposal.");

            secureValue.Dispose();

            // Assert
            Assert.ThrowsException<ObjectDisposedException>(() =>
            {
                secureValue.As<string>(this.encryptionContext);
            }, "Using As<T> after disposal should throw.");

            System.Reflection.FieldInfo? field = typeof(SecureValue).GetField("decryptedBytes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            byte[]? value = (byte[]?)field?.GetValue(secureValue);
            Assert.IsNull(value, "decryptedBytes should be null after disposal.");
        }

        #endregion
    }
}