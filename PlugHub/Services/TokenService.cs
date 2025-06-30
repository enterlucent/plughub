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

        public ITokenSet CreateTokenSet(Token? read = null, Token? write = null)
        {
            var secRead = read ?? write ?? Token.Public;
            var secWrite = write ?? Token.Blocked;

            return new TokenSet(secRead, secWrite);
        }

        public bool AllowAccess(Token source, Token accessor, bool throwException = true)
        {
            if (source != Token.Blocked && (source == Token.Public || source == accessor))
                return true;

            if (throwException)
                ThrowUniformException();

            return false;
        }

        public bool AllowAny(ITokenSet required, ITokenSet provided, bool throwIfInvalid = true)
        {
            if (this.AllowAccess(required.Read, provided.Read, false) || this.AllowAccess(required.Write, provided.Write, false))
                return true;

            if (throwIfInvalid)
                ThrowUniformException();

            return false;
        }

        private static void ThrowUniformException()
            => throw new UnauthorizedAccessException($"Proivded token is invalid.");
    }
}