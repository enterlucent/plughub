using System.Reflection;

namespace PlugHub.Shared.Models
{
    /// <summary>
    /// Represents a specific implementation of an interface provided by a plugin. Encapsulates the runtime assembly, the concrete class implementing the interface, and the interface type itself.
    /// </summary>
    /// <param name="Assembly">The assembly in which the implementation resides.</param>
    /// <param name="ImplementationType">The concrete type implementing the interface.</param>
    /// <param name="InterfaceType">The interface type being implemented by the plugin.</param>
    /// <remarks>
    /// Exposes properties to access descriptive metadata related to the assembly and types, supporting high-fidelity plugin discovery, enablement, and diagnostics.
    /// </remarks>
    public record PluginInterface(Assembly Assembly, Type ImplementationType, Type InterfaceType)
    {
        /// <summary>
        /// The file system path of the assembly containing the implementation.
        /// </summary>
        public string AssemblyLocation => this.Assembly.Location;

        /// <summary>
        /// The simple name of the assembly containing the implementation.
        /// </summary>
        public string AssemblyName => this.Assembly.GetName().Name ?? string.Empty;

        /// <summary>
        /// The fully qualified name of the plugin's concrete implementation type. Used for instantiation and reflection-based operations.
        /// </summary>
        public string ImplementationName => this.ImplementationType.FullName ?? this.ImplementationType.Name;

        /// <summary>
        /// The fully qualified name of the interface being implemented. Facilitates interface-based resolution and registration.
        /// </summary>
        public string InterfaceName => this.InterfaceType.FullName ?? this.InterfaceType.Name;
    }

    /// <summary>
    /// Represents a fully discovered plugin, including its source assembly, primary type, binding metadata, and the set of its distinct interfaces.
    /// </summary>
    /// <param name="Assembly">The assembly from which the plugin was loaded.</param>
    /// <param name="Type">The primary class or entry-point type implementing the plugin.</param>
    /// <param name="Metadata">Strongly-typed metadata describing plugin identity and attributes.</param>
    /// <param name="Interfaces">A collection of interface implementations offered by this plugin.</param>
    /// <remarks>
    /// Used throughout PlugHub to persist plugin state, resolve manifests, determine enablement, and to drive loading and registration workflows.
    /// </remarks>
    public record Plugin(Assembly Assembly, Type Type, PluginMetadata Metadata, IEnumerable<PluginInterface> Interfaces)
    {
        /// <summary>
        /// The file system path of the assembly containing the plugin's entry point.
        /// </summary>
        public string AssemblyLocation => this.Assembly.Location;

        /// <summary>
        /// The simple name of the assembly containing the plugin's entry point.
        /// </summary>
        public string AssemblyName => this.Assembly.GetName().Name ?? string.Empty;

        /// <summary>
        /// The fully qualified name of the plugin's primary class or entry type. Used for loading, discovery, and manifest recording.
        /// </summary>
        public string TypeName => this.Type.FullName ?? this.Type.Name;
    }
}