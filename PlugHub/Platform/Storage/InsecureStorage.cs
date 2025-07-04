using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Platform.Storage;
using PlugHub.Shared.Interfaces.Services;
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
    public sealed class InsecureStorageConfig
    {
        public string BinaryDirectory { get; init; } =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PlugHub",
                "SecureStore");
    }

    public sealed class InsecureStorage : ISecureStorage
    {
        public event EventHandler<SecureStorageChangedEventArgs>? Changed;

        private readonly ILogger logger;
        private readonly ITokenService tokenService;
        private readonly IConfigService configService;
        private readonly ITokenSet configTokenSet;
        private readonly string binaryDirectory;
        private bool isDisposed;
        private readonly SemaphoreSlim systemLock = new(1, 1);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> storageLock = new();

        public InsecureStorage(ILogger<ISecureStorage> logger, ITokenService tokenService, IConfigService configService, string storeRootDirectory = "")
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(tokenService);
            ArgumentNullException.ThrowIfNull(configService);

            this.logger = logger;
            this.configService = configService;
            this.tokenService = tokenService;
            this.configTokenSet
                = this.tokenService
                    .CreateTokenSet(
                        this.tokenService.CreateToken(),
                        this.tokenService.CreateToken(),
                        this.tokenService.CreateToken());

            this.configService.RegisterConfig(typeof(InsecureStorageConfig), this.configTokenSet);

            if (string.IsNullOrWhiteSpace(storeRootDirectory))
            {
                InsecureStorageConfig config =
                    (InsecureStorageConfig)this.configService.GetConfigInstance(typeof(InsecureStorageConfig), this.configTokenSet);

                this.binaryDirectory = config.BinaryDirectory;
            }
            else this.binaryDirectory = storeRootDirectory;
        }

        public async ValueTask<ReadOnlyMemory<byte>?> TryLoadAsync(string id, CancellationToken ct = default)
        {
            ObjectDisposedException.ThrowIf(this.isDisposed, this);

            ValidateId(id);

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
            ObjectDisposedException.ThrowIf(this.isDisposed, this);

            ValidateId(id);

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
            ObjectDisposedException.ThrowIf(this.isDisposed, this);
            ValidateId(id);

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
            ObjectDisposedException.ThrowIf(this.isDisposed, this);

            if (!Directory.Exists(this.binaryDirectory))
                yield break;

            IEnumerable<string> files;

            await this.systemLock.WaitAsync(ct).ConfigureAwait(false);

            this.logger.LogTrace("SecureStore: listing ids (prefix = '{Prefix}')", prefix);

            try
            {
                files = Directory
                    .EnumerateFiles(this.binaryDirectory, "*.bin", SearchOption.TopDirectoryOnly)
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
                this.configService.UnregisterConfig(typeof(InsecureStorageConfig), this.configTokenSet);
            }
            finally { this.systemLock.Release(); }

            GC.SuppressFinalize(this);
        }

        private SemaphoreSlim GetStorageLock(string id)
            => this.storageLock.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(
                    "binary id must be a non-empty string.",
                    nameof(id));

            if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException(
                    "binary id contains invalid path characters.",
                    nameof(id));
        }
        private string GetBinaryPath(string id) =>
            Path.Combine(this.binaryDirectory, $"{id}.bin");
    }
}