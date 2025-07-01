using PlugHub.Shared.Utility;

namespace PlugHub.UnitTests
{
    internal sealed class MSTestHelpers : IDisposable
    {
        public Guid Guid { get; set; }
        public string TempDirectory { get; set; }

        public MSTestHelpers()
        {
            this.Guid = Guid.NewGuid();
            this.TempDirectory = Path.Combine(Path.GetTempPath(), "PlugHubTest", this.Guid.ToString());
        }
        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(this.TempDirectory) && Directory.Exists(this.TempDirectory))
                Directory.Delete(this.TempDirectory, recursive: true);
        }

        public void CreateTempFile(string text, string filename)
            => Atomic.Write(Path.Combine(this.TempDirectory, filename), text);
    }
}
