using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Services;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;

namespace PlugHub.UnitTests.Services
{
    [TestClass]
    public class TokenServiceTests
    {
        private ILogger<ITokenService>? logger;
        private TokenService? tokenService;

        [TestInitialize]
        public void Setup()
        {
            this.logger = new NullLogger<ITokenService>();
            this.tokenService = new TokenService(this.logger);
        }

        #region TokenServiceTests: Instantiation

        [TestMethod]
        [TestCategory("Instantiation")]
        public void CreateTokenReturnsValidToken()
        {
            // Act
            Token token = this.tokenService!.CreateToken();

            // Assert
            Assert.IsFalse(token.IsPublic);
            Assert.IsFalse(token.IsBlocked);
            Assert.AreNotEqual(Guid.Empty, (Guid)token);
        }

        [TestCategory("Instantiation")]
        public void CreateTokenSet_Defaults_ReturnsExpectedTokens()
        {
            // Act
            ITokenSet tokenSet = this.tokenService!.CreateTokenSet();

            // Assert
            Assert.AreEqual(Token.Public, tokenSet.Read);
            Assert.AreEqual(Token.Blocked, tokenSet.Write);
        }

        [TestMethod]
        [TestCategory("Instantiation")]
        public void CreateTokenSet_WriteOverridesRead_Defaults()
        {
            // Arrange
            Token write = Token.New();

            // Act
            ITokenSet tokenSet = this.tokenService!.CreateTokenSet(writeToken: write);

            // Assert
            Assert.AreEqual(write, tokenSet.Read);   
            Assert.AreEqual(write, tokenSet.Write);
        }

        [TestMethod]
        [TestCategory("Instantiation")]
        public void CreateTokenSet_ReadAndWrite_AreDistinct()
        {
            // Arrange
            Token read = Token.New();
            Token write = Token.New();

            // Act
            ITokenSet tokenSet = this.tokenService!.CreateTokenSet(readToken: read, writeToken: write);

            // Assert
            Assert.AreEqual(read, tokenSet.Read);
            Assert.AreEqual(write, tokenSet.Write);
        }

        #endregion

        #region TokenServiceTests: Security

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_PublicSource_PublicAccessorReturnsTrue()
        {
            // Arrange
            Token source = Token.Public;
            Token accessor = Token.Public;

            // Act
            bool result = this.tokenService!.AllowAccess(null, source, null, accessor);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_BlockedSourceAnyAccessor_ThrowsException()
        {
            // Arrange
            Token source = Token.Blocked;
            Token accessor = Token.New();

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.AllowAccess(null, source, null, accessor, true));
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_CustomTokenValidAccessor_ReturnsTrue()
        {
            // Arrange
            ITokenSet source = this.tokenService!.CreateTokenSet(this.tokenService.CreateToken(), this.tokenService.CreateToken(), this.tokenService.CreateToken());
            ITokenSet accessor = this.tokenService.CreateTokenSet(source.Owner, this.tokenService.CreateToken(), this.tokenService.CreateToken());

            // Act
            bool result = this.tokenService!.AllowAccess(source.Owner, source.Read, accessor.Owner, accessor.Read);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_CustomTokenInvalidAccessor_ThrowsException()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = Token.New();

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.AllowAccess(null, source, null, accessor, true));
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_PublicSourceBlockedAccessor_ThrowsException()
        {
            // Arrange
            Token source = Token.Blocked;
            Token accessor = Token.Public;

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.AllowAccess(null, source, null, accessor, true));
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_NonThrowingModeReturnsFalseForInvalid()
        {
            // Arrange
            ITokenSet source = this.tokenService!.CreateTokenSet();
            ITokenSet accessor = this.tokenService.CreateTokenSet();

            // Act
            bool result = this.tokenService!.AllowAccess(source.Owner, source.Write, accessor.Owner, accessor.Write, false);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_SameTokenDifferentInstances_ReturnsTrue()
        {
            // Arrange
            Guid tokenId = Guid.NewGuid();
            Token source = Token.FromGuid(tokenId);
            Token accessor = Token.FromGuid(tokenId);

            // Act
            bool result = this.tokenService!.AllowAccess(null, source, null, accessor);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_OwnerMatch_ReturnsTrue()
        {
            // Arrange
            Token ownerToken = Token.New();
            Token resourcePermission = Token.New();
            Token accessor = ownerToken;

            // Act
            bool result = this.tokenService!.AllowAccess(
                resourceOwner: ownerToken,
                resourcePermission: resourcePermission,
                accessor: accessor,
                accessorPermission: null,
                throwException: false
            );

            // Assert
            Assert.IsTrue(result, "Owner token should grant access");
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_OwnerMismatch_ReturnsFalse()
        {
            // Arrange
            Token resourceOwner = Token.New();
            Token resourcePermission = Token.New();
            Token accessor = Token.New();

            // Act
            bool result = this.tokenService!.AllowAccess(
                resourceOwner: resourceOwner,
                resourcePermission: resourcePermission,
                accessor: accessor,
                accessorPermission: Token.New(),
                throwException: false
            );

            // Assert
            Assert.IsFalse(result, "Non-owner without matching permissions should be denied");
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_NullOwner_FallsBackToPermissions()
        {
            // Act
            bool result = this.tokenService!.AllowAccess(
                resourceOwner: null, 
                resourcePermission: Token.Public,
                accessor: Token.New(),
                accessorPermission: null,
                throwException: false
            );

            // Assert
            Assert.IsTrue(result); 
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_OwnerBypassesBlockedResource_ReturnsTrue()
        {
            // Arrange
            Token owner = Token.New();

            // Act
            bool result = this.tokenService!.AllowAccess(
                resourceOwner: owner,
                resourcePermission: Token.Blocked,
                accessor: owner,
                accessorPermission: null,
                throwException: true
            );

            // Assert
            Assert.IsTrue(result);
        }

        #endregion

        #region TokenServiceTests: Conversion

        [TestMethod]
        [TestCategory("Conversion")]
        public void Token_ImplicitConversion_ReturnsCorrectGuid()
        {
            // Arrange
            Guid expected = Guid.NewGuid();
            Token token = Token.FromGuid(expected);

            // Act
            Guid actual = token;

            // Assert
            Assert.AreEqual(expected, actual);
        }

        #endregion
    }
}