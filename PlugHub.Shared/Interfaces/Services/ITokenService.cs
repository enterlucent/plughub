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
        /// Validates whether the accessor is authorized to access the source.
        /// </summary>
        /// <param name="source">The original <see cref="Token"/> to be accessed.</param>
        /// <param name="accessor">The <see cref="Token"/> attempting to access the source.</param>
        /// <param name="throwException">If <c>true</c>, throws an exception when validation fails; otherwise, returns <c>false</c>.</param> 
        /// <returns>
        /// <c>true</c> if the accessor token is valid for the source; otherwise, <c>false</c>.
        /// </returns>
        public bool AllowAccess(Token source, Token accessor, bool throwException = true);

        /// <summary>
        /// Creates a new <see cref="ITokenSet"/> with optional read and write <see cref="Token"/>s.
        /// </summary>
        /// <param name="read">An optional <see cref="Token"/> granting read access.</param>
        /// <param name="write">An optional <see cref="Token"/> granting write access.</param>
        /// <returns>
        /// An <see cref="ITokenSet"/> containing the specified tokens.
        /// </returns>
        public ITokenSet CreateTokenSet(Token? read = null, Token? write = null);

        /// <summary>
        /// Determines if any token in the provided set satisfies the requirements of the required set.
        /// </summary>
        /// <param name="required">The set of tokens required for access.</param>
        /// <param name="provided">The set of tokens presented for validation.</param>
        /// <param name="throwIfInvalid">
        /// If <c>true</c>, throws an exception when validation fails; otherwise, returns <c>false</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if any provided token meets the required access; otherwise, <c>false</c>.
        /// </returns>
        bool AllowAny(ITokenSet required, ITokenSet provided, bool throwIfInvalid = true);
    }
}
