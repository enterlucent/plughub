namespace PlugHub.Shared.Models.Plugins
{
    /// <summary>
    /// Base descriptor that defines the identity and load characteristics for a plugin or plugin interface.
    /// Encapsulates core metadata, including unique plugin ID, version, and all declared dependency and load order relationships.
    /// Used as a stable contract for all forms of interface-level sorting, resolution, and conflict management within PlugHub.
    /// </summary>
    public abstract record PluginDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null);
}