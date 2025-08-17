using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Accessors
{
    public interface IConfigAccessor
    {
        /// The interface under which you registered yourself:
        /// typeof(IConfigAccessor), typeof(ISecureConfigAccessor), etc.
        public Type AccessorInterface { get; init; }


        /// <summary>Sets the configuration types this accessor will handle.</summary>
        /// <param name="configTypes">List of configuration types to register.</param>
        /// <returns>The updated accessor instance.</returns>
        public IConfigAccessor SetConfigTypes(IList<Type> configTypes);

        /// <summary>Assigns the underlying configuration service used for data operations.</summary>
        /// <param name="configService">The configuration service instance.</param>
        /// <returns>The updated accessor instance.</returns>
        public IConfigAccessor SetConfigService(IConfigService configService);

        /// <summary>Sets the access tokens controlling owner, read, and write permissions.</summary>
        /// <param name="ownerToken">Owner token with full permissions.</param>
        /// <param name="readToken">Token for read access.</param>
        /// <param name="writeToken">Token for write access.</param>
        /// <returns>The updated accessor instance.</returns>
        public IConfigAccessor SetAccess(Token ownerToken, Token readToken, Token writeToken);

        /// <summary>Sets the access tokens using a token set containing owner, read, and write tokens.</summary>
        /// <param name="tokenSet">Token set encapsulating access tokens.</param>
        /// <returns>The updated accessor instance.</returns>
        public IConfigAccessor SetAccess(ITokenSet tokenSet);


        /// <summary>Gets a strongly typed configuration accessor for the specified configuration type.</summary>
        /// <typeparam name="TConfig">Type of the configuration.</typeparam>
        /// <returns>A strongly typed accessor for <typeparamref name="TConfig"/>.</returns>
        public IConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class;

        /// <summary>Creates a strongly typed configuration accessor for the specified type and tokens.</summary>
        /// <typeparam name="TConfig">Type of the configuration.</typeparam>
        /// <param name="tokenService">Token service for access validation and token set creation.</param>
        /// <param name="configService">Configuration service to associate.</param>
        /// <param name="ownerToken">Owner access token.</param>
        /// <param name="readToken">Read access token.</param>
        /// <param name="writeToken">Write access token.</param>
        /// <returns>A strongly typed accessor for <typeparamref name="TConfig"/>.</returns>
        public IConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, Token? ownerToken, Token? readToken, Token? writeToken) where TConfig : class;

        /// <summary>
        /// Creates a strongly typed configuration accessor for the specified configuration type using an <see cref="ITokenSet"/> for access control.
        /// </summary>
        /// <typeparam name="TConfig">The type of the configuration object.</typeparam>
        /// <param name="tokenService">Token service for access validation and token set creation.</param>
        /// <param name="configService">The configuration service instance to associate with the accessor.</param>
        /// <param name="tokenSet">The token set containing owner, read, and write tokens for access control.</param>
        /// <returns>A strongly typed configuration accessor for <typeparamref name="TConfig"/>.</returns>
        public IConfigAccessorFor<TConfig> CreateFor<TConfig>(ITokenService tokenService, IConfigService configService, ITokenSet tokenSet) where TConfig : class;
    }

    public interface IConfigAccessorFor<TConfig> where TConfig : class
    {
        /// <summary>
        /// Sets the access tokens for this config accessor.
        /// </summary>
        /// <param name="ownerToken">Owner token for full access.</param>
        /// <param name="readToken">Read token for read access.</param>
        /// <param name="writeToken">Write token for write access.</param>
        /// <returns>The updated config accessor instance.</returns>
        public IConfigAccessorFor<TConfig> SetAccess(Token ownerToken, Token readToken, Token writeToken);

        /// <summary>
        /// Sets the access tokens for this config accessor using a token set.
        /// </summary>
        /// <param name="tokenSet">Token set containing owner, read, and write tokens.</param>
        /// <returns>The updated config accessor instance.</returns>
        public IConfigAccessorFor<TConfig> SetAccess(ITokenSet tokenSet);


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
        /// <exception cref="KeyNotFoundException"/>
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