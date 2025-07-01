using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Models
{
    /// <summary>
    /// Represents a set of access tokens.
    /// </summary>
    public interface ITokenSet
    {
        /// <summary>
        /// Gets the <see cref="Token"/> used for owner access.
        /// </summary>
        Token Owner { get; }

        /// <summary>
        /// Gets the <see cref="Token"/> used for read access.
        /// </summary>
        Token Read { get; }

        /// <summary>
        /// Gets the <see cref="Token"/> used for write access.
        /// </summary>
        Token Write { get; }

        /// <summary>
        /// Deconstructs the token set into its individual <see cref="Token"/> components.
        /// </summary>
        /// <param name="ownerToken">The owner access <see cref="Token"/>.</param>
        /// <param name="readToken">The read access <see cref="Token"/>.</param>
        /// <param name="writeToken">The write access <see cref="Token"/>.</param>
        void Deconstruct(out Token ownerToken, out Token readToken, out Token writeToken);
    }
}
