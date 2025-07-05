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
        private readonly MSTestHelpers helpers = new();
        private EncryptionService? encryptionService;
        private IEncryptionContext? encryptionContext;


        [TestInitialize]
        public void Initialize()
        {
            TokenService tokenService = new(new NullLogger<TokenService>());
            ConfigService configService = new(new NullLogger<ConfigService>(),
                tokenService,
                this.helpers.TempDirectory,
                this.helpers.TempDirectory);
            InsecureStorage storage = new(new NullLogger<InsecureStorage>(), tokenService, configService);

            this.encryptionService = new EncryptionService(new NullLogger<EncryptionService>(), storage);
            this.encryptionContext = this.encryptionService.GetEncryptionContext<SecureValueTests>(Guid.NewGuid());
        }

        [TestCleanup]
        public void Dispose()
        {
            (this.encryptionService as IDisposable)?.Dispose();
            this.helpers.Dispose();
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

        #endregion
    }
}