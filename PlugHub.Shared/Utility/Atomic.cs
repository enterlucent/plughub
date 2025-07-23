using System.Text;

namespace PlugHub.Shared.Utility
{
    /// <summary>
    /// Cross-platform helper for atomic, multi-process-safe file writes.
    /// </summary>
    public static class Atomic
    {
        /// <summary>
        /// Atomically writes UTF-8 text <paramref name="contents"/> to <paramref name="destinationPath"/>.
        /// </summary>
        public static void Write(string destinationPath, string contents) =>
            Write(destinationPath, Encoding.UTF8.GetBytes(contents));

        /// <summary>
        /// UTF-8 helper that converts <paramref name="contents"/> to bytes and delegates to the primary writer.
        /// </summary>
        public static Task WriteAsync(string destinationPath, string contents, int maxRetries = 3, int baseDelayMs = 200, CancellationToken cancellationToken = default)
            => WriteAsync(destinationPath, Encoding.UTF8.GetBytes(contents), maxRetries, baseDelayMs, cancellationToken);


        /// <summary>
        /// Atomically writes binary <paramref name="data"/> to <paramref name="destinationPath"/>.
        /// </summary>
        public static void Write(string destinationPath, ReadOnlySpan<byte> data)
        {
            ArgumentNullException.ThrowIfNull(destinationPath);

            string dir = Path.GetDirectoryName(destinationPath)!;
            bool directoryExists = Directory.Exists(dir);

            if (!directoryExists)
            {
                Directory.CreateDirectory(dir);
            }

            string tempPath = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(tempPath, data.ToArray());

            File.Move(tempPath, destinationPath, overwrite: true);
        }

        /// <summary>
        /// Atomically writes <paramref name="bytes"/> to <paramref name="destinationPath"/> asynchronously.
        /// Retries up to <paramref name="maxRetries"/> times on transient <see cref="IOException"/>s.
        /// </summary>
        public static async Task WriteAsync(string destinationPath, ReadOnlyMemory<byte> bytes, int maxRetries = 3, int baseDelayMs = 200, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(destinationPath);
            ArgumentNullException.ThrowIfNull(bytes);

            cancellationToken.ThrowIfCancellationRequested();

            EnsureDirectoryExists(destinationPath);

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                string tempPath = Path.GetTempFileName();
                bool isLastAttempt = (attempt == maxRetries);

                try
                {
                    // Stage 1: Write to temporary file with optimal settings
                    await using (FileStream fs = new(
                        tempPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None | FileShare.Delete,
                        bufferSize: 8192,
                        options: FileOptions.Asynchronous | FileOptions.WriteThrough))
                    {
                        await fs.WriteAsync(bytes, cancellationToken);
                    }

                    // Stage 2: Atomically move to final destination
                    bool destinationExists = File.Exists(destinationPath);

                    if (destinationExists)
                    {
                        File.Replace(tempPath, destinationPath, null, ignoreMetadataErrors: true);
                    }
                    else
                    {
                        File.Move(tempPath, destinationPath);
                    }

                    return;
                }
                catch (IOException) when (!isLastAttempt)
                {
                    await Task.Delay(baseDelayMs * (1 << (attempt - 1)), cancellationToken);
                }
                finally
                {
                    CleanupTempFile(tempPath);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            // Stage 3: Throw exception for max retries exceeded
            throw new IOException($"Unable to write '{destinationPath}' after {maxRetries} attempts.");
        }


        #region Atomic Helpers

        private static void EnsureDirectoryExists(string path)
        {
            string? dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }

        private static void CleanupTempFile(string tempPath)
        {
            try
            {
                bool tempFileExists = File.Exists(tempPath);
                if (tempFileExists)
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
                // Cleanup failures are non-critical - ignored intentionally
            }
        }

        #endregion
    }
}