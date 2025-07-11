using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Platform.Storage
{
    public sealed class InsecureStorage : ISecureStorage
    {
        public event EventHandler<SecureStorageChangedEventArgs>? Changed;

        private readonly ILogger logger;
        private string? storeRootDirectory;
        private bool isDisposed;
        private readonly SemaphoreSlim systemLock = new(1, 1);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> storageLock = new();

        public InsecureStorage(ILogger<ISecureStorage> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
            this.storeRootDirectory = null;
        }


        public void Initialize(string newRootDirectory, bool moveExisting = true, bool overwrite = false)
        {
            if (string.IsNullOrWhiteSpace(newRootDirectory))
                throw new ArgumentException("New storage directory cannot be blank.", nameof(newRootDirectory));

            if (moveExisting && this.storeRootDirectory != null && Directory.Exists(this.storeRootDirectory))
            {
                foreach (var file in Directory.GetFiles(this.storeRootDirectory, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(this.storeRootDirectory, file);
                    var destFile = Path.Combine(newRootDirectory, relativePath);

                    if (File.Exists(destFile) && !overwrite)
                        continue;

                    string? dirname = Path.GetDirectoryName(destFile);

                    if (dirname == null)
                        continue;

                    Directory.CreateDirectory(dirname);

                    File.Move(file, destFile, overwrite);
                }
            }
            this.storeRootDirectory = newRootDirectory;
        }


        public async ValueTask<ReadOnlyMemory<byte>?> TryLoadAsync(string id, CancellationToken ct = default)
        {
            EnsureUndisposed(this.isDisposed, this);
            EnsureDirectorySet(this.storeRootDirectory);
            EnsureValidId(id);

            string path = this.GetBinaryPath(id);

            byte[] bytes;

            if (!File.Exists(path))
            {
                this.logger.LogDebug("SecureStore: [{Id}] not found – returning null", id);
                return null;
            }

            SemaphoreSlim aioLock = this.GetStorageLock(id);
            await aioLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                bytes = await File.ReadAllBytesAsync(path, ct);

                this.logger.LogDebug("SecureStore: [{Id}] loaded {Count} bytes", id, bytes.Length);
            }
            finally { aioLock.Release(); }

            return bytes;
        }
        public async ValueTask SaveAsync(string id, ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            EnsureUndisposed(this.isDisposed, this);
            EnsureDirectorySet(this.storeRootDirectory);
            EnsureValidId(id);

            string path = this.GetBinaryPath(id);

            bool updating = false;

            SemaphoreSlim aioLock = this.GetStorageLock(id);

            await aioLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                updating = File.Exists(path);

                this.logger.LogDebug("SecureStore: [{Id}] saving {Length} bytes (update = {Updating})", id, data.Length, updating);

                await Atomic.WriteAsync(path, data, cancellationToken: ct);

                this.logger.LogInformation("SecureStore: [{Id}] {Action}", id, updating ? "updated" : "added");
            }
            finally { aioLock.Release(); }

            Changed?.Invoke(this,
                new SecureStorageChangedEventArgs(
                    id,
                    updating ? SecureStorageChangeKind.Updated : SecureStorageChangeKind.Added));
        }
        public async ValueTask DeleteAsync(string id, CancellationToken ct = default)
        {
            EnsureUndisposed(this.isDisposed, this);
            EnsureDirectorySet(this.storeRootDirectory);
            EnsureValidId(id);

            string path = this.GetBinaryPath(id);

            SemaphoreSlim aioLock = this.GetStorageLock(id);

            await aioLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                if (!File.Exists(path))
                {
                    this.logger.LogDebug("SecureStore: [{Id}] delete requested but file missing", id);
                    return;
                }

                File.Delete(path);

                this.logger.LogInformation("SecureStore: [{Id}] deleted", id);
                this.storageLock.TryRemove(id, out _);
            }
            finally { aioLock.Release(); }

            Changed?.Invoke(this,
                new SecureStorageChangedEventArgs(id, SecureStorageChangeKind.Deleted));
        }


        public async IAsyncEnumerable<string> ListIdsAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            EnsureUndisposed(this.isDisposed, this);
            EnsureDirectorySet(this.storeRootDirectory);

            IEnumerable<string> files;

            await this.systemLock.WaitAsync(ct).ConfigureAwait(false);

            this.logger.LogTrace("SecureStore: listing ids (prefix = '{Prefix}')", prefix);

            try
            {
                files = Directory
                    .EnumerateFiles(this.storeRootDirectory!, "*.bin", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => name is not null)
                    .Select(name => name!);
            }
            finally { this.systemLock.Release(); }

            foreach (string id in files)
            {
                if (ct.IsCancellationRequested)
                    yield break;

                if (prefix is null || id.StartsWith(prefix, StringComparison.Ordinal))
                    yield return id;

                await Task.Yield();
            }
        }
        public ValueTask RotateEncryptionAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public void Dispose()
        {
            if (this.isDisposed) return;

            this.systemLock.Wait();

            try
            {
                this.isDisposed = true;
            }
            finally { this.systemLock.Release(); }

            GC.SuppressFinalize(this);
        }

        private SemaphoreSlim GetStorageLock(string id)
            => this.storageLock.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        private string GetBinaryPath(string id)
            => Path.Combine(this.storeRootDirectory!, $"{id}.bin");


        private static void EnsureValidId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("binary id must be a non-empty string.", nameof(id));

            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("binary id contains invalid path characters.", nameof(id));
        }
        private static void EnsureUndisposed(bool isDisposed, object sender)
        {
            ObjectDisposedException.ThrowIf(isDisposed, sender);
        }
        private static void EnsureDirectorySet(string? storageRootDirectory)
        {
            if (string.IsNullOrWhiteSpace(storageRootDirectory))
                throw new InvalidOperationException("Storage directory is not set. Set the directory before using storage.");
        }
    }
}