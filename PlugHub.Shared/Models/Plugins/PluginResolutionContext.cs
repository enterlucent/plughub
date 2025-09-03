using PlugHub.Shared.Extensions;


namespace PlugHub.Shared.Models.Plugins
{
    public class PluginResolutionContext<TDescriptor>(List<TDescriptor> descriptors) where TDescriptor : PluginDescriptor
    {
        public Dictionary<Guid, TDescriptor> IdToDescriptor { get; } =
            descriptors.ToDictionary(d => d.DescriptorID, d => d);

        public HashSet<TDescriptor> DependencyDisabled { get; } = [];
        public HashSet<TDescriptor> ConflictDisabled { get; } = [];

        public Dictionary<TDescriptor, HashSet<TDescriptor>> Graph { get; } =
            descriptors.ToDictionary(d => d, _ => new HashSet<TDescriptor>());

        public IReadOnlyList<TDescriptor> GetSorted()
            => [.. this.Graph.Keys.TopologicalSort(d => this.Graph[d])];
    }
}