using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Services.Plugins
{
    public interface IPluginCache
    {
        IEnumerable<PluginReference> Plugins { get; }
    }
}