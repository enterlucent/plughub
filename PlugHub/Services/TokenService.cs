using Microsoft.Extensions.Logging;
using PlugHub.Models;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;

namespace PlugHub.Services
{
    public class TokenService(ILogger<ITokenService> logger) : ITokenService
    {
        private readonly ILogger<ITokenService> logger = logger;

        public Token CreateToken()
            => Token.New();

        public ITokenSet CreateTokenSet(Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        {
            var nOwner = ownerToken ?? new Token();
            var nRead = readToken ?? writeToken ?? Token.Public;
            var nWrite = writeToken ?? Token.Blocked;

            return new TokenSet(nOwner, nRead, nWrite);
        }

        public bool AllowAccess(Token? resourceOwner, Token? resourcePermission, Token? accessor, Token? accessorPermission, bool throwException = true)
        {
            if (accessor != null && accessor != Token.Public && resourceOwner == accessor) return true;

            if (resourcePermission == Token.Blocked)
            {
                if (throwException) ThrowUniformException();

                return false;
            }

            bool isPublic = resourcePermission == Token.Public;
            bool permissionsMatch = (resourcePermission != null && resourcePermission == accessorPermission);

            if (isPublic || permissionsMatch) return true;

            if (throwException) ThrowUniformException();

            return false;
        }

        private static void ThrowUniformException()
            => throw new UnauthorizedAccessException($"Proivded token is invalid.");
    }
}