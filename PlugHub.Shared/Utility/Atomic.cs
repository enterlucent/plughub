using System.Text;

namespace PlugHub.Shared.Utility
{
    /// <summary>
    /// Cross-platform helper for atomic, multi-process-safe file writes.
    /// </summary>
    public static class Atomic
    {
        /// <summary>Ensures the directory containing <paramref name="path"/> exists.</summary>
        private static void EnsureDirectoryExists(string path)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }



        /// <summary>
        /// UTF-8 helper that converts <paramref name="contents"/> to bytes and delegates to the primary writer.
        /// </summary>
        public static Task WriteAsync(string destinationPath, string contents, int maxRetries = 3, int baseDelayMs = 200, CancellationToken cancellationToken = default)
            => WriteAsync(destinationPath, Encoding.UTF8.GetBytes(contents), maxRetries, baseDelayMs, cancellationToken);

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

                try
                {
                    await using (FileStream fs =
                        new(
                        tempPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None | FileShare.Delete,
                        bufferSize: 8192,
                        options: FileOptions.Asynchronous | FileOptions.WriteThrough)) await fs.WriteAsync(bytes, cancellationToken);

                    if (File.Exists(destinationPath))
                        File.Replace(tempPath, destinationPath, null, ignoreMetadataErrors: true);
                    else
                        File.Move(tempPath, destinationPath);

                    return;
                }
                catch (IOException) when (attempt < maxRetries)
                {
                    await Task.Delay(baseDelayMs * (1 << (attempt - 1)), cancellationToken);
                }
                finally
                {
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignored */ }
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            throw new IOException($"Unable to write '{destinationPath}' after {maxRetries} attempts.");
        }



        /// <summary>
        /// Atomically writes UTF-8 text <paramref name="contents"/> to <paramref name="destinationPath"/>.
        /// </summary>
        public static void Write(string destinationPath, string contents) =>
            Write(destinationPath, Encoding.UTF8.GetBytes(contents));

        /// <summary>
        /// Atomically writes binary <paramref name="data"/> to <paramref name="destinationPath"/>.
        /// </summary>
        public static void Write(string destinationPath, ReadOnlySpan<byte> data)
        {
            ArgumentNullException.ThrowIfNull(destinationPath);

            string dir = Path.GetDirectoryName(destinationPath)!;
            Directory.CreateDirectory(dir);

            string tempPath = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(tempPath, data.ToArray());

            File.Move(tempPath, destinationPath, overwrite: true);
        }
    }
}