using PlugHub.Shared.Interfaces.Services;

namespace PlugHub.Shared.Interfaces.Accessors
{
    /// <summary>
    /// Root entry-point into PlugHub’s configuration system.
    /// </summary>
    /// <remarks>
    /// Call <see cref="For{TConfig}"/> to obtain an accessor that is specialised for a single
    /// configuration POCO.  From that accessor you can read, mutate, and persist configuration
    /// values while PlugHub handles default/user merge-logic, RBAC, and—in the secure pipeline—
    /// automatic encryption.
    /// </remarks>
    public interface IConfigAccessor
    {
        /// <summary>
        /// Returns a strongly-typed accessor for configuration section
        /// <typeparamref name="TConfig"/>.
        /// </summary>
        /// <typeparam name="TConfig">
        /// Class that models a configuration section (must be public and have a parameterless constructor).
        /// </typeparam>
        /// <returns>
        /// An <see cref="IConfigAccessorFor{TConfig}"/> instance through which the caller can
        /// <see cref="IConfigAccessorFor{TConfig}.Get{T}(string)"/> / 
        /// <see cref="IConfigAccessorFor{TConfig}.Set{T}(string,T)"/> individual keys,
        /// or load / save an entire <typeparamref name="TConfig"/> object.
        /// </returns>
        /// <exception cref="ConfigTypeNotFoundException">
        /// Thrown when <typeparamref name="TConfig"/> has not been registered with the
        /// configuration service.
        /// </exception>
        IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class;
    }

    /// <summary>
    /// Provides fine-grained, type-safe access to a single configuration class of type <typeparamref name="TConfig"/>.
    /// Supports key-level reads/writes with compile-time type validation and whole-object operations
    /// including retrieval, merging, and persistence. Includes synchronous and asynchronous save methods.
    /// </summary>
    /// <typeparam name="TConfig">The POCO representing the configuration section</typeparam>
    public interface IConfigAccessorFor<TConfig> where TConfig : class
    {
        /// <summary>
        /// Fetches the current *effective* value for <paramref name="key"/> and converts it
        /// to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Desired return type.</typeparam>
        /// <param name="key">Public property name on <typeparamref name="TConfig"/>.</param>
        /// <returns>The merged (user ⮕ default) value converted to <typeparamref name="T"/>.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown when the property does not exist in <typeparamref name="TConfig"/>.
        /// </exception>
        T Get<T>(string key);

        /// <summary>
        /// Assigns <paramref name="value"/> to <paramref name="key"/> in memory.
        /// </summary>
        /// <remarks>
        /// The change is *not* flushed to disk until one of the <c>Save*</c> methods is
        /// invoked.  For secure properties the value is encrypted automatically before it
        /// leaves the accessor.
        /// </remarks>
        /// <typeparam name="T">Type of the value being stored.</typeparam>
        /// <param name="key">Public property name on <typeparamref name="TConfig"/>.</param>
        void Set<T>(string key, T value);

        /// <summary>
        /// Synchronously writes all pending per-key edits to the user-scope configuration
        /// file.  
        /// Only keys whose value differs from the default are written, so the file contains
        /// nothing but user overrides.
        /// </summary>
        /// <remarks>Blocks the calling thread for the duration of the file I/O.</remarks>
        void Save();

        /// <summary>
        /// Asynchronous counterpart of <see cref="Save"/>.  
        /// Use this in UI or high-throughput code paths to avoid blocking.
        /// </summary>
        Task SaveAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Builds and returns the fully merged <typeparamref name="TConfig"/> instance that
        /// the current caller would observe—i.e. <c>user&nbsp;overrides&nbsp;⇾&nbsp;default</c>.
        /// </summary>
        TConfig Get();

        /// <summary>
        /// Writes every property of <paramref name="config"/> to the user configuration
        /// file using PlugHub’s merge rules.
        /// </summary>
        /// <param name="config">Fully populated configuration object.</param>
        /// <remarks>
        /// <list type="bullet">
        ///   <item><description>
        ///     Properties whose value equals the default are **omitted** (existing overrides
        ///     are removed).
        ///   </description></item>
        ///   <item><description>
        ///     Properties flagged as secure are persisted as encrypted Base-64 strings.
        ///   </description></item>
        /// </list>
        /// This synchronous overload blocks until the write is complete; use
        /// <see cref="SaveAsync(TConfig)"/> to perform the same operation asynchronously.
        /// </remarks>
        void Save(TConfig config);

        /// <summary>
        /// Asynchronous variant of <see cref="Save(TConfig)"/> that performs the comparison,
        /// optional encryption, and disk write without blocking the calling thread.
        /// </summary>
        Task SaveAsync(TConfig config, CancellationToken cancellationToken = default);
    }
}
