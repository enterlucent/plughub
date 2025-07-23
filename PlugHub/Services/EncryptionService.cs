using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Services
{
    public static class CryptoSchema
    {
        public const byte AlgoAesGcm = 0x10;  // 0001 xxxx
        public const byte V1 = 0x01;  // xxxx 0001

        public const byte MarkerAesGcmV1 = AlgoAesGcm | V1;  // 0x11
    }

    public sealed class EncryptionService : IEncryptionService, IDisposable
    {
        private sealed class EncryptionBuffers
        {
            public byte[] Ciphertext { get; set; } = [];
            public byte[] Tag { get; set; } = [];
        }

        public const int KeySizeBytes = 32;
        public const int TagSize = 16;
        public const int NonceSize = 12;

        private readonly ISecureStorage storage;
        private readonly ILogger logger;
        private readonly string masterKeyId;
        private readonly Lazy<Task<byte[]>> masterKeyTask;

        private byte[] masterKey = new byte[KeySizeBytes];
        private bool disposed;

        private static readonly SemaphoreSlim systemLock = new(1, 1);

        public EncryptionService(ILogger<IEncryptionService> logger, ISecureStorage storage, string masterKeyId = "plughub-masterkey")
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(storage);
            ArgumentNullException.ThrowIfNull(masterKeyId);

            this.logger = logger;
            this.storage = storage;
            this.masterKeyId = masterKeyId;
            this.masterKeyTask = new Lazy<Task<byte[]>>(this.LoadOrCreateMasterKeyAsync);

            this.logger.LogDebug("EncryptionService initialized with master key ID: {MasterKeyId}", masterKeyId);
        }

        #region EncryptionService: Public API

        public IEncryptionContext GetEncryptionContext(Type configType, Guid id)
        {
            ArgumentNullException.ThrowIfNull(configType);

            return this.GetEncryptionContextAsync(configType, id)
                       .AsTask()
                       .GetAwaiter()
                       .GetResult();
        }
        public async ValueTask<IEncryptionContext> GetEncryptionContextAsync(Type configType, Guid id)
        {
            ArgumentNullException.ThrowIfNull(configType);

            this.ThrowIfDisposed();

            byte[] loadedMasterKey = await this.EnsureMasterKeyLoadedAsync().ConfigureAwait(false);
            string contextBlobId = BuildContextIdentifier(configType, id);
            byte[] contextKey = await this.LoadOrCreateContextKeyAsync(contextBlobId, loadedMasterKey).ConfigureAwait(false);

            this.logger.LogDebug("Created encryption context for {ConfigType} with ID {Id}", configType.Name, id);

            return new EncryptionContext(this, contextKey);
        }

        public byte[] Encrypt(byte[] plaintext, byte[] key)
        {
            ArgumentNullException.ThrowIfNull(plaintext);
            ArgumentNullException.ThrowIfNull(key);

            this.ThrowIfDisposed();
            this.ValidateEncryptionKey(key);

            byte[] nonce = GenerateNonce();
            EncryptionBuffers buffers = CreateEncryptionBuffers(plaintext.Length);

            PerformAesGcmEncryption(plaintext, key, nonce, buffers);

            byte[] result = AssembleEncryptedResult(nonce, buffers);

            this.logger.LogDebug("Encrypted {PlaintextLength} bytes to {ResultLength} bytes", plaintext.Length, result.Length);

            return result;
        }
        public byte[] Decrypt(byte[] blob, byte[] key)
        {
            ArgumentNullException.ThrowIfNull(blob);
            ArgumentNullException.ThrowIfNull(key);

            this.ThrowIfDisposed();
            this.ValidateDecryptionInputs(blob, key);

            byte cryptoMarker = blob[0];

            if (cryptoMarker == CryptoSchema.MarkerAesGcmV1)
            {
                byte[] result = DecryptAesGcmV1(blob.AsSpan(1), key);
                this.logger.LogDebug("Decrypted {BlobLength} bytes to {ResultLength} bytes", blob.Length, result.Length);

                return result;
            }
            else
            {
                this.logger.LogError("Unknown crypto marker encountered: 0x{CryptoMarker:X2}", cryptoMarker);

                throw new CryptographicException($"Unknown crypto marker 0x{cryptoMarker:X2}");
            }
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.SecurelyZeroMemory();
            this.disposed = true;

            this.logger.LogDebug("EncryptionService disposed and memory zeroed");
        }

        #endregion

        #region EncryptionService: Master Key Management

        private async Task<byte[]> EnsureMasterKeyLoadedAsync()
        {
            byte[] loadedKey = await this.masterKeyTask.Value.ConfigureAwait(false);
            this.masterKey = loadedKey;

            return loadedKey;
        }
        private async Task<byte[]> LoadOrCreateMasterKeyAsync()
        {
            ReadOnlyMemory<byte>? existingKey = await this.TryLoadExistingMasterKeyAsync().ConfigureAwait(false);

            bool keyExists = existingKey.HasValue;
            bool keyIsValidSize = keyExists && existingKey.Value.Length == KeySizeBytes;

            if (keyIsValidSize)
            {
                this.logger.LogDebug("Loaded existing master key");

                return existingKey.Value.ToArray();
            }

            return await this.CreateNewMasterKeyWithLocking().ConfigureAwait(false);
        }
        private async Task<ReadOnlyMemory<byte>?> TryLoadExistingMasterKeyAsync()
        {
            try
            {
                return await this.storage.TryLoadAsync(this.masterKeyId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning("Failed to load existing master key: {Error}", ex.Message);

                return null;
            }
        }
        private async Task<byte[]> CreateNewMasterKeyWithLocking()
        {
            await systemLock.WaitAsync().ConfigureAwait(false);

            try
            {
                ReadOnlyMemory<byte>? doubleCheckKey = await this.TryLoadExistingMasterKeyAsync().ConfigureAwait(false);

                bool keyExistsAfterLock = doubleCheckKey.HasValue;
                bool keyIsValidAfterLock = keyExistsAfterLock && doubleCheckKey.Value.Length == KeySizeBytes;

                if (keyIsValidAfterLock)
                {
                    this.logger.LogDebug("Master key was created by another thread during lock wait");
                    return doubleCheckKey.Value.ToArray();
                }

                return await this.GenerateAndSaveNewMasterKey().ConfigureAwait(false);
            }
            finally
            {
                systemLock.Release();
            }
        }
        private async Task<byte[]> GenerateAndSaveNewMasterKey()
        {
            byte[] newKey = GenerateSecureRandomKey();

            try
            {
                await this.storage.SaveAsync(this.masterKeyId, newKey).ConfigureAwait(false);

                this.logger.LogInformation("Generated and saved new master key");

                return newKey;
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to save new master key: {Error}", ex.Message);

                CryptographicOperations.ZeroMemory(newKey.AsSpan());

                throw;
            }
        }

        #endregion

        #region EncryptionService: Context Key Management

        private async Task<byte[]> LoadOrCreateContextKeyAsync(string contextBlobId, byte[] masterKey)
        {
            byte[]? encryptedContextKey = await this.LoadExistingContextKeyAsync(contextBlobId).ConfigureAwait(false);

            if (encryptedContextKey != null)
            {
                return this.Decrypt(encryptedContextKey, masterKey);
            }
            else
            {
                return await this.CreateNewContextKeyAsync(contextBlobId, masterKey).ConfigureAwait(false);
            }
        }
        private async Task<byte[]?> LoadExistingContextKeyAsync(string contextBlobId)
        {
            try
            {
                ReadOnlyMemory<byte>? contextKeyData = await this.storage.TryLoadAsync(contextBlobId).ConfigureAwait(false);

                bool contextDataExists = contextKeyData.HasValue;

                if (contextDataExists)
                {
                    this.logger.LogDebug("Loaded existing context key for blob ID: {BlobId}", contextBlobId);
                    return contextKeyData.Value.ToArray();
                }

                return null;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning("Failed to load context key for blob ID {BlobId}: {Error}", contextBlobId, ex.Message);

                return null;
            }
        }
        private async Task<byte[]> CreateNewContextKeyAsync(string contextBlobId, byte[] masterKey)
        {
            byte[] newContextKey = GenerateSecureRandomKey();

            try
            {
                byte[] encryptedContextKey = this.Encrypt(newContextKey, masterKey);

                await this.storage.SaveAsync(contextBlobId, encryptedContextKey).ConfigureAwait(false);

                this.logger.LogDebug("Created and saved new context key for blob ID: {BlobId}", contextBlobId);

                return newContextKey;
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to save new context key for blob ID {BlobId}: {Error}", contextBlobId, ex.Message);

                CryptographicOperations.ZeroMemory(newContextKey.AsSpan());

                throw;
            }
        }

        #endregion

        #region EncryptionService: Encryption Operations

        private static EncryptionBuffers CreateEncryptionBuffers(int plaintextLength)
        {
            return new EncryptionBuffers
            {
                Ciphertext = new byte[plaintextLength],
                Tag = new byte[TagSize]
            };
        }
        private static void PerformAesGcmEncryption(byte[] plaintext, byte[] key, byte[] nonce, EncryptionBuffers buffers)
        {
            using (AesGcm aes = new(key, TagSize))
            {
                aes.Encrypt(nonce, plaintext, buffers.Ciphertext, buffers.Tag);
            }
        }
        private static byte[] AssembleEncryptedResult(byte[] nonce, EncryptionBuffers buffers)
        {
            byte[] result = new byte[1 + NonceSize + TagSize + buffers.Ciphertext.Length];

            result[0] = CryptoSchema.MarkerAesGcmV1;

            Buffer.BlockCopy(nonce, 0, result, 1, NonceSize);
            Buffer.BlockCopy(buffers.Tag, 0, result, 1 + NonceSize, TagSize);
            Buffer.BlockCopy(buffers.Ciphertext, 0, result, 1 + NonceSize + TagSize, buffers.Ciphertext.Length);

            return result;
        }
        private static byte[] DecryptAesGcmV1(ReadOnlySpan<byte> span, byte[] key)
        {
            ReadOnlySpan<byte> nonce = span[..NonceSize];
            ReadOnlySpan<byte> tag = span.Slice(NonceSize, TagSize);
            ReadOnlySpan<byte> data = span[(NonceSize + TagSize)..];

            byte[] plaintext = new byte[data.Length];

            using (AesGcm aes = new(key, TagSize))
            {
                aes.Decrypt(nonce, data, tag, plaintext);
            }

            return plaintext;
        }

        #endregion

        #region EncryptionService: Validation and Helpers

        private void ValidateEncryptionKey(byte[] key)
        {
            bool keyIsCorrectSize = key.Length == KeySizeBytes;

            if (!keyIsCorrectSize)
            {
                this.logger.LogError("Invalid encryption key size: {ActualSize}, expected: {ExpectedSize}", key.Length, KeySizeBytes);

                throw new ArgumentException($"Key must be {KeySizeBytes} bytes, got {key.Length}", nameof(key));
            }
        }
        private void ValidateDecryptionInputs(byte[] blob, byte[] key)
        {
            bool blobHasMinimumSize = blob.Length >= 1 + NonceSize + TagSize;
            bool keyIsCorrectSize = key.Length == KeySizeBytes;

            if (!blobHasMinimumSize)
            {
                this.logger.LogError("Blob too small for decryption: {ActualSize}, minimum required: {MinimumSize}", blob.Length, 1 + NonceSize + TagSize);

                throw new ArgumentException($"Blob too small for decryption, got {blob.Length} bytes", nameof(blob));
            }

            if (!keyIsCorrectSize)
            {
                this.logger.LogError("Invalid decryption key size: {ActualSize}, expected: {ExpectedSize}", key.Length, KeySizeBytes);

                throw new ArgumentException($"Key must be {KeySizeBytes} bytes, got {key.Length}", nameof(key));
            }
        }

        private void ThrowIfDisposed()
        {
            if (!this.disposed)
            {
                return;
            }

            throw new ObjectDisposedException(nameof(EncryptionService));
        }
        private void SecurelyZeroMemory()
        {
            CryptographicOperations.ZeroMemory(this.masterKey.AsSpan());
        }

        private static byte[] GenerateNonce()
        {
            return RandomNumberGenerator.GetBytes(NonceSize);
        }
        private static byte[] GenerateSecureRandomKey()
        {
            return RandomNumberGenerator.GetBytes(KeySizeBytes);
        }

        private static string BuildContextIdentifier(Type configType, Guid id)
        {
            byte[] rawIdentifier = Encoding.UTF8.GetBytes($"{configType.FullName}_{id:N}");
            byte[] hashedIdentifier = SHA256.HashData(rawIdentifier);

            return Convert.ToHexString(hashedIdentifier);
        }

        #endregion
    }
}
