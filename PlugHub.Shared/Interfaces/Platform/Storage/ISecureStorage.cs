namespace PlugHub.Shared.Interfaces.Platform.Storage
{
    public sealed record SecureStorageChangedEventArgs(string Id, SecureStorageChangeKind Kind);

    public enum SecureStorageChangeKind
    {
        Added,
        Updated,
        Deleted
    }

    /// <summary>
    ///  Minimal, provider-agnostic secure blob store.
    ///  Accepts opaque, already-encrypted data and returns it unchanged.
    /// </summary>
    public interface ISecureStorage : IDisposable
    {
        /// <summary>Raised when a blob is added, updated, or deleted by this process.</summary>
        public event EventHandler<SecureStorageChangedEventArgs>? Changed;


        /// <summary>Saves or replaces a blob under the given <paramref name="id"/>.</summary>
        public ValueTask SaveAsync(string id, ReadOnlyMemory<byte> data, CancellationToken ct = default);

        /// <summary>
        ///  Attempts to load a blob; returns <c>null</c> when the id is unknown.
        /// </summary>
        public ValueTask<ReadOnlyMemory<byte>?> TryLoadAsync(string id, CancellationToken ct = default);

        /// <summary>Deletes the blob if it exists (no-op otherwise).</summary>
        public ValueTask DeleteAsync(string id, CancellationToken ct = default);


        /// <summary>
        ///  Streams all stored ids; optionally filters by <paramref name="prefix"/>.
        /// </summary>
        public IAsyncEnumerable<string> ListIdsAsync(string? prefix = null, CancellationToken ct = default);


        /// <summary>
        ///  Re-encrypts every stored blob with whatever new key/material
        ///  the implementation chooses.  Callers don’t supply crypto details;
        ///  they just get told when the job is done.
        /// </summary>
        public ValueTask RotateEncryptionAsync(CancellationToken ct = default);
    }
}