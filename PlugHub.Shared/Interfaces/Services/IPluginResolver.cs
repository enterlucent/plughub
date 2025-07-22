namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>
    /// Resolves plugin interface descriptors into a stable, dependency-valid, conflict-free load order.
    /// The resolver ensures that results are deterministic and safe for initialization, accounting for
    /// all declared dependencies, ordering constraints, and exclusions. Resolution is always performed
    /// at the interface level, supporting manifest-driven systems and pluggable load strategies.
    /// </summary>
    public interface IPluginResolver
    {
        /// <summary>
        /// Returns a dependency- and conflict-aware, stable ordering of interface descriptors.
        /// Sorting operates on individual interfaces, assembling an order that respects each descriptor’s
        /// declared dependencies, ordering constraints, and conflict exclusions. Unsatisfiable or conflicting
        /// descriptors are filtered out. The resulting list preserves the same order on every run for the
        /// same input, ensuring predictability and safe plugin initialization.
        /// </summary>
        /// <typeparam name="TDescriptor">
        /// The interface descriptor type being resolved and ordered; must inherit from <see cref="PluginDescriptor"/>.
        /// </typeparam>
        /// <param name="descriptors">
        /// The collection of interface descriptors to process and order.
        /// </param>
        /// <returns>
        /// A deterministic, dependency-valid, and conflict-free sequence of interface descriptors, sorted for correct load.
        /// </returns>
        IEnumerable<TDescriptor> ResolveDescriptors<TDescriptor>(IEnumerable<TDescriptor> descriptors)
            where TDescriptor : PluginDescriptor;
    }
}
