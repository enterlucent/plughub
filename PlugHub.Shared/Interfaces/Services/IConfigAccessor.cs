using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>
    /// Provides an entry point for accessing configuration sections by type.
    /// </summary>
    public interface IConfigAccessor
    {
        /// <summary>
        /// Gets a strongly-typed configuration accessor for the specified configuration type.
        /// </summary>
        /// <typeparam name="TConfig">The configuration section type.</typeparam>
        /// <returns>
        /// An <see cref="IConfigAccessorFor{TConfig}"/> for accessing configuration values of <typeparamref name="TConfig"/>.
        /// </returns>
        IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class;
    }

    /// <summary>
    /// Provides methods to get, set, and save configuration values for a specific configuration section.
    /// </summary>
    /// <typeparam name="TConfig">The configuration section type.</typeparam>
    public interface IConfigAccessorFor<TConfig> where TConfig : class
    {
        /// <summary>
        /// Gets a configuration value by key and converts it to the specified type.
        /// </summary>
        /// <typeparam name="T">The expected type of the configuration value.</typeparam>
        /// <param name="key">The key identifying the configuration value.</param>
        /// <returns>
        /// The configuration value converted to type <typeparamref name="T"/>.
        /// </returns>
        T Get<T>(string key);

        /// <summary>
        /// Sets a configuration value for the specified key.
        /// </summary>
        /// <typeparam name="T">The type of the value being set.</typeparam>
        /// <param name="key">The key identifying the configuration value.</param>
        /// <param name="value">The value to set.</param>
        void Set<T>(string key, T value);

        /// <summary>
        /// Persists any changes made to the configuration section.
        /// </summary>
        Task SaveAsync();


        /// <summary>
        /// Saves all property values from the provided <paramref name="config"/> instance to persistent storage.
        /// Each property is applied using the config service's merge logic (removing user values that match base, etc.).
        /// This method ensures that only changed or user-specific values are persisted, maintaining minimal user config footprint.
        /// </summary>
        /// <param name="config">The configuration instance to persist.</param>
        /// <returns>A task that completes when the save operation finishes.</returns>
        TConfig Get();

        /// <summary>
        /// Retrieves a fully populated instance of the configuration class <typeparamref name="TConfig"/>.
        /// This method merges base and user settings according to PlugHub config rules, ensuring the returned object
        /// reflects the effective configuration for the current accessor's context and permissions.
        /// </summary>
        /// <returns>The current configuration instance for <typeparamref name="TConfig"/>.</returns>
        Task SaveAsync(TConfig config);
    }
}
