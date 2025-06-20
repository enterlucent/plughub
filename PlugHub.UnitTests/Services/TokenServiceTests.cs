using Microsoft.Extensions.Logging;
using Moq;
using PlugHub.Services;
using PlugHub.Shared.Models;

namespace PlugHub.UnitTests.Services
{
    [TestClass]
    public class TokenServiceTests
    {
        private Mock<ILogger<TokenService>>? loggerMock;
        private TokenService? tokenService;

        [TestInitialize]
        public void Setup()
        {
            this.loggerMock = new Mock<ILogger<TokenService>>();
            this.tokenService = new TokenService(this.loggerMock.Object);
        }

        [TestMethod]
        public void CreateTokenReturnsValidToken()
        {
            // Act
            Token token = this.tokenService!.CreateToken();

            // Assert
            Assert.IsFalse(token.IsPublic);
            Assert.IsFalse(token.IsBlocked);
            Assert.AreNotEqual(Guid.Empty, (Guid)token);
        }

        [TestMethod]
        public void ValidateAccessorPublicSourcePublicAccessorReturnsTrue()
        {
            // Arrange
            Token source = Token.Public;
            Token accessor = Token.Public;

            // Act
            bool result = this.tokenService!.ValidateAccessor(source, accessor);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateAccessorBlockedSourceAnyAccessorThrowsException()
        {
            // Arrange
            Token source = Token.Blocked;
            Token accessor = Token.New();

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.ValidateAccessor(source, accessor, true));
        }

        [TestMethod]
        public void ValidateAccessorCustomTokenValidAccessorReturnsTrue()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = source; // Same token

            // Act
            bool result = this.tokenService!.ValidateAccessor(source, accessor);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateAccessorCustomTokenInvalidAccessorThrowsException()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = Token.New(); // Different token

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.ValidateAccessor(source, accessor, true));
        }

        [TestMethod]
        public void ValidateAccessorPublicSourceBlockedAccessorThrowsException()
        {
            // Arrange
            Token source = Token.Blocked;
            Token accessor = Token.Public;

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.ValidateAccessor(source, accessor, true));
        }

        [TestMethod]
        public void ValidateAccessorNonThrowingModeReturnsFalseForInvalid()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = Token.New(); // Different token

            // Act
            bool result = this.tokenService!.ValidateAccessor(source, accessor, false);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateAccessorSameTokenDifferentInstancesReturnsTrue()
        {
            // Arrange
            Guid tokenId = Guid.NewGuid();
            Token source = Token.FromGuid(tokenId);
            Token accessor = Token.FromGuid(tokenId);

            // Act
            bool result = this.tokenService!.ValidateAccessor(source, accessor);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void TokenImplicitConversionReturnsCorrectGuid()
        {
            // Arrange
            Guid expected = Guid.NewGuid();
            Token token = Token.FromGuid(expected);

            // Act
            Guid actual = token;

            // Assert
            Assert.AreEqual(expected, actual);
        }
    }
}
