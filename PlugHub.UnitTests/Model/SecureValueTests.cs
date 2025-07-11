using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Platform.Storage;
using PlugHub.Services;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;

namespace PlugHub.UnitTests.Model
{
    [TestClass]
    public sealed class SecureValueTests : IDisposable
    {
        private readonly MSTestHelpers msTestHelpers = new();
        private EncryptionService? encryptionService;
        private IEncryptionContext? encryptionContext;


        [TestInitialize]
        public void Initialize()
        {
            InsecureStorage storage = new(new NullLogger<InsecureStorage>());
            storage.Initialize(this.msTestHelpers.TempDirectory);

            this.encryptionService = new EncryptionService(new NullLogger<EncryptionService>(), storage);
            this.encryptionContext = this.encryptionService.GetEncryptionContext(typeof(SecureValueTests), Guid.NewGuid());
        }

        [TestCleanup]
        public void Dispose()
        {
            (this.encryptionService as IDisposable)?.Dispose();
            this.msTestHelpers.Dispose();
        }


        #region SecureValueTests: Dispose

        [TestMethod]
        [TestCategory("Dispose")]
        public void DisposeThenAccess_ThrowsObjectDisposedException()
        {
            // Arrange
            SecureValue secure = SecureValue.From("super-secret", this.encryptionContext!);

            // Act
            secure.Dispose();

            // Assert
            Assert.ThrowsException<ObjectDisposedException>(() => secure.As<string>(this.encryptionContext!));
        }

        [TestMethod]
        [TestCategory("Dispose")]
        public void DoubleDispose_NoException()
        {
            // Arrange
            SecureValue secure = SecureValue.From(12345, this.encryptionContext!);

            // Act & Assert
            secure.Dispose();
            secure.Dispose();
        }

        [TestMethod]
        [TestCategory("Dispose")]
        public void SecureValue_Dispose_ClearsDecryptedBytesAndPreventsFurtherUse()
        {
            // Arrange
            string original = "SensitiveData";
            SecureValue secureValue = SecureValue.From(original, this.encryptionContext!);

            // Act
            string decrypted = secureValue.As<string>(this.encryptionContext!);
            Assert.AreEqual(original, decrypted, "Decryption must yield the original value before disposal.");

            secureValue.Dispose();

            // Assert
            Assert.ThrowsException<ObjectDisposedException>(() =>
            {
                secureValue.As<string>(this.encryptionContext!);
            }, "Using As<T> after disposal should throw.");

            System.Reflection.FieldInfo? field = typeof(SecureValue).GetField("decryptedBytes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            byte[]? value = (byte[]?)field?.GetValue(secureValue);
            Assert.IsNull(value, "decryptedBytes should be null after disposal.");
        }

        #endregion
    }
}