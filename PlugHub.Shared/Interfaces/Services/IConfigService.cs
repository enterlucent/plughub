using Microsoft.Extensions.Configuration;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;
using System.Text.Json;

namespace PlugHub.Shared.Interfaces.Services
{
    /// <summary>
    /// Exception thrown when a requested type config is not registered in the config service.
    /// </summary>
    /// <remarks>
    /// Inherits from <see cref="KeyNotFoundException"/> to indicate that the requested type could not be found
    /// in the underlying collection of config.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ConfigTypeNotFoundException"/> class
    /// with a message indicating the missing type.
    /// </remarks>
    /// <param name="type">The <see cref="Type"/> for which the config was not registered.</param>
    public class ConfigTypeNotFoundException(string? message) : KeyNotFoundException(message) { }


    /// <summary>
    /// Enumerates the types of configuration operations that can trigger a fire-and-forget save error.
    /// </summary>
    public enum ConfigSaveOperation
    {
        /// <summary>
        /// The operation is unknown or unspecified.
        /// </summary>
        Unknown,

        /// <summary>
        /// Represents a save operation for configuration settings.
        /// </summary>
        SaveSettings,

        /// <summary>
        /// Represents a save operation for an entire configuration instance.
        /// </summary>
        SaveConfigInstance,

        /// <summary>
        /// Represents a save operation for default configuration file contents.
        /// </summary>
        SaveDefaultConfigFileContents
    }

    /// <summary>
    /// Provides data for the <c>ConfigService.SyncSaveOpErrors</c> event,
    /// containing details about a fire-and-forget save error in the configuration service.
    /// </summary>
    /// <param name="ex">The exception that was thrown during the operation.</param>
    /// <param name="operation">The type of configuration operation that failed.</param>
    /// <param name="configType">The configuration type involved in the operation, if applicable.</param>
    public class ConfigServiceSaveErrorEventArgs(Exception ex, ConfigSaveOperation operation = ConfigSaveOperation.Unknown, Type? configType = null) : EventArgs
    {
        /// <summary>
        /// Gets the exception that was thrown during the configuration save operation.
        /// </summary>
        public Exception Exception = ex;

        /// <summary>
        /// Gets the type of configuration operation that failed.
        /// </summary>
        public ConfigSaveOperation Operation = operation;

        /// <summary>
        /// Gets the configuration type involved in the operation, if applicable.
        /// </summary>
        public Type? ConfigType = configType;
    }


    /// <summary>
    /// Event arguments for when a configuration file is reloaded in the <see cref="ConfigService"/>.
    /// Provides the type of the configuration that was reloaded.
    /// </summary>
    /// <param name="configType">The configuration type that was reloaded.</param>
    public class ConfigServiceConfigReloadedEventArgs(Type configType) : EventArgs
    {
        /// <summary>
        /// Gets the configuration type that was reloaded.
        /// </summary>
        public Type ConfigType { get; } = configType;
    }

    /// <summary>
    /// Event arguments for when a default or user setting changes in the <see cref="ConfigService"/>.
    /// Provides the configuration type, key, and both the old and new values of the setting.
    /// </summary>
    /// <param name="configType">The configuration type where the setting changed.</param>
    /// <param name="key">The key of the setting that changed.</param>
    /// <param name="oldValue">The previous value of the setting.</param>
    /// <param name="newValue">The new value of the setting.</param>
    public class ConfigServiceSettingChangeEventArgs(Type configType, string key, object? oldValue, object? newValue) : EventArgs
    {
        /// <summary>
        /// Gets the configuration type where the setting changed.
        /// </summary>
        public Type ConfigType { get; } = configType;

        /// <summary>
        /// Gets the key of the setting that changed.
        /// </summary>
        public string Key { get; } = key;

        /// <summary>
        /// Gets the previous value of the setting.
        /// </summary>
        public object? OldValue { get; } = oldValue;

        /// <summary>
        /// Gets the new value of the setting.
        /// </summary>
        public object? NewValue { get; } = newValue;
    }


    /// <summary>
    /// Provides methods for managing application and type-defaultd configuration settings.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Occurs when a fire-and-forget save operation in <see cref="ConfigService"/> fails.
        /// </summary>
        public event EventHandler<ConfigServiceSaveErrorEventArgs>? SyncSaveOpErrors;

        /// <summary>
        /// Occurs when a configuration file is reloaded from disk (for example, due to an external file change).
        /// </summary>
        public event EventHandler<ConfigServiceConfigReloadedEventArgs>? ConfigReloaded;

        /// <summary>
        /// Occurs when a type-specific configuration value is changed via <see cref="SetSetting{T}(Type, string, T, bool)"/>.
        /// </summary>
        public event EventHandler<ConfigServiceSettingChangeEventArgs>? SettingChanged;


        /// <summary>
        /// Creates an <see cref="IConfigAccessor"/> for a single configuration type,
        /// using dedicated tokens for access control operations.
        /// </summary>
        /// <param name="configType">The configuration type to access.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// <returns>
        /// An <see cref="IConfigAccessor"/> scoped to the specified type and tokens.
        /// </returns>
        public IConfigAccessor CreateAccessor(Type configType, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null);

        /// <summary>
        /// Creates an <see cref="IConfigAccessor"/> for a single configuration type,
        /// using dedicated tokens for access control operations.
        /// </summary>
        /// <param name="configType">The configuration type to access.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>
        /// An <see cref="IConfigAccessor"/> scoped to the specified type and tokens.
        /// </returns>
        public IConfigAccessor CreateAccessor(Type configType, ITokenSet tokenSet);


        /// <summary>
        /// Creates an <see cref="IConfigAccessor"/> for multiple configuration types,
        /// using the specified tokens for access control.
        /// </summary>
        /// <param name="configTypes">The configuration types to access.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// <returns>
        /// An <see cref="IConfigAccessor"/> scoped to the specified types and tokens.
        /// </returns>
        public IConfigAccessor CreateAccessor(IList<Type> configTypes, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null);

        /// <summary>
        /// Creates an <see cref="IConfigAccessor"/> for multiple configuration types,
        /// using the specified tokens for access control.
        /// </summary>
        /// <param name="configTypes">The configuration types to access.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>
        /// An <see cref="IConfigAccessor"/> scoped to the specified types and tokens.
        /// </returns>
        public IConfigAccessor CreateAccessor(IList<Type> configTypes, ITokenSet tokenSet);


        /// <summary>
        /// Registers a configuration type with granular token-based access control and type-specific JSON serialization settings.
        /// </summary>
        /// <param name="configType">The type representing the configuration.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// <param name="jsonOptions">Specifies custom JSON serialization settings for this configuration type, allowing fine-grained control over property naming, converters, and formatting. Overrides global JSON options for this type.</param>
        /// <param name="reloadOnChange">If true, automatically reloads configuration when source changes.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void RegisterConfig(Type configType, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);

        /// <summary>
        /// Registers a configuration type using a token set for consolidated access control,
        /// with type-specific JSON serialization settings and change reloading.
        /// </summary>
        /// <param name="configType">The type representing the configuration structure.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <param name="jsonOptions">Custom JSON serialization settings for this configuration type, overriding global options for property naming, converters, and formatting.</param>
        /// <param name="reloadOnChange">Enables automatic reloading when underlying configuration files change.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when invalid tokens are provided</exception>
        /// <remarks>
        /// Provides a consolidated interface for token management while maintaining identical security 
        /// and serialization behavior as the granular token overload.
        /// </remarks>
        public void RegisterConfig(Type configType, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);


        /// <summary>
        /// Registers multiple configuration types with shared token policies and type-specific JSON serialization settings.
        /// </summary>
        /// <param name="configTypes">The types representing configurations.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// <param name="jsonOptions">Specifies custom JSON serialization settings for these configuration types, allowing per-type control over serialization behavior. Applied to all types in the collection.</param>
        /// <param name="reloadOnChange">If true, automatically reloads configurations when sources change.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void RegisterConfigs(IEnumerable<Type> configTypes, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);

        /// <summary>
        /// Registers multiple configuration types using a consolidated token set for access control, with optional JSON serialization settings.
        /// </summary>
        /// <param name="configTypes">The configuration types to register.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <param name="jsonOptions">Custom JSON serialization settings applied to all specified configuration types.</param>
        /// <param name="reloadOnChange">Enables automatic reload when underlying configuration files change.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown if current context lacks permission to register configurations.</exception>
        public void RegisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);


        /// <summary>
        /// Unregisters a single configuration type from the service, removing all associated settings and change listeners.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> of the configuration to unregister.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown if the provided token does not have write access to the configuration.</exception>
        /// <remarks>
        /// This method is thread-safe. If the configuration type is not registered, the operation is a no-op.
        /// Any registered reload callbacks for the configuration will be disposed.
        /// </remarks>
        public void UnregisterConfig(Type configType, Token? ownerToken = null);

        /// <summary>
        /// Unregisters a single configuration type from the service, removing all associated settings and change listeners.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> of the configuration to unregister.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown if the provided token does not have write access to the configuration.</exception>
        /// <remarks>
        /// This method is thread-safe. If the configuration type is not registered, the operation is a no-op.
        /// Any registered reload callbacks for the configuration will be disposed.
        /// </remarks>
        public void UnregisterConfig(Type configType, ITokenSet tokenSet);


        /// <summary>
        /// Unregisters multiple configuration types from the service, removing all associated settings and change listeners for each.
        /// </summary>
        /// <param name="configTypes">A collection of <see cref="Type"/> objects representing the configurations to unregister.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configTypes"/> is null.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if the provided token does not have write access to any configuration in the collection.</exception>
        /// <remarks>
        /// This method is thread-safe. If a configuration type in the collection is not registered, it is skipped.
        /// Any registered reload callbacks for each configuration will be disposed.
        /// </remarks>
        public void UnregisterConfigs(IEnumerable<Type> configTypes, Token? ownerToken = null);

        /// <summary>
        /// Unregisters multiple configuration types from the service, removing all associated settings and change listeners for each.
        /// </summary>
        /// <param name="configTypes">A collection of <see cref="Type"/> objects representing the configurations to unregister.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configTypes"/> is null.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if the provided token does not have write access to any configuration in the collection.</exception>
        /// <remarks>
        /// This method is thread-safe. If a configuration type in the collection is not registered, it is skipped.
        /// Any registered reload callbacks for each configuration will be disposed.
        /// </remarks>
        public void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet);


        /// <summary>
        /// Gets the raw contents of the default configuration file for the specified configuration type.
        /// </summary>
        /// <param name="configType">The type of the configuration section.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <returns>The contents of the default configuration file as a string.</returns>
        public string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null);

        /// <summary>
        /// Gets the raw contents of the default configuration file for the specified configuration type.
        /// </summary>
        /// <param name="configType">The type of the configuration section.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>The contents of the default configuration file as a string.</returns>
        public string GetDefaultConfigFileContents(Type configType, ITokenSet tokenSet);


        /// <summary>
        /// Asynchronously saves the provided contents to the default configuration file for the specified configuration type.
        /// </summary>
        /// <param name="configType">The type of the configuration section.</param>
        /// <param name="contents">The raw string contents to write to the default configuration file.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asynchronously saves the provided contents to the default configuration file for the specified configuration type.
        /// </summary>
        /// <param name="configType">The type of the configuration section.</param>
        /// <param name="contents">The raw string contents to write to the default configuration file.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, ITokenSet tokenSet, CancellationToken cancellationToken = default);


        /// <summary>
        /// Overwrites the default configuration file for the specified type with the provided JSON contents asynchronously.
        /// This method initiates the write operation and returns immediately; errors are reported via the <see cref="SyncSaveOpErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose default config file should be overwritten.</param>
        /// <param name="contents">The raw JSON contents to write to the default config file.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// Optional access token for write authorization. If not specified, defaults to <see cref="Token.Public"/>.
        /// </param>
        /// <remarks>
        /// - This is a fire-and-forget operation; exceptions are not thrown synchronously.
        /// - Errors that occur during the write are surfaced via the <see cref="SyncSaveOpErrors"/> event handler.
        /// - Use <see cref="SaveDefaultConfigFileContentsAsync"/> for awaitable, exception-aware saving.
        /// </remarks>
        public void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null);

        /// <summary>
        /// Overwrites the default configuration file for the specified type with the provided JSON contents asynchronously.
        /// This method initiates the write operation and returns immediately; errors are reported via the <see cref="SyncSaveOpErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose default config file should be overwritten.</param>
        /// <param name="contents">The raw JSON contents to write to the default config file.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// Optional access token for write authorization. If not specified, defaults to <see cref="Token.Public"/>.
        /// </param>
        /// <remarks>
        /// - This is a fire-and-forget operation; exceptions are not thrown synchronously.
        /// - Errors that occur during the write are surfaced via the <see cref="SyncSaveOpErrors"/> event handler.
        /// - Use <see cref="SaveDefaultConfigFileContentsAsync"/> for awaitable, exception-aware saving.
        /// </remarks>
        public void SaveDefaultConfigFileContents(Type configType, string contents, ITokenSet tokenSet);


        /// <summary>
        /// Retrieves a fully populated configuration instance of the specified type.
        /// This method merges default and user settings according to PlugHub config rules,
        /// returning an object that reflects the effective configuration state.
        /// </summary>
        /// <param name="configType">The Type of configuration class to retrieve</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <returns>A populated instance of the requested configuration type</returns>
        /// <exception cref="KeyNotFoundException">Thrown if configType is not registered</exception>
        /// <exception cref="InvalidOperationException">Thrown if instance creation fails</exception>
        public object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null);

        /// <summary>
        /// Retrieves a fully populated configuration instance of the specified type.
        /// This method merges default and user settings according to PlugHub config rules,
        /// returning an object that reflects the effective configuration state.
        /// </summary>
        /// <param name="configType">The Type of configuration class to retrieve</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>A populated instance of the requested configuration type</returns>
        /// <exception cref="KeyNotFoundException">Thrown if configType is not registered</exception>
        /// <exception cref="InvalidOperationException">Thrown if instance creation fails</exception>
        public object GetConfigInstance(Type configType, ITokenSet tokenSet);


        /// <summary>
        /// Persists property values from a configuration instance to storage.
        /// Applies each property using the config service's merge logic, ensuring user values
        /// are only stored when they differ from default values (minimal user config footprint).
        /// </summary>
        /// <param name="configType">The Type of configuration being updated</param>
        /// <param name="updatedConfig">Instance containing new property values</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// <exception cref="ArgumentNullException">Thrown if updatedConfig is null</exception>
        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists property values from a configuration instance to storage.
        /// Applies each property using the config service's merge logic, ensuring user values
        /// are only stored when they differ from default values (minimal user config footprint).
        /// </summary>
        /// <param name="configType">The Type of configuration being updated</param>
        /// <param name="updatedConfig">Instance containing new property values</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <exception cref="ArgumentNullException">Thrown if updatedConfig is null</exception>
        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default);


        /// <summary>
        /// Saves the provided configuration object instance to the user settings file for the specified type asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveOpErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be updated.</param>
        /// <param name="updatedConfig">The updated configuration object to persist.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// Optional access token for write authorization. If not specified, defaults to <see cref="Token.Public"/>.
        /// </param>
        /// <remarks>
        /// - This is a fire-and-forget operation; exceptions are not thrown synchronously.
        /// - Errors that occur during the save are surfaced via the <see cref="SyncSaveOpErrors"/> event handler.
        /// - Use <see cref="SaveConfigInstanceAsync"/> for awaitable, exception-aware saving.
        /// </remarks>
        public void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null);

        /// <summary>
        /// Saves the provided configuration object instance to the user settings file for the specified type asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveOpErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be updated.</param>
        /// <param name="updatedConfig">The updated configuration object to persist.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// Optional access token for write authorization. If not specified, defaults to <see cref="Token.Public"/>.
        /// </param>
        /// <remarks>
        /// - This is a fire-and-forget operation; exceptions are not thrown synchronously.
        /// - Errors that occur during the save are surfaced via the <see cref="SyncSaveOpErrors"/> event handler.
        /// - Use <see cref="SaveConfigInstanceAsync"/> for awaitable, exception-aware saving.
        /// </remarks>
        public void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet);


        /// <summary>
        /// Gets a read-only <see cref="IConfiguration"/> representing the current environment variables and command-line arguments.
        /// </summary>
        /// <returns>An <see cref="IConfiguration"/> instance containing environment and command-line settings. This object is read-only.</returns>
        public IConfiguration GetEnvConfig();


        /// <summary>
        /// Returns the *baseline* value that was loaded from
        /// <c>&lt;{configType.Name}&gt;.DefaultSettings.json</c> for the property named
        /// <paramref name="key"/> – ignoring any user-override that might currently exist.
        /// </summary>
        /// <typeparam name="T">Desired return type.  If the stored object implements <see cref="IConvertible"/> it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section whose default you want to inspect. Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <returns> The default value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException">Thrown when <paramref name="configType"/> has not been registered.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the supplied <paramref name="key"/> does not exist in the defaults file.</exception>
        /// <remarks>
        /// For properties marked *secure*, the return value is a <c>SecureValue</c> instance
        /// containing the encrypted Base-64 payload.
        /// The method is crypto-agnostic; no decryption occurs inside <c>ConfigService</c>.
        /// </remarks>
        T? GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);

        /// <summary>
        /// Returns the *baseline* value that was loaded from
        /// <c>&lt;{configType.Name}&gt;.DefaultSettings.json</c> for the property named
        /// <paramref name="key"/> – ignoring any user-override that might currently exist.
        /// </summary>
        /// <typeparam name="T">Desired return type.  If the stored object implements <see cref="IConvertible"/> it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section whose default you want to inspect. Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns> The default value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException">Thrown when <paramref name="configType"/> has not been registered.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the supplied <paramref name="key"/> does not exist in the defaults file.</exception>
        /// <remarks>
        /// For properties marked *secure*, the return value is a <c>SecureValue</c> instance
        /// containing the encrypted Base-64 payload.
        /// The method is crypto-agnostic; no decryption occurs inside <c>ConfigService</c>.
        /// </remarks>
        T? GetDefault<T>(Type configType, string key, ITokenSet tokenSet);


        /// <summary>
        /// Returns the *effective* value for the property named <paramref name="key"/>,
        /// i.e.&nbsp;<c>UserValue ?? DefaultValue</c>, using the merge order defined by PlugHub.
        /// </summary>
        /// <typeparam name="T">Desired return type. If the stored object implements <see cref="IConvertible"/>, it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section you want to query.Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <returns>The effective value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException">Thrown when <paramref name="configType"/> has not been registered.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the supplied <paramref name="key"/> does not exist in the configuration.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the caller, as identified by <paramref name="readToken"/>, does not have read access to the requested setting.</exception>
        /// <remarks>
        /// For properties marked *secure*, the returned value is a <c>SecureValue</c> object
        /// containing the encrypted Base-64 payload unless the caller explicitly requests a
        /// decrypted type via a secure accessor.
        /// </remarks>
        public T? GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);

        /// <summary>
        /// Returns the *effective* value for the property named <paramref name="key"/>,
        /// i.e.&nbsp;<c>UserValue ?? DefaultValue</c>, using the merge order defined by PlugHub.
        /// </summary>
        /// <typeparam name="T">Desired return type. If the stored object implements <see cref="IConvertible"/>, it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section you want to query.Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>The effective value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException">Thrown when <paramref name="configType"/> has not been registered.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the supplied <paramref name="key"/> does not exist in the configuration.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the caller, as identified by <paramref name="readToken"/>, does not have read access to the requested setting.</exception>
        /// <remarks>
        /// For properties marked *secure*, the returned value is a <c>SecureValue</c> object
        /// containing the encrypted Base-64 payload unless the caller explicitly requests a
        /// decrypted type via a secure accessor.
        /// </remarks>
        public T? GetSetting<T>(Type configType, string key, ITokenSet tokenSet);


        /// <summary>
        /// Sets a type-specific setting by type and key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="configType">The configuration type.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        public void SetSetting<T>(Type configType, string key, T? value, Token? ownerToken = null, Token? writeToken = null);

        /// <summary>
        /// Sets a type-specific setting by type and key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="configType">The configuration type.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        public void SetSetting<T>(Type configType, string key, T? value, ITokenSet tokenSet);


        /// <summary>
        /// Saves the current user and default settings for the specified configuration type to disk asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveOpErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be saved.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        /// Optional access token for write authorization. If not specified, defaults to <see cref="Token.Public"/>.
        /// </param>
        /// <remarks>
        /// - This is a fire-and-forget operation; exceptions are not thrown synchronously.
        /// - Errors that occur during the save are surfaced via the <see cref="SyncSaveOpErrors"/> event handler.
        /// - Use <see cref="SaveSettingsAsync"/> for awaitable, exception-aware saving.
        /// </remarks>
        public void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null);

        /// <summary>
        /// Saves the current user and default settings for the specified configuration type to disk asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveOpErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be saved.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// Optional access token for write authorization. If not specified, defaults to <see cref="Token.Public"/>.
        /// </param>
        /// <remarks>
        /// - This is a fire-and-forget operation; exceptions are not thrown synchronously.
        /// - Errors that occur during the save are surfaced via the <see cref="SyncSaveOpErrors"/> event handler.
        /// - Use <see cref="SaveSettingsAsync"/> for awaitable, exception-aware saving.
        /// </remarks>
        public void SaveSettings(Type configType, ITokenSet tokenSet);


        /// <summary>
        /// Saves settings for a specific type.
        /// </summary>
        /// <param name="configType">The configuration type.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        public Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves settings for a specific type.
        /// </summary>
        /// <param name="configType">The configuration type.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        public Task SaveSettingsAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default);
    }
}
