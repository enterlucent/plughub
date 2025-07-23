using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;

namespace PlugHub.Shared.Models
{
    /// <summary>
    /// Represents an immutable encryption context that pairs an encryption service
    /// with an optional encryption key. This class serves as a lightweight container
    /// for encryption operations, ensuring the service is always available while
    /// allowing for flexible key management scenarios.
    /// </summary>
    /// <param name="encryptionService">The encryption service to use for cryptographic operations. Cannot be null.</param>
    /// <param name="key">Optional encryption key. If not provided, defaults to an empty byte array, 
    /// allowing the encryption service to handle key generation or use its own default key management.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="encryptionService"/> is null.</exception>
    public sealed class EncryptionContext(IEncryptionService encryptionService, byte[]? key = null) : IEncryptionContext
    {
        /// <summary>
        /// Gets the encryption service responsible for performing cryptographic operations.
        /// This service is guaranteed to be non-null and provides the core encryption functionality.
        /// </summary>
        public IEncryptionService EncryptionService { get; } = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));

        /// <summary>
        /// Gets the encryption key as a byte array. If no key was provided during construction,
        /// this returns an empty byte array, allowing the encryption service to determine
        /// appropriate key handling (such as generating a new key or using a default).
        /// </summary>
        public byte[] Key { get; } = key ?? [];
    }
}
