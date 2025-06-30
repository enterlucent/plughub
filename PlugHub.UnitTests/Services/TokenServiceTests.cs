using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PlugHub.Services;
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
            var tokenSet = this.tokenService!.CreateTokenSet();

            // Assert
            Assert.AreEqual(Token.Public, tokenSet.Read);
            Assert.AreEqual(Token.Blocked, tokenSet.Write);
        }

        [TestMethod]
        [TestCategory("Instantiation")]
        public void CreateTokenSet_WriteOverridesRead_Defaults()
        {
            // Arrange
            var write = Token.New();

            // Act
            var tokenSet = this.tokenService!.CreateTokenSet(write: write);

            // Assert
            Assert.AreEqual(write, tokenSet.Read);   // Read inherits write if read is null
            Assert.AreEqual(write, tokenSet.Write);
        }

        [TestMethod]
        [TestCategory("Instantiation")]
        public void CreateTokenSet_ReadAndWrite_AreDistinct()
        {
            // Arrange
            var read = Token.New();
            var write = Token.New();

            // Act
            var tokenSet = this.tokenService!.CreateTokenSet(read: read, write: write);

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
            bool result = this.tokenService!.AllowAccess(source, accessor);

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
                () => this.tokenService!.AllowAccess(source, accessor, true));
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_CustomTokenValidAccessor_ReturnsTrue()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = source;

            // Act
            bool result = this.tokenService!.AllowAccess(source, accessor);

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
                () => this.tokenService!.AllowAccess(source, accessor, true));
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
                () => this.tokenService!.AllowAccess(source, accessor, true));
        }

        [TestMethod]
        [TestCategory("Security")]
        public void ValidateAccessor_NonThrowingModeReturnsFalseForInvalid()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = Token.New();

            // Act
            bool result = this.tokenService!.AllowAccess(source, accessor, false);

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
            bool result = this.tokenService!.AllowAccess(source, accessor);

            // Assert
            Assert.IsTrue(result);
        }


        [TestCategory("Security")]
        public void AllowAny_AnyMatchingToken_ReturnsTrue()
        {
            // Arrange
            var token = Token.New();
            var required = this.tokenService!.CreateTokenSet(read: token);
            var provided = this.tokenService.CreateTokenSet(read: token);

            // Act
            bool result = this.tokenService.AllowAny(required, provided);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        [TestCategory("Security")]
        public void AllowAny_NoMatchingToken_ReturnsFalse()
        {
            // Arrange
            var required = this.tokenService!.CreateTokenSet(read: Token.New());
            var provided = this.tokenService.CreateTokenSet(read: Token.New());

            // Act
            bool result = this.tokenService.AllowAny(required, provided, throwIfInvalid: false);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        [TestCategory("Security")]
        public void AllowAny_NoMatch_ThrowsWhenRequested()
        {
            // Arrange
            var required = this.tokenService!.CreateTokenSet(read: Token.New());
            var provided = this.tokenService.CreateTokenSet(read: Token.New());

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(() =>
                this.tokenService.AllowAny(required, provided, throwIfInvalid: true));
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
