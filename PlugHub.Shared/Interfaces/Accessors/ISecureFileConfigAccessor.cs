using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Accessors
{
    public interface ISecureFileConfigAccessor
        : IFileConfigAccessor
    {
        /// <summary>
        /// Sets the list of configuration types that this accessor will manage.
        /// </summary>
        /// <param name="configTypes">A list of <see cref="Type"/> objects representing the configurations to be handled.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new ISecureFileConfigAccessor SetConfigTypes(IList<Type> configTypes);

        /// <summary>
        /// Assigns the configuration service instance to this accessor.
        /// </summary>
        /// <param name="configService">An instance of <see cref="IConfigService"/> responsible for managing configuration lifecycles.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new ISecureFileConfigAccessor SetConfigService(IConfigService configService);

        /// <summary>
        /// Sets the security tokens for controlling ownership and permissions on configuration data.
        /// </summary>
        /// <param name="ownerToken">Token representing ownership privileges.</param>
        /// <param name="readToken">Token representing read permissions.</param>
        /// <param name="writeToken">Token representing write permissions.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new ISecureFileConfigAccessor SetAccess(Token ownerToken, Token readToken, Token writeToken);

        /// <summary>
        /// Sets a consolidated token set for ownership and access permissions.
        /// </summary>
        /// <param name="tokenSet">An <see cref="ITokenSet"/> containing owner, read, and write tokens.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public new ISecureFileConfigAccessor SetAccess(ITokenSet tokenSet);

        /// <summary>
        /// Specifies the encryption context to be used for securing the configuration data.
        /// </summary>
        /// <param name="encryptionContext">An <see cref="IEncryptionContext"/> that defines encryption parameters and metadata.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public ISecureFileConfigAccessor SetEncryptionContext(IEncryptionContext encryptionContext);

        /// <summary>
        /// Sets the encryption service responsible for encrypting and decrypting configuration data transparently.
        /// </summary>
        /// <param name="encryptionService">An instance of <see cref="IEncryptionService"/> implementing encryption logic.</param>
        /// <returns>The current instance for fluent chaining.</returns>
        public ISecureFileConfigAccessor SetEncryptionService(IEncryptionService encryptionService);

        /// <summary>
        /// Gets a strongly-typed secure accessor for the specified configuration type.
        /// </summary>
        /// <typeparam name="TConfig">The configuration type to access, constrained to class types.</typeparam>
        /// <returns>A secure accessor interface specialized for <typeparamref name="TConfig"/>.</returns>
        public new ISecureFileConfigAccessorFor<TConfig> For<TConfig>() where TConfig : class;
    }

    /// <summary>
    /// Represents a secure, strongly-typed file configuration accessor for a specific configuration type <typeparamref name="TConfig"/>.
    /// </summary>
    /// <typeparam name="TConfig">The configuration type this accessor manages. Must be a reference type.</typeparam>
    public interface ISecureFileConfigAccessorFor<TConfig>
        : IFileConfigAccessorFor<TConfig> where TConfig : class
    { }
}