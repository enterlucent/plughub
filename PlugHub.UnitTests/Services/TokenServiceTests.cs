using Microsoft.Extensions.Logging;
using Moq;
using PlugHub.Services;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;

namespace PlugHub.UnitTests.Services
{
    [TestClass]
    public class TokenServiceTests
    {
        private Mock<ILogger<ITokenService>>? loggerMock;
        private TokenService? tokenService;

        [TestInitialize]
        public void Setup()
        {
            this.loggerMock = new Mock<ILogger<ITokenService>>();
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
        public void ValidateAccessor_PublicSource_PublicAccessorReturnsTrue()
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
        public void ValidateAccessor_BlockedSourceAnyAccessor_ThrowsException()
        {
            // Arrange
            Token source = Token.Blocked;
            Token accessor = Token.New();

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.ValidateAccessor(source, accessor, true));
        }

        [TestMethod]
        public void ValidateAccessor_CustomTokenValidAccessor_ReturnsTrue()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = source;

            // Act
            bool result = this.tokenService!.ValidateAccessor(source, accessor);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ValidateAccessor_CustomTokenInvalidAccessor_ThrowsException()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = Token.New();

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.ValidateAccessor(source, accessor, true));
        }

        [TestMethod]
        public void ValidateAccessor_PublicSourceBlockedAccessor_ThrowsException()
        {
            // Arrange
            Token source = Token.Blocked;
            Token accessor = Token.Public;

            // Act & Assert
            Assert.ThrowsException<UnauthorizedAccessException>(
                () => this.tokenService!.ValidateAccessor(source, accessor, true));
        }

        [TestMethod]
        public void ValidateAccessor_NonThrowingModeReturnsFalseForInvalid()
        {
            // Arrange
            Token source = Token.New();
            Token accessor = Token.New();

            // Act
            bool result = this.tokenService!.ValidateAccessor(source, accessor, false);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ValidateAccessor_SameTokenDifferentInstances_ReturnsTrue()
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
    }
}
