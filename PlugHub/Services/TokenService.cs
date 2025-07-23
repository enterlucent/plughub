using Microsoft.Extensions.Logging;
using PlugHub.Models;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;


namespace PlugHub.Services
{
    public sealed class TokenService(ILogger<ITokenService> logger) : ITokenService
    {
        private readonly ILogger<ITokenService> logger = logger;

        public Token CreateToken()
            => Token.New();

        public ITokenSet CreateTokenSet(Token? ownerToken = null, Token? readToken = null, Token? writeToken = null)
        {
            Token nOwner = ownerToken ?? Token.New();
            Token nRead = readToken ?? writeToken ?? Token.Public;
            Token nWrite = writeToken ?? Token.Blocked;

            return new TokenSet(nOwner, nRead, nWrite);
        }

        public bool AllowAccess(Token? resourceOwner, Token? resourcePermission, Token? accessor, Token? accessorPermission, bool throwException = true)
        {
            if (accessor != null && accessor != Token.Public && resourceOwner == accessor)
            {
                return true;
            }
            else if (resourcePermission == Token.Blocked)
            {
                if (throwException) ThrowUniformException();

                return false;
            }

            bool isPublic = resourcePermission == Token.Public;
            bool permissionsMatch = (resourcePermission != null && resourcePermission == accessorPermission);

            if (isPublic || permissionsMatch)
            {
                return true;
            }
            else
            {
                if (throwException) ThrowUniformException();

                return false;
            }
        }

        private static void ThrowUniformException()
            => throw new UnauthorizedAccessException("Provided token is invalid.");
    }
}