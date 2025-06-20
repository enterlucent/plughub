using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>
    /// Defines methods for creating and validating tokens within the PlugHub system.
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
        /// Validates whether the accessor token is authorized to access the source token.
        /// </summary>
        /// <param name="source">
        /// The original <see cref="Token"/> to be accessed.
        /// </param>
        /// <param name="accessor">
        /// The <see cref="Token"/> attempting to access the source.
        /// </param>
        /// <param name="throwException">
        /// If <c>true</c>, throws an exception when validation fails; otherwise, returns <c>false</c>.
        /// </param>
        /// <returns>
        /// <c>true</c> if the accessor token is valid for the source; otherwise, <c>false</c>.
        /// </returns>
        public bool ValidateAccessor(Token source, Token accessor, bool throwException = true);
    }
}
