using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;

namespace PlugHub.Services
{
    public class TokenService(ILogger<ITokenService> logger) : ITokenService
    {
        private readonly ILogger<ITokenService> logger = logger;

        public Token CreateToken()
        {
            Token token = Token.New();
            return token;
        }

        public bool ValidateAccessor(Token source, Token accessor, bool throwException = true)
        {
            if (source != Token.Blocked && (source == Token.Public || source == accessor))
                return true;

            if (throwException)
                throw new UnauthorizedAccessException($"Proivded token is invalid.");

            return false;
        }
    }
}