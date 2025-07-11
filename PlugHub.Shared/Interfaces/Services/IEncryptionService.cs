using PlugHub.Shared.Interfaces.Models;
using System.Security.Cryptography;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>Host-managed symmetric encryption helper.</summary>
    public interface IEncryptionService
    {
        /// <summary>Gets (or creates) the persistent encryption context for (<typeparamref name="T"/>, <paramref name="id"/>).</summary>
        /// <param name="configType"></param>
        /// <param name="id">Plugin / tenant identifier.</param>                
        /// <returns>Encryption context holding algorithm + sub-key.</returns>
        public ValueTask<IEncryptionContext> GetEncryptionContextAsync(Type configType, Guid id);

        /// <summary>Synchronous wrapper for <see cref="GetEncryptionContextAsync(Guid, Type)"/>.</summary>
        /// <param name="configType"></param>
        /// <param name="id">Plugin / tenant identifier.</param>
        /// <returns>Encryption context.</returns>        
        public IEncryptionContext GetEncryptionContext(Type configType, Guid id);

        /// <summary>Encrypts <paramref name="data"/> with <paramref name="key"/>.</summary>
        /// <param name="data">Plain-text bytes.</param>
        /// <param name="key">Symmetric key.</param>
        /// <returns>Cipher-text bytes.</returns>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="CryptographicException"/>
        public byte[] Encrypt(byte[] data, byte[] key);

        /// <summary>Decrypts <paramref name="encryptedData"/> with <paramref name="key"/>.</summary>
        /// <param name="encryptedData">Cipher-text bytes.</param>
        /// <param name="key">Symmetric key.</param>
        /// <returns>Plain-text bytes.</returns>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="CryptographicException"/>
        public byte[] Decrypt(byte[] encryptedData, byte[] key);
    }
}
