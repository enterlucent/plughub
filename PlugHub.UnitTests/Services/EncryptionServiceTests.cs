using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PlugHub.UnitTests.Services
{
    /// <summary>Test-only helper that peeks inside <see cref="EncryptionService"/>.</summary>
    internal static class EncryptionServiceExtensions
    {
        private static readonly FieldInfo masterKeyField =
            typeof(EncryptionService).GetField("masterKey",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Field 'masterKey' not found via reflection.");

        public static byte[] GetMasterKey(this EncryptionService service)
        {
            ArgumentNullException.ThrowIfNull(service);

            return masterKeyField.GetValue(service) as byte[] ?? [];
        }
    }

    [TestClass]
    public class EncryptionServiceTests
    {
        private MSTestHelpers msTestHelpers = new();
        private InsecureStorage? storage;
        private IConfigService? configService;
        private EncryptionService? encryptionService;


        private void RestartEncryptionService(bool rebuildFiles = true)
        {
            if (rebuildFiles)
            {
                this.msTestHelpers.Dispose();
                this.msTestHelpers = new();
            }

            ITokenService tokenService = new TokenService(new NullLogger<TokenService>());

            if (this.encryptionService != null)
                ((IDisposable)this.encryptionService!).Dispose();

            if (this.storage != null)
                ((IDisposable)this.storage!).Dispose();

            if (this.configService != null)
                ((IDisposable)this.configService!).Dispose();

            this.configService = new ConfigService(
                new NullLogger<IConfigService>(),
                tokenService,
                this.msTestHelpers.TempDirectory,
                this.msTestHelpers.TempDirectory);

            this.storage = new InsecureStorage(new NullLogger<ISecureStorage>(), tokenService, this.configService, this.msTestHelpers.TempDirectory);

            this.encryptionService = new EncryptionService(new NullLogger<IEncryptionService>(), this.storage);
        }


        [TestInitialize]
        public void Initialize()
        {
            this.RestartEncryptionService();
        }

        [TestCleanup]
        public void Dispose()
        {
            this.msTestHelpers!.Dispose();
        }


        #region EncryptionService: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public void MasterKey_PersistsBetweenSessions()
        {
            // Arrange
            byte[] firstMasterKey = new byte[32];

            // Act
            byte[] masterKey = this.encryptionService!.GetMasterKey();
            Array.Copy(masterKey, firstMasterKey, masterKey.Length);

            // Assert
            this.RestartEncryptionService(false);

            CollectionAssert.AreEqual(firstMasterKey, this.encryptionService!.GetMasterKey());
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task RoundTripEncryption_WithKeyPersistence_Succeeds()
        {
            // Arrange
            Guid contextId = new("EFFFFFFF-FFFF-EEEE-FFFF-FFFFFFFFFFFE");
            const string originalData = "foo";

            // Act
            IEncryptionContext context =
                await this.encryptionService!.GetEncryptionContextAsync<EncryptionServiceTests>(contextId);

            byte[] encryptedData = this.encryptionService
                .Encrypt(Encoding.UTF8.GetBytes(originalData), context.Key);

            this.RestartEncryptionService(false);

            IEncryptionContext newContext =
                await this.encryptionService.GetEncryptionContextAsync<EncryptionServiceTests>(contextId);

            string decryptedData = Encoding.UTF8.GetString(
                this.encryptionService.Decrypt(encryptedData, newContext.Key));

            // Assert
            Assert.AreEqual(originalData, decryptedData);
            CollectionAssert.AreEqual(context.Key, newContext.Key);
        }


        [TestMethod]
        [TestCategory("Persistence")]
        public void EncryptDecrypt_WithSameKey_ReturnsOriginalData()
        {
            // Arrange
            byte[] key = new byte[32];
            RandomNumberGenerator.Fill(key);
            byte[] originalData = Encoding.UTF8.GetBytes("test payload");

            // Act
            byte[] encrypted = this.encryptionService!.Encrypt(originalData, key);
            byte[] decrypted = this.encryptionService.Decrypt(encrypted, key);

            // Assert
            CollectionAssert.AreEqual(originalData, decrypted);
        }

        #endregion

        #region EncryptionService: Initialization

        [TestMethod]
        [TestCategory("Initialization")]
        public async Task Ctor_GeneratesPrimaryKey_WhenAbsent()
        {
            // Arrange – first touch forces lazy initialisation
            _ = await this.encryptionService!
                .GetEncryptionContextAsync<EncryptionServiceTests>(Guid.NewGuid());

            // Act
            byte[] key = this.encryptionService.GetMasterKey();

            byte[]? persisted = (await this.storage!
                    .TryLoadAsync("plughub-masterkey"))
                    ?.ToArray();

            // Assert
            Assert.AreEqual(EncryptionService.KeySizeBytes, key.Length,
                "Master-key must be 32 bytes long.");

            CollectionAssert.AreEqual(key, persisted,
                "Key should have been stored in secure storage.");
        }

        #endregion

        #region EncryptionService: EncryptionContext

        [TestMethod]
        [TestCategory("EncryptionContext")]
        public async Task DifferentContexts_GenerateDifferentKeys()
        {
            // Arrange
            Guid contextId1 = Guid.NewGuid();
            Guid contextId2 = Guid.NewGuid();

            // Act
            IEncryptionContext context1 = await this.encryptionService!.GetEncryptionContextAsync<EncryptionServiceTests>(contextId1);
            IEncryptionContext context2 = await this.encryptionService.GetEncryptionContextAsync<EncryptionServiceTests>(contextId2);

            // Assert
            CollectionAssert.AreNotEqual(context1.Key, context2.Key);
        }

        [TestMethod]
        [TestCategory("EncryptionContext")]
        public async Task GetEncryptionContext_SameTypeAndId_ReturnsSameKey()
        {
            // Arrange
            byte[] key1 = new byte[32];
            byte[] key2 = new byte[32];

            Guid contextID = Guid.NewGuid();

            // Act
            IEncryptionContext context1 = await this.encryptionService!.GetEncryptionContextAsync<EncryptionServiceTests>(contextID);

            Array.Copy(context1.Key, key1, 32);

            this.RestartEncryptionService(false);

            IEncryptionContext context2 = await this.encryptionService!.GetEncryptionContextAsync<EncryptionServiceTests>(contextID);

            Array.Copy(context2.Key, key2, 32);

            // Assert
            CollectionAssert.AreEqual(key1, key2, "Same type + id must yield identical context keys across sessions.");
        }

        [TestMethod]
        [TestCategory("EncryptionContext")]
        public async Task GetEncryptionContext_DifferentTypeSameId_ReturnsDifferentKeys()
        {
            // Arrange
            Guid contextID = Guid.NewGuid();

            // Act
            IEncryptionContext context1 = await this.encryptionService!.GetEncryptionContextAsync<EncryptionServiceTests>(contextID);
            IEncryptionContext context2 = await this.encryptionService!.GetEncryptionContextAsync<EncryptionService>(contextID);

            byte[] keyA = context1.Key;
            byte[] keyB = context2.Key;

            // Assert
            CollectionAssert.AreNotEqual(keyA, keyB, "Changing the generic type must change the derived context key.");
        }

        #endregion

        #region EncryptionContext: Encryptoin/Decryption

        [TestMethod]
        [TestCategory("EncryptDecrypt")]
        public void EncryptDecrypt_EmptyPayload_RoundTrips()
        {
            // Arrange
            byte[] key = new byte[32]; RandomNumberGenerator.Fill(key);

            // Act
            byte[] cipher = this.encryptionService!.Encrypt([], key);
            byte[] plain = this.encryptionService.Decrypt(cipher, key);

            // Assert
            CollectionAssert.AreEqual(Array.Empty<byte>(), plain);

            Assert.AreEqual(1 + EncryptionService.NonceSize + EncryptionService.TagSize, cipher.Length, "Blob length should only contain overhead.");
        }

        [TestMethod]
        [TestCategory("EncryptDecrypt")]
        public void Encrypt_GeneratesUniqueCiphertext_ForSameInput()
        {
            // Arrange
            byte[] key = new byte[32]; RandomNumberGenerator.Fill(key);
            byte[] data = Encoding.UTF8.GetBytes("hello");

            // Act
            byte[] c1 = this.encryptionService!.Encrypt(data, key);
            byte[] c2 = this.encryptionService.Encrypt(data, key);

            // Assert
            CollectionAssert.AreNotEqual(c1, c2, "Different nonces must produce different ciphertext for the same input.");
        }

        [TestMethod]
        [TestCategory("EncryptDecrypt")]
        public void Encrypt_OutputContainsExpectedOverhead()
        {
            // Arrange
            byte[] data = new byte[128];
            byte[] key = new byte[32]; RandomNumberGenerator.Fill(key);

            int expected = 1 + EncryptionService.NonceSize + EncryptionService.TagSize + data.Length;

            // Act
            byte[] cipher = this.encryptionService!.Encrypt(data, key);

            // Assert
            Assert.AreEqual(expected, cipher.Length,
                "Ciphertext layout should match marker + nonce + tag + data.");
            Assert.AreEqual(CryptoSchema.MarkerAesGcmV1, cipher[0],
                "First byte must be the AES/GCM v1 schema marker.");
        }

        [TestMethod]
        [TestCategory("EncryptDecrypt")]
        public void Encrypt_NullPlainText_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] key = new byte[32]; RandomNumberGenerator.Fill(key);

            // Act & Assert
            Assert.ThrowsException<NullReferenceException>(() =>
                this.encryptionService!.Encrypt(null!, key));
        }

        #endregion

        #region EncryptionContext: Security

        [TestMethod]
        [TestCategory("Security")]
        public void Decrypt_WithWrongKey_ThrowsCryptographicException()
        {
            // Arrange
            byte[] goodKey = new byte[32]; RandomNumberGenerator.Fill(goodKey);
            byte[] badKey = new byte[32]; RandomNumberGenerator.Fill(badKey);

            // Act
            byte[] cipher = this.encryptionService!.Encrypt([1, 2, 3, 4], goodKey);

            // Assert
            Assert.ThrowsException<AuthenticationTagMismatchException>(() => this.encryptionService.Decrypt(cipher, badKey));
        }

        [TestMethod]
        [TestCategory("Security")]
        public void Decrypt_WithInvalidMarker_ThrowsCryptographicException()
        {
            // Arrange
            byte[] key = new byte[32]; RandomNumberGenerator.Fill(key);

            // Act
            byte[] cipher = this.encryptionService!.Encrypt([7, 7], key);

            // Assert
            cipher[0] ^= 0xFF;

            Assert.ThrowsException<CryptographicException>(() => this.encryptionService.Decrypt(cipher, key));
        }

        [TestMethod]
        [TestCategory("Security")]
        public void EncryptionService_LargeBufferTests_Succeeds()
        {
            var tokenService = new TokenService(new NullLogger<TokenService>());
            var configService = new ConfigService(new NullLogger<ConfigService>(),
                tokenService, this.msTestHelpers.
                TempDirectory,
                this.msTestHelpers.TempDirectory);
            var storage = new InsecureStorage(new NullLogger<InsecureStorage>(), tokenService, configService);

            this.encryptionService = new EncryptionService(new NullLogger<EncryptionService>(), storage);
            IEncryptionContext encryptionContext =
                this.encryptionService.GetEncryptionContext<EncryptionServiceTests>(Guid.NewGuid());

            byte[] plain = new byte[1048576]; // 1 MiB
            RandomNumberGenerator.Fill(plain);

            byte[] cipher = this.encryptionService!.Encrypt(plain, encryptionContext.Key);
            byte[] roundTrip = this.encryptionService.Decrypt(cipher, encryptionContext.Key);

            CollectionAssert.AreEqual(plain, roundTrip,
                "Encrypting and decrypting a large buffer should return the original payload.");
        }

        #endregion

        #region EncryptionContext: Disposal

        [TestMethod]
        [TestCategory("Disposal")]
        public void Dispose_ZeroesOutMasterKey()
        {
            // Arrange
            byte[] copy;
            byte[] field;

            // Act
            this.RestartEncryptionService();
            copy = this.encryptionService!.GetMasterKey();
            this.RestartEncryptionService(false);
            field = this.encryptionService!.GetMasterKey();

            // Assert
            Assert.IsTrue(Array.TrueForAll(copy, b => b == 0), "Master-key bytes should have been zeroed on dispose.");

            CollectionAssert.AllItemsAreNotNull(copy);
        }

        #endregion
    }
}
