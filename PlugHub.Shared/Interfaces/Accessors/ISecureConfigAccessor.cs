using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Accessors
{
    /// <summary>Entry-point for encryption-aware configuration access.</summary>
    public interface ISecureConfigAccessor
    {
        public ISecureConfigAccessor Init(IList<Type> configTypes, IEncryptionContext encryptionContext, Token? ownerToken, Token? readToken, Token? writeToken);

        /// <summary>Returns a strongly-typed secure accessor for <typeparamref name="TConfig"/>.</summary>
        /// <typeparam name="TConfig">Configuration POCO.</typeparam>
        /// <returns>Secure accessor scoped to <typeparamref name="TConfig"/>.</returns>
        public ISecureConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class;
    }

    /// <summary>Strongly-typed accessor that transparently encrypts / decrypts secure fields.</summary>
    /// <typeparam name="TConfig">Configuration POCO managed by this accessor.</typeparam>
    public interface ISecureConfigAccessorFor<TConfig> : IConfigAccessorFor<TConfig> where TConfig : class
    {
        /// <summary>Gets the value of <paramref name="key"/> converted to <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">Expected return type.</typeparam>
        /// <param name="key">Property name within <typeparamref name="TConfig"/>.</param>
        /// <returns>Effective value for <paramref name="key"/>.</returns>
        /// <exception cref="KeyNotFoundException">Key not found on <typeparamref name="TConfig"/>.</exception>
        public new T Get<T>(string key);

        /// <summary>Assigns <paramref name="value"/> to property <paramref name="key"/>.</summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="key">Property name within <typeparamref name="TConfig"/>.</param>
        /// <param name="value">New value to store (will be encrypted if the field is secure).</param>
        public new void Set<T>(string key, T value);


        /// <summary>Returns the merged configuration object.</summary>
        /// <returns>Fully-hydrated <typeparamref name="TConfig"/> instance.</returns>
        public new TConfig Get();

        /// <summary>Saves <paramref name="updated"/> to disk.</summary>
        /// <param name="updated">Updated configuration instance.</param>
        public new void Save(TConfig updated);

        /// <summary>Asynchronously saves <paramref name="updated"/>.</summary>
        /// <param name="updated">Updated configuration instance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task that completes when the save is finished.</returns>
        public new Task SaveAsync(TConfig updated, CancellationToken cancellationToken = default);
    }
}
