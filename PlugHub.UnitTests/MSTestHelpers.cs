using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Utility;
using Serilog;

namespace PlugHub.UnitTests
{
    internal sealed class MSTestHelpers : IDisposable
    {
        public Guid Guid { get; init; }
        public string TempDirectory { get; init; }
        public string PluginDirectory { get; init; }


        public MSTestHelpers()
        {
            this.Guid = Guid.NewGuid();
            this.TempDirectory = Path.Combine(Path.GetTempPath(), "PlugHubTest", this.Guid.ToString());
            this.PluginDirectory = Path.Combine(GetDesktopProjectOutputPath(), "Plugins");
        }

        public static string GetDesktopProjectOutputPath()
        {
            string testDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            DirectoryInfo dir = new(testDir);

            string rid = System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier;
            string tfm = dir.Name;
            string config = dir.Parent!.Name;
            string bin = dir.Parent!.Parent!.Name;
            string desktopProj = "PlugHub.Desktop";
            string solutionRoot = dir.Parent!.Parent!.Parent!.Parent!.FullName;

            return Path.Combine(solutionRoot, desktopProj, bin, config, tfm, rid);
        }


        public void CreateTempFile(string text, string filename)
            => Atomic.Write(Path.Combine(this.TempDirectory, filename), text);

        public ServiceCollection CreateTempServiceCollection()
        {
            ServiceCollection services = [];

            services.AddLogging(builder =>
            {
                Directory.CreateDirectory(this.TempDirectory);

                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.File(Path.Combine(this.TempDirectory, "UnitTest.logs"),
                        rollingInterval: RollingInterval.Infinite)
                    .CreateLogger();

                builder.ClearProviders();
                builder.AddSerilog(Log.Logger, dispose: true);
            });

            return services;
        }

        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(this.TempDirectory) && Directory.Exists(this.TempDirectory))
                Directory.Delete(this.TempDirectory, recursive: true);
        }
    }
}