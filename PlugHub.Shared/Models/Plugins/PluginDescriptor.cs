namespace PlugHub.Shared.Models.Plugins
{
    /// <summary>
    /// Base descriptor that defines an extension point within PlugHub.
    /// Encapsulates the identity, version, and declared relationships (dependencies, load order, conflicts) that govern how extensions are discovered, sorted, and resolved at runtime.
    /// </summary>
    public abstract record PluginDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null);
}