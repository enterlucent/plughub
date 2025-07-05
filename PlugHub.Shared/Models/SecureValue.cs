using PlugHub.Shared.Interfaces.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PlugHub.Shared.Models
{
    /// <summary>
    /// Decorates a property in a configuration POCO to indicate that its value
    /// is considered sensitive and must be persisted as encrypted cipher-text.
    /// </summary>
    /// <remarks>
    /// When PlugHub processes a configuration class it treats any property that is
    /// either of type <see cref="SecureValue"/> *or* marked with
    /// <see cref="SecureAttribute"/> as “secure”.  
    /// Secure values are automatically wrapped in <see cref="SecureValue"/> on write and
    /// transparently decrypted on read.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SecureAttribute : Attribute { }


    /// <summary>
    /// Lightweight envelope that stores an AES-GCM-encrypted payload as a Base-64 string
    /// and offers lazy, type-safe decryption.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Internal use</b> – <strong>SecureValue</strong> is part of PlugHub’s plumbing
    /// and is not intended to be constructed directly by application code.  Use the API
    /// exposed by <c>ISecureAccessor</c> instead.
    /// </para>
    /// <para>
    /// <u>Lifecycle</u><br/>
    /// – The cipher-text is supplied via the constructor and is immutable.<br/>
    /// – Plaintext bytes are decrypted the first time <see cref="As{T}"/> is called and
    /// kept in memory until <see cref="Dispose"/> is invoked, after which the buffer is
    /// zeroed out for hygiene.
    /// </para>
    /// </remarks>
    public sealed class SecureValue(string encryptedBase64) : IDisposable
    {
        private byte[]? decryptedBytes;
        private bool disposed;
        private readonly SemaphoreSlim aiolock = new(1, 1);

        /// <summary>
        /// Gets the encrypted payload in Base-64 form exactly as it appears on disk.
        /// </summary>
        public string EncryptedBase64 { get; } = encryptedBase64 ?? throw new ArgumentNullException(nameof(encryptedBase64));

        /// <summary>
        /// Decrypts the payload (on first call) and deserialises it to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Expected CLR type of the plaintext value.</typeparam>
        /// <param name="context">The encryption context that holds the matching key.</param>
        /// <returns>The decrypted value, or <c>default</c> when deserialisation fails.</returns>
        /// <exception cref="ObjectDisposedException">
        /// Thrown when the instance has been disposed.
        /// </exception>
        public T As<T>(IEncryptionContext context)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);

            this.aiolock.Wait();

            try
            {
                if (this.decryptedBytes is null)
                {
                    byte[] cipher = Convert.FromBase64String(this.EncryptedBase64);
                    this.decryptedBytes = context.EncryptionService.Decrypt(cipher, context.Key);
                }
            }
            finally { this.aiolock.Release(); }

            return JsonSerializer.Deserialize<T>(this.decryptedBytes)!;
        }

        /// <summary>
        /// Encrypts <paramref name="value"/> using <paramref name="context"/> and returns a
        /// new <see cref="SecureValue"/> that carries the resulting cipher-text.
        /// </summary>
        public static SecureValue From<T>(T value, IEncryptionContext context)
        {
            string json = JsonSerializer.Serialize(value);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptedBytes = context.EncryptionService.Encrypt(plainBytes, context.Key);
            string base64 = Convert.ToBase64String(encryptedBytes);
            return new SecureValue(base64);
        }

        /// <summary>
        /// Zeroes the plaintext buffer (if any) and marks the instance as unusable.
        /// </summary>
        public void Dispose()
        {
            if (this.disposed) return;

            this.aiolock.Wait();
            try
            {
                if (this.decryptedBytes != null)
                {
                    CryptographicOperations.ZeroMemory(this.decryptedBytes.AsSpan());
                    this.decryptedBytes = null;
                }
                this.disposed = true;
            }
            finally { this.aiolock.Release(); }

            this.aiolock.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public override string ToString() => this.EncryptedBase64;
    }
}