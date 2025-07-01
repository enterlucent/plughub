using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>
    /// Defines methods for creating and validating access permission tokens within the PlugHub system.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Creates a new <see cref="Token"/> instance.
        /// </summary>
        /// <returns>
        /// A newly generated <see cref="Token"/>.
        /// </returns>
        public Token CreateToken();

        /// <summary>
        /// Creates a new <see cref="ITokenSet"/> with optional read and write <see cref="Token"/>s.
        /// </summary>
        /// <param name="ownerToken">A <see cref="Token"/> signifying owner access.</param>
        /// <param name="readToken">An optional <see cref="Token"/> granting read access.</param>
        /// <param name="writeToken">An optional <see cref="Token"/> granting write access.</param>
        /// <returns>
        /// An <see cref="ITokenSet"/> containing the specified tokens.
        /// </returns>
        public ITokenSet CreateTokenSet(Token? ownerToken = null, Token? readToken = null, Token? writeToken = null);


        /// <summary>
        /// Validates whether the accessor is authorized to access the resource using dual-token permissions.
        /// </summary>
        /// <param name="resourceOwner">The owner token of the protected resource.</param>
        /// <param name="resourcePermission">The required permission token (e.g., Read/Write) for accessing the resource.</param>
        /// <param name="accessor">The token presented by the entity attempting access.</param>
        /// <param name="accessorPermission">The permission token claimed by the accessing entity.</param>
        /// <param name="throwException">If <c>true</c>, throws <see cref="UnauthorizedAccessException"/> on failure; otherwise returns <c>false</c>.</param>
        /// <returns>
        /// <c>true</c> if access is granted; <c>false</c> if validation fails and <paramref name="throwException"/> is <c>false</c>.
        /// </returns>
        /// <remarks>
        public bool AllowAccess(Token? resourceOwner, Token? resourcePermission, Token? accessor, Token? accessorPermission, bool throwException = true);
    }
}
