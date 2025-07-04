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
        private readonly ISecureStorage storage;
        private readonly ILogger logger;

        private readonly Lazy<Task<byte[]>> masterKeyTask;
        private readonly string masterKeyId;
        private byte[] masterKey = new byte[32];

        private bool disposed;
        private static readonly SemaphoreSlim systemLock = new(1, 1);

        public const int KeySizeBytes = 32;
        public const int TagSize = 16;
        public const int NonceSize = 12;

        public EncryptionService(ILogger<IEncryptionService> logger, ISecureStorage storage, string masterKeyId = "plughub-masterkey")
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.masterKeyId = masterKeyId;
            this.masterKeyTask = new Lazy<Task<byte[]>>(this.LoadOrCreateMasterKeyAsync);
        }

        public IEncryptionContext GetEncryptionContext<T>(Guid id)
            => this.GetEncryptionContextAsync<T>(id)
                   .AsTask()
                   .GetAwaiter()
                   .GetResult();
        public async ValueTask<IEncryptionContext> GetEncryptionContextAsync<T>(Guid id)
        {
            this.masterKey = await this.masterKeyTask.Value.ConfigureAwait(false);

            string contextBlobId = BuildOpaqueId<T>(id);

            byte[]? encryptedContextKey = this.LoadKey(contextBlobId);

            if (encryptedContextKey is null)
            {
                byte[] contextKey = GenerateRandomKey();
                encryptedContextKey = this.Encrypt(contextKey, this.masterKey);
                this.SaveKey(contextBlobId, encryptedContextKey);
            }

            byte[] plainKey = this.Decrypt(encryptedContextKey, this.masterKey);
            return new EncryptionContext(this, plainKey);
        }

        private async Task<byte[]> LoadOrCreateMasterKeyAsync()
        {
            if (await this.storage.TryLoadAsync(this.masterKeyId) is { Length: 32 } key)
                return key.ToArray();

            await systemLock.WaitAsync();

            try
            {
                if (await this.storage.TryLoadAsync(this.masterKeyId) is { Length: 32 } existing)
                    return existing.ToArray();

                byte[] newKey = RandomNumberGenerator.GetBytes(32);
                await this.storage.SaveAsync(this.masterKeyId, newKey);
                return newKey;
            }
            finally { systemLock.Release(); }
        }

        public byte[] Encrypt(byte[] plaintext, byte[] key)
        {
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] ciphertext = new byte[plaintext.Length];
            byte[] tag = new byte[TagSize];

            using AesGcm aes = new(key, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

            byte[] result = new byte[1 + NonceSize + TagSize + ciphertext.Length];
            result[0] = CryptoSchema.MarkerAesGcmV1;

            Buffer.BlockCopy(nonce, 0, result, 1, NonceSize);
            Buffer.BlockCopy(tag, 0, result, 1 + NonceSize, TagSize);
            Buffer.BlockCopy(ciphertext, 0, result, 1 + NonceSize + TagSize, ciphertext.Length);

            return result;
        }
        public byte[] Decrypt(byte[] blob, byte[] key)
        {
            return blob[0] switch
            {
                CryptoSchema.MarkerAesGcmV1 => DecryptAesGcmV1(blob.AsSpan(1), key),
                _ => throw new CryptographicException($"Unknown crypto marker 0x{blob[0]:X2}")
            };
        }

        public void Dispose()
        {
            if (this.disposed) return;

            CryptographicOperations.ZeroMemory(this.masterKey.AsSpan());

            this.disposed = true;
        }

        private byte[]? LoadKey(string blobId)
        {
            ReadOnlyMemory<byte>? mem = this.storage
                .TryLoadAsync(blobId)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            return mem.HasValue ? mem.Value.ToArray() : null;
        }
        private void SaveKey(string blobId, byte[] key)
        {
            this.storage
                .SaveAsync(blobId, key)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        private static byte[] GenerateRandomKey()
        {
            byte[] key = new byte[KeySizeBytes];
            RandomNumberGenerator.Fill(key);
            return key;
        }
        private static string BuildOpaqueId<T>(Guid id)
        {
            byte[] raw = Encoding.UTF8.GetBytes($"{typeof(T).FullName}_{id:N}");
            byte[] hash = SHA256.HashData(raw);
            return Convert.ToHexString(hash);
        }
        private static byte[] DecryptAesGcmV1(ReadOnlySpan<byte> span, byte[] key)
        {
            ReadOnlySpan<byte> nonce = span[..NonceSize];
            ReadOnlySpan<byte> tag = span.Slice(NonceSize, TagSize);
            ReadOnlySpan<byte> data = span[(NonceSize + TagSize)..];

            byte[] plaintext = new byte[data.Length];
            using AesGcm aes = new(key, TagSize);
            aes.Decrypt(nonce, data, tag, plaintext);
            return plaintext;
        }
    }
}
