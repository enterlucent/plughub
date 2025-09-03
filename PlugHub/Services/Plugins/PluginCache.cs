using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models.Plugins;
using System.Collections.Generic;

namespace PlugHub.Services.Plugins
{
    public sealed class PluginCache(IEnumerable<PluginReference> plugins) : IPluginCache
    {
        private readonly List<PluginReference> plugins = [.. plugins];

        public IEnumerable<PluginReference> Plugins => this.plugins;
    }
}