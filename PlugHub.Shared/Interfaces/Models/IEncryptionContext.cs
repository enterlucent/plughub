using PlugHub.Shared.Interfaces.Services;

namespace PlugHub.Shared.Interfaces.Models
{
    /// <summary>
    /// Immutable bundle that pairs a symmetric key with the <see cref="IEncryptionService"/> able
    /// to use it.  Safe to share across threads.
    /// </summary>
    public interface IEncryptionContext
    {
        /// <summary>Encryption / decryption engine associated with this context.</summary>
        IEncryptionService EncryptionService { get; }

        /// <summary>Raw symmetric key bytes (caller must treat as read-only).</summary>
        byte[] Key { get; }
    }
}
