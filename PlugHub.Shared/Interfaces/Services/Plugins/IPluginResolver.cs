using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Interfaces.Services.Plugins
{
    /// <summary>
    /// Resolves plugin descriptors into a stable, dependency-valid, conflict-free load order.
    /// The resolver ensures that results are deterministic and safe for initialization, accounting for
    /// all declared dependencies, ordering constraints, and exclusions. Resolution is always performed
    /// at the interface level, supporting manifest-driven systems and pluggable load strategies.
    /// </summary>
    public interface IPluginResolver
    {
        /// <summary>
        /// Resolves descriptors for the given plugin interface type by:<br/>
        /// 1. Using the interface’s <see cref="DescriptorProviderAttribute"/> to locate the descriptor accessor.<br/>
        /// 2. Invoking the accessor on each provided plugin instance to collect descriptors.<br/>
        /// 3. Performing dependency resolution and topological sort.<br/>
        /// 4. Applying the interface’s declared <see cref="DescriptorSortContext"/>.
        /// </summary>
        /// <typeparam name="TInterface">The plugin interface type annotated with <see cref="DescriptorProviderAttribute"/>.</typeparam>
        /// <typeparam name="TDescriptor">The descriptor type returned by the accessor; must inherit from <see cref="PluginDescriptor"/>.</typeparam>
        /// <param name="plugins">The plugin instances implementing <typeparamref name="TInterface"/>.</param>
        /// <returns>A deterministic, dependency-valid, conflict-free, and correctly ordered sequence of descriptors.</returns>
        IReadOnlyList<TDescriptor> ResolveAndOrder<TInterface, TDescriptor>(IEnumerable<TInterface> plugins) where TInterface : class where TDescriptor : PluginDescriptor;

        /// <summary>
        /// Builds a full resolution context for the given set of plugin descriptors,
        /// preserving both valid order information and any rejection reasons.  
        /// </summary>
        /// <typeparam name="TDescriptor">The interface descriptor type being resolved; must inherit from <see cref="PluginDescriptor"/>.</typeparam>
        /// <param name="descriptors">The collection of interface descriptors to evaluate for ordering and conflicts.</param>
        /// <returns>A <see cref="PluginResolutionContext{TDescriptor}"/> containing deterministic ordering results along with detailed state about rejected or disabled descriptors.</returns>
        PluginResolutionContext<TDescriptor> ResolveContext<TDescriptor>(IEnumerable<TDescriptor> descriptors) where TDescriptor : PluginDescriptor;

        /// <summary>
        /// Returns a dependency- and conflict-aware, stable ordering of interface descriptors.
        /// Sorting operates on individual interfaces, assembling an order that respects each descriptor’s
        /// declared dependencies, ordering constraints, and conflict exclusions. Unsatisfiable or conflicting
        /// descriptors are filtered out. The resulting list preserves the same order on every run for the
        /// same input, ensuring predictability and safe plugin initialization.
        /// </summary>
        /// <typeparam name="TDescriptor">The interface descriptor type being resolved and ordered; must inherit from <see cref="PluginDescriptor"/>.</typeparam>
        /// <param name="descriptors">The collection of interface descriptors to process and order.</param>
        /// <returns>A deterministic, dependency-valid, and conflict-free sequence of interface descriptors, sorted for correct load.</returns>
        IEnumerable<TDescriptor> ResolveDescriptors<TDescriptor>(IEnumerable<TDescriptor> descriptors) where TDescriptor : PluginDescriptor;
    }
}