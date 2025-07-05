using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;
using System.Text.Json;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>
    /// Provides methods for managing encrypted application and type-defaultd configuration settings.
    /// </summary>
    public interface ISecureConfigService : IConfigService
    {
        /// <summary>
        /// Registers a configuration type with encryption support for <see cref="SecureValue"/> properties, ensuring sensitive values are encrypted before persistence.
        /// </summary>
        /// <param name="configType">The type representing the configuration.</param>
        /// <param name="encryptionContext">The encryption context used to encrypt <see cref="SecureValue"/> properties during registration.</param>
        /// <param name="accessorToken">Access token for registration operations. Defaults to <see cref="Token.Public"/>.</param>
        /// <param name="readToken">Access token for read operations. Defaults to <see cref="Token.Public"/>.</param>
        /// <param name="writeToken">Access token for modification operations. Defaults to <paramref name="readToken"/>.</param>
        /// <param name="jsonOptions">Specifies custom JSON serialization settings for this configuration type, allowing fine-grained control over property naming, converters, and formatting. Overrides global JSON options for this type.</param>
        /// <param name="reloadOnChange">If true, automatically reloads configuration when source changes.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void RegisterConfig(Type configType, IEncryptionContext encryptionContext, Token? accessorToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);

        /// <summary>
        /// Registers a configuration type with encryption support for <see cref="SecureValue"/> properties, ensuring sensitive values are encrypted before persistence.
        /// </summary>
        /// <param name="configType">The type representing the configuration.</param>
        /// <param name="encryptionContext">The encryption context used to encrypt <see cref="SecureValue"/> properties during registration.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <param name="jsonOptions">Specifies custom JSON serialization settings for this configuration type, allowing fine-grained control over property naming, converters, and formatting. Overrides global JSON options for this type.</param>
        /// <param name="reloadOnChange">If true, automatically reloads configuration when source changes.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        void RegisterConfig(Type configType, IEncryptionContext encryptionContext, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);


        /// <summary>
        /// Registers multiple configuration types with encryption support for <see cref="SecureValue"/> properties, ensuring sensitive values are encrypted before persistence.
        /// </summary>
        /// <param name="configTypes">The types representing configurations.</param>
        /// <param name="encryptionContext">The encryption context used to encrypt <see cref="SecureValue"/> properties during registration.</param>
        /// <param name="accessorToken">Access token for registration operations. Defaults to <see cref="Token.Public"/>.</param>
        /// <param name="readToken">Access token for read operations. Defaults to <see cref="Token.Public"/>.</param>
        /// <param name="writeToken">Access token for modification operations. Defaults to <paramref name="readToken"/>.</param>
        /// <param name="jsonOptions">Specifies custom JSON serialization settings for these configuration types, allowing per-type control over serialization behavior. Applied to all types in the collection.</param>
        /// <param name="reloadOnChange">If true, automatically reloads configurations when sources change.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void RegisterConfigs(IEnumerable<Type> configTypes, IEncryptionContext encryptionContext, Token? accessorToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);

        /// <summary>
        /// Registers multiple configuration types with encryption support for <see cref="SecureValue"/> properties, ensuring sensitive values are encrypted before persistence.
        /// </summary>
        /// <param name="configTypes">The types representing configurations.</param>
        /// <param name="encryptionContext">The encryption context used to encrypt <see cref="SecureValue"/> properties during registration.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <param name="jsonOptions">Specifies custom JSON serialization settings for these configuration types, allowing per-type control over serialization behavior. Applied to all types in the collection.</param>
        /// <param name="reloadOnChange">If true, automatically reloads configurations when sources change.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        void RegisterConfigs(IEnumerable<Type> configTypes, IEncryptionContext encryptionContext, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);
    }
}