using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Accessors
{
    public interface IFileConfigAccessor
        : IConfigAccessor
    {
        /// <summary>
        /// Sets the list of configuration types that this accessor will manage.
        /// </summary>
        /// <param name="configTypes">A list of <see cref="Type"/> objects representing the configurations to be handled.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new IFileConfigAccessor SetConfigTypes(IList<Type> configTypes);

        /// <summary>
        /// Assigns the configuration service instance to this accessor.
        /// </summary>
        /// <param name="configService">An instance of <see cref="IConfigService"/> responsible for managing configuration lifecycles.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new IFileConfigAccessor SetConfigService(IConfigService configService);

        /// <summary>
        /// Sets the security tokens for controlling ownership and permissions on configuration data.
        /// </summary>
        /// <param name="ownerToken">Token representing ownership privileges.</param>
        /// <param name="readToken">Token representing read permissions.</param>
        /// <param name="writeToken">Token representing write permissions.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new IFileConfigAccessor SetAccess(Token ownerToken, Token readToken, Token writeToken);

        /// <summary>
        /// Sets a consolidated token set for ownership and access permissions.
        /// </summary>
        /// <param name="tokenSet">An <see cref="ITokenSet"/> containing owner, read, and write tokens.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new IFileConfigAccessor SetAccess(ITokenSet tokenSet);

        /// <summary>Returns a strongly-typed accessor for <typeparamref name="TConfig"/>.</summary>
        /// <typeparam name="TConfig">Configuration POCO (public, parameter-less ctor).</typeparam>
        /// <returns>IConfigAccessorFor&lt;TConfig&gt; scoped to the requested section.</returns>
        /// <exception cref="KeyNotFoundException"/>
        public new IFileConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class;
    }

    /// <summary>Type-safe accessor for a single configuration class.</summary>
    /// <typeparam name="TConfig">Configuration POCO.</typeparam>
    public interface IFileConfigAccessorFor<TConfig>
        : IConfigAccessorFor<TConfig> where TConfig : class
    { }
}