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
        private readonly SemaphoreSlim systemLock = new(1, 1);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> storageLock = new();

        private string? storeRootDirectory;
        private bool isDisposed;

        public InsecureStorage(ILogger<ISecureStorage> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
            this.storeRootDirectory = null;
        }

        #region InsecureStorage: Initialization

        public void Initialize(string newRootDirectory, bool moveExisting = true, bool overwrite = false)
        {
            ArgumentNullException.ThrowIfNull(newRootDirectory);

            ValidateInitializationParameters(newRootDirectory);

            bool shouldMigrateFiles = this.ShouldMigrateExistingFiles(moveExisting);

            if (shouldMigrateFiles)
            {
                this.MigrateExistingFiles(newRootDirectory, overwrite);
            }

            this.storeRootDirectory = newRootDirectory;

            this.logger.LogInformation("Storage initialized with directory: {Directory}", newRootDirectory);
        }

        #endregion

        #region InsecureStorage: Public API

        public async ValueTask<ReadOnlyMemory<byte>?> TryLoadAsync(string id, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(id);

            this.ValidateStorageState();
            ValidateId(id);

            string filePath = this.GetBinaryPath(id);

            bool fileExists = File.Exists(filePath);

            if (!fileExists)
            {
                this.logger.LogDebug("SecureStore: [{Id}] not found – returning null", id);

                return null;
            }

            return await this.LoadFileWithLocking(id, filePath, ct).ConfigureAwait(false);
        }
        public async ValueTask SaveAsync(string id, ReadOnlyMemory<byte> data, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(data);

            this.ValidateStorageState();
            ValidateId(id);

            string filePath = this.GetBinaryPath(id);

            await this.SaveFileWithLocking(id, filePath, data, ct).ConfigureAwait(false);
        }
        public async ValueTask DeleteAsync(string id, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(id);

            this.ValidateStorageState();
            ValidateId(id);

            string filePath = this.GetBinaryPath(id);

            await this.DeleteFileWithLocking(id, filePath, ct).ConfigureAwait(false);
        }
        public async IAsyncEnumerable<string> ListIdsAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken ct = default)
        {
            this.ValidateStorageState();

            IEnumerable<string> discoveredFiles = await this.DiscoverStorageFiles(prefix, ct).ConfigureAwait(false);

            foreach (string id in discoveredFiles)
            {
                bool cancellationRequested = ct.IsCancellationRequested;

                if (cancellationRequested)
                {
                    yield break;
                }

                bool matchesPrefix = DoesIdMatchOptionalPrefix(id, prefix);

                if (matchesPrefix)
                {
                    yield return id;
                }

                await Task.Yield();
            }
        }

        public ValueTask RotateEncryptionAsync(CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.PerformDisposal();

            GC.SuppressFinalize(this);
        }

        #endregion

        #region InsecureStorage: File Operations

        private async Task<ReadOnlyMemory<byte>> LoadFileWithLocking(string id, string filePath, CancellationToken ct)
        {
            SemaphoreSlim fileLock = this.GetStorageLock(id);

            await fileLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                byte[] fileData = await File.ReadAllBytesAsync(filePath, ct).ConfigureAwait(false);

                this.logger.LogDebug("SecureStore: [{Id}] loaded {Count} bytes", id, fileData.Length);

                return fileData;
            }
            finally
            {
                fileLock.Release();
            }
        }
        private async Task SaveFileWithLocking(string id, string filePath, ReadOnlyMemory<byte> data, CancellationToken ct)
        {
            SemaphoreSlim fileLock = this.GetStorageLock(id);

            await fileLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                bool isUpdate = File.Exists(filePath);

                this.logger.LogDebug("SecureStore: [{Id}] saving {Length} bytes (update = {Updating})", id, data.Length, isUpdate);

                await Atomic.WriteAsync(filePath, data, cancellationToken: ct).ConfigureAwait(false);

                string operationType = isUpdate ? "updated" : "added";
                SecureStorageChangeKind changeKind = isUpdate ? SecureStorageChangeKind.Updated : SecureStorageChangeKind.Added;

                this.logger.LogInformation("SecureStore: [{Id}] {Action}", id, operationType);

                this.RaiseStorageChangedEvent(id, changeKind);
            }
            finally
            {
                fileLock.Release();
            }
        }
        private async Task DeleteFileWithLocking(string id, string filePath, CancellationToken ct)
        {
            SemaphoreSlim fileLock = this.GetStorageLock(id);

            await fileLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                bool fileExists = File.Exists(filePath);

                if (!fileExists)
                {
                    this.logger.LogDebug("SecureStore: [{Id}] delete requested but file missing", id);
                    return;
                }

                File.Delete(filePath);

                this.logger.LogInformation("SecureStore: [{Id}] deleted", id);

                this.RemoveStorageLock(id);
                this.RaiseStorageChangedEvent(id, SecureStorageChangeKind.Deleted);
            }
            finally
            {
                fileLock.Release();
            }
        }

        #endregion

        #region InsecureStorage: Migration and Discovery

        private static void ValidateInitializationParameters(string newRootDirectory)
        {
            bool directoryIsEmpty = string.IsNullOrWhiteSpace(newRootDirectory);

            if (directoryIsEmpty)
            {
                throw new ArgumentException("New storage directory cannot be blank.", nameof(newRootDirectory));
            }
        }

        private bool ShouldMigrateExistingFiles(bool moveExisting)
        {
            bool hasMoveRequest = moveExisting;
            bool hasCurrentDirectory = this.storeRootDirectory != null;
            bool currentDirectoryExists = hasCurrentDirectory && Directory.Exists(this.storeRootDirectory);

            return hasMoveRequest && hasCurrentDirectory && currentDirectoryExists;
        }

        private void MigrateExistingFiles(string newRootDirectory, bool overwrite)
        {
            string[] existingFiles = Directory.GetFiles(this.storeRootDirectory!, "*", SearchOption.AllDirectories);

            foreach (string sourceFile in existingFiles)
            {
                bool migrationSuccessful = this.TryMigrateFile(sourceFile, newRootDirectory, overwrite);

                if (migrationSuccessful)
                {
                    this.logger.LogDebug("Migrated file: {SourceFile}", sourceFile);
                }
            }

            this.logger.LogInformation("Completed file migration from {OldDirectory} to {NewDirectory}", this.storeRootDirectory, newRootDirectory);
        }

        private bool TryMigrateFile(string sourceFile, string newRootDirectory, bool overwrite)
        {
            try
            {
                string relativePath = Path.GetRelativePath(this.storeRootDirectory!, sourceFile);
                string destinationFile = Path.Combine(newRootDirectory, relativePath);

                bool destinationExists = File.Exists(destinationFile);
                bool shouldSkip = destinationExists && !overwrite;

                if (shouldSkip)
                {
                    return false;
                }

                string? destinationDirectory = Path.GetDirectoryName(destinationFile);

                if (destinationDirectory == null)
                {
                    return false;
                }

                Directory.CreateDirectory(destinationDirectory);
                File.Move(sourceFile, destinationFile, overwrite);

                return true;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning("Failed to migrate file {SourceFile}: {Error}", sourceFile, ex.Message);
                return false;
            }
        }

        private async Task<IEnumerable<string>> DiscoverStorageFiles(string? prefix, CancellationToken ct)
        {
            await this.systemLock.WaitAsync(ct).ConfigureAwait(false);

            try
            {
                this.logger.LogTrace("SecureStore: listing ids (prefix = '{Prefix}')", prefix);

                IEnumerable<string> discoveredFiles = Directory
                    .EnumerateFiles(this.storeRootDirectory!, "*.bin", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(name => name is not null)
                    .Select(name => name!);

                return [.. discoveredFiles];
            }
            finally
            {
                this.systemLock.Release();
            }
        }

        private static bool DoesIdMatchOptionalPrefix(string id, string? prefix)
        {
            if (prefix == null)
            {
                return true;
            }

            return id.StartsWith(prefix, StringComparison.Ordinal);
        }

        #endregion

        #region InsecureStorage: Storage Management

        private SemaphoreSlim GetStorageLock(string id)
            => this.storageLock.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
        private void RemoveStorageLock(string id)
        {
            this.storageLock.TryRemove(id, out _);
        }

        private string GetBinaryPath(string id)
            => Path.Combine(this.storeRootDirectory!, $"{id}.bin");

        private void RaiseStorageChangedEvent(string id, SecureStorageChangeKind changeKind)
        {
            Changed?.Invoke(this, new SecureStorageChangedEventArgs(id, changeKind));
        }

        private void PerformDisposal()
        {
            this.systemLock.Wait();

            try
            {
                this.isDisposed = true;
                this.logger.LogDebug("InsecureStorage disposed");
            }
            finally
            {
                this.systemLock.Release();
            }
        }

        #endregion

        #region InsecureStorage: Validation and Helpers

        private void ValidateStorageState()
        {
            this.ThrowIfDisposed();
            this.ValidateDirectorySet();
        }
        private static void ValidateId(string id)
        {
            bool idIsEmpty = string.IsNullOrWhiteSpace(id);

            if (idIsEmpty)
            {
                throw new ArgumentException("binary id must be a non-empty string.", nameof(id));
            }

            bool hasInvalidCharacters = id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0;

            if (hasInvalidCharacters)
            {
                throw new ArgumentException("binary id contains invalid path characters.", nameof(id));
            }
        }
        private void ValidateDirectorySet()
        {
            bool directoryNotSet = string.IsNullOrWhiteSpace(this.storeRootDirectory);

            if (directoryNotSet)
            {
                throw new InvalidOperationException("Storage directory is not set. Set the directory before using storage.");
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);
        }

        #endregion
    }
}