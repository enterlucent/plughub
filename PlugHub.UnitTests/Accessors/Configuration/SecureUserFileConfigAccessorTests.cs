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
    public class SecureUserFileConfigAccessorTests
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
                    new FileConfigAccessor(new NullLogger<IConfigAccessor>()),
                    new SecureFileConfigAccessor(new NullLogger<IConfigAccessor>(), this.encryptionService),
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
                new SecureUserFileConfigServiceParams(
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


        #region SecureAccessorTests: Persistence

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_NoChanges_WritesEmptyFile()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act
            await accessor.SaveAsync();

            // Assert
            string path = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.user.json");
            bool exists = File.Exists(path);

            Assert.IsTrue(exists);
            Assert.AreEqual("{}", File.ReadAllText(path).Trim());
        }

        [TestMethod]
        [TestCategory("Persistence")]
        public async Task SaveAsync_RevertOverride_RemovesUserEntry()
        {
            // Arrange
            this.configService.RegisterConfig(typeof(UnitTestSecureAConfig), this.secureParams!);

            ISecureFileConfigAccessorFor<UnitTestSecureAConfig> accessor = new SecureFileConfigAccessor(
                new NullLogger<IConfigAccessor>(), this.encryptionService)
                    .SetEncryptionContext(this.encryptionContext!)
                    .SetConfigTypes([typeof(UnitTestSecureAConfig)])
                    .SetConfigService(this.configService)
                    .SetAccess(this.tokenSet)
                    .For<UnitTestSecureAConfig>();

            // Act
            accessor.Set("FieldA", Guid.NewGuid());
            await accessor.SaveAsync();

            accessor.Set("FieldA", Guid.Empty);
            await accessor.SaveAsync();

            // Assert
            string path = Path.Combine(this.msTestHelpers.TempDirectory, "UnitTestSecureAConfig.user.json");
            string json = File.ReadAllText(path);

            Assert.IsFalse(json.Contains("FieldA"), "User override should have been removed");
        }

        #endregion
    }

}