using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Accessors
{
    /// <summary>Root entry-point into PlugHub’s configuration system.</summary>
    public interface IConfigAccessor
    {
        public IConfigAccessor Init(IList<Type> configTypes, Token? ownerToken, Token? readToken, Token? writeToken);

        /// <summary>Returns a strongly-typed accessor for <typeparamref name="TConfig"/>.</summary>
        /// <typeparam name="TConfig">Configuration POCO (public, parameter-less ctor).</typeparam>
        /// <returns>IConfigAccessorFor&lt;TConfig&gt; scoped to the requested section.</returns>
        /// <exception cref="ConfigTypeNotFoundException">Type not registered.</exception>
        public IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class;
    }

    /// <summary>Type-safe accessor for a single configuration class.</summary>
    /// <typeparam name="TConfig">Configuration POCO.</typeparam>
    public interface IConfigAccessorFor<TConfig> where TConfig : class
    {
        /// <summary>
        /// <summary>Gets the default <paramref name="key"/> as <typeparamref name="T"/>.</summary>
        /// </summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="key">Public property name.</param>
        /// <returns>Current default value.</returns>
        /// <exception cref="KeyNotFoundException"/>
        /// <exception cref="InvalidCastException"/>
        public T Default<T>(string key);

        /// <summary>Gets the effective <paramref name="key"/> as <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Return type.</typeparam>
        /// <param name="key">Public property name.</param>
        /// <returns>Current merged value.</returns>
        /// <exception cref="KeyNotFoundException">Property not found.</exception>
        public T Get<T>(string key);

        /// <summary>Sets <paramref name="value"/> on <paramref name="key"/> in memory.</summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Public property name.</param>
        /// <param name="value">New value (encrypted transparently if secure).</param>
        public void Set<T>(string key, T value);

        /// <summary>Flushes pending edits to disk (blocking).</summary>
        public void Save();

        /// <summary>Flushes pending edits to disk asynchronously.</summary>
        public Task SaveAsync(CancellationToken cancellationToken = default);


        /// <summary>Returns the fully merged <typeparamref name="TConfig"/> object.</summary>
        public TConfig Get();

        /// <summary>Saves the supplied <paramref name="config"/> to disk (blocking).</summary>
        /// <param name="config">Updated configuration instance.</param>
        public void Save(TConfig config);

        /// <summary>Saves <paramref name="config"/> asynchronously.</summary>
        /// <param name="config">Updated configuration instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public Task SaveAsync(TConfig config, CancellationToken cancellationToken = default);
    }
}
