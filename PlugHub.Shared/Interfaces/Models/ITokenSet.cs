using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Models
{
    /// <summary>
    /// Represents a set of access tokens.
    /// </summary>
    public interface ITokenSet
    {
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
        /// <param name="read">The read access <see cref="Token"/>.</param>
        /// <param name="write">The write access <see cref="Token"/>.</param>
        void Deconstruct(out Token read, out Token write);
    }
}
