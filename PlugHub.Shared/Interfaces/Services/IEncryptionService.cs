using PlugHub.Shared.Interfaces.Models;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>Host-managed symmetric encryption helper.</summary>
    public interface IEncryptionService
    {
        /// <summary>Gets (or creates) the persistent encryption context for (<typeparamref name="T"/>, <paramref name="id"/>).</summary>
        /// <typeparam name="T">Configuration type.</typeparam>
        /// <param name="id">Plugin / tenant identifier.</param>
        /// <returns>Encryption context holding algorithm + sub-key.</returns>
        public ValueTask<IEncryptionContext> GetEncryptionContextAsync<T>(Guid id);

        /// <summary>Synchronous wrapper for <see cref="GetEncryptionContextAsync{T}(Guid)"/>.</summary>
        /// <typeparam name="T">Configuration type.</typeparam>
        /// <param name="id">Plugin / tenant identifier.</param>
        /// <returns>Encryption context.</returns>
        public IEncryptionContext GetEncryptionContext<T>(Guid id);

        /// <summary>Encrypts <paramref name="data"/> with <paramref name="key"/>.</summary>
        /// <param name="data">Plain-text bytes.</param>
        /// <param name="key">Symmetric key.</param>
        /// <returns>Cipher-text bytes.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="data"/> or <paramref name="key"/> is null.</exception>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Encryption failure.</exception>
        public byte[] Encrypt(byte[] data, byte[] key);

        /// <summary>Decrypts <paramref name="encryptedData"/> with <paramref name="key"/>.</summary>
        /// <param name="encryptedData">Cipher-text bytes.</param>
        /// <param name="key">Symmetric key.</param>
        /// <returns>Plain-text bytes.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="encryptedData"/> or <paramref name="key"/> is null.</exception>
        /// <exception cref="System.Security.Cryptography.CryptographicException">Decryption failure.</exception>
        public byte[] Decrypt(byte[] encryptedData, byte[] key);
    }
}
