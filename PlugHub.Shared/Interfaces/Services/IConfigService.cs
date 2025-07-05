using Microsoft.Extensions.Configuration;
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
    /// Event args for the SaveCompleted event.
    /// Carries info about what was saved, where, and how.
    /// </summary>
    /// <remarks>
    /// Create a new event args for a completed save.
    /// </remarks>
    public class ConfigServiceSaveCompletedEventArgs(Type configType) : EventArgs
    {
        /// <summary>
        /// The type of config that was saved (e.g., AppConfig).
        /// </summary>
        public Type ConfigType { get; } = configType;
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
        /// Occurs when a fire-and-forget save operation in <see cref="ConfigService"/> completes.
        /// </summary>
        public event EventHandler<ConfigServiceSaveCompletedEventArgs>? SyncSaveCompleted;

        /// <summary>
        /// Occurs when a fire-and-forget save operation in <see cref="ConfigService"/> fails.
        /// </summary>
        public event EventHandler<ConfigServiceSaveErrorEventArgs>? SyncSaveErrors;

        /// <summary>
        /// Occurs when a configuration file is reloaded from disk (for example, due to an external file change).
        /// </summary>
        public event EventHandler<ConfigServiceConfigReloadedEventArgs>? ConfigReloaded;

        /// <summary>
        /// Occurs when a type-specific configuration value is changed via <see cref="SetSetting{T}(Type, string, T, bool)"/>.
        /// </summary>
        public event EventHandler<ConfigServiceSettingChangeEventArgs>? SettingChanged;


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
        /// Registers a configuration type with granular token-based access control and type-specific JSON serialization settings.
        /// </summary>
        /// <param name="configType">The type representing the configuration structure.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <param name="jsonOptions">Custom JSON serialization settings for this configuration type, overriding global options for property naming, converters, and formatting.</param>
        /// <param name="reloadOnChange">Enables automatic reloading when underlying configuration files change.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void RegisterConfig(Type configType, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);


        /// <summary>
        /// Registers a configuration type with granular token-based access control and type-specific JSON serialization settings.
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
        /// Registers a configuration type with granular token-based access control and type-specific JSON serialization settings.
        /// </summary>
        /// <param name="configTypes">The configuration types to register.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <param name="jsonOptions">Custom JSON serialization settings applied to all specified configuration types.</param>
        /// <param name="reloadOnChange">Enables automatic reload when underlying configuration files change.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void RegisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false);


        /// <summary>
        /// Unregisters a single configuration type from the service, removing all associated settings and change listeners.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> of the configuration to unregister.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void UnregisterConfig(Type configType, Token? ownerToken = null);

        /// <summary>
        /// Unregisters a single configuration type from the service, removing all associated settings and change listeners.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> of the configuration to unregister.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <exception cref="UnauthorizedAccessException"/>
        public void UnregisterConfig(Type configType, ITokenSet tokenSet);


        /// <summary>
        /// Unregisters multiple configuration types from the service, removing all associated settings and change listeners for each.
        /// </summary>
        /// <param name="configTypes">A collection of <see cref="Type"/> objects representing the configurations to unregister.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="UnauthorizedAccessException"/>
        public void UnregisterConfigs(IEnumerable<Type> configTypes, Token? ownerToken = null);

        /// <summary>
        /// Unregisters multiple configuration types from the service, removing all associated settings and change listeners for each.
        /// </summary>
        /// <param name="configTypes">A collection of <see cref="Type"/> objects representing the configurations to unregister.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="UnauthorizedAccessException"/>
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
        /// This method initiates the write operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose default config file should be overwritten.</param>
        /// <param name="contents">The raw JSON contents to write to the default config file.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        public void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null);

        /// <summary>
        /// Overwrites the default configuration file for the specified type with the provided JSON contents asynchronously.
        /// This method initiates the write operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose default config file should be overwritten.</param>
        /// <param name="contents">The raw JSON contents to write to the default config file.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>        
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
        /// <exception cref="KeyNotFoundException"/>
        /// <exception cref="InvalidOperationException"/>
        public object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null);

        /// <summary>
        /// Retrieves a fully populated configuration instance of the specified type.
        /// This method merges default and user settings according to PlugHub config rules,
        /// returning an object that reflects the effective configuration state.
        /// </summary>
        /// <param name="configType">The Type of configuration class to retrieve</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>A populated instance of the requested configuration type</returns>
        /// <exception cref="KeyNotFoundException"/>
        /// <exception cref="InvalidOperationException"/>
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
        /// <exception cref="ArgumentNullException"/>
        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists property values from a configuration instance to storage.
        /// Applies each property using the config service's merge logic, ensuring user values
        /// are only stored when they differ from default values (minimal user config footprint).
        /// </summary>
        /// <param name="configType">The Type of configuration being updated</param>
        /// <param name="updatedConfig">Instance containing new property values</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <exception cref="ArgumentNullException"/>
        public Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default);


        /// <summary>
        /// Saves the provided configuration object instance to the user settings file for the specified type asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be updated.</param>
        /// <param name="updatedConfig">The updated configuration object to persist.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        public void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null);

        /// <summary>
        /// Saves the provided configuration object instance to the user settings file for the specified type asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be updated.</param>
        /// <param name="updatedConfig">The updated configuration object to persist.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        public void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet);


        /// <summary>
        /// Gets a read-only <see cref="IConfiguration"/> representing the current environment variables and command-line arguments.
        /// </summary>
        /// <returns>An <see cref="IConfiguration"/> instance containing environment and command-line settings. This object is read-only.</returns>
        public IConfiguration GetEnvConfig();


        /// <summary>
        /// Returns the *baseline* value that was loaded from
        /// <paramref name="key"/> – ignoring any user-override that might currently exist.
        /// </summary>
        /// <typeparam name="T">Desired return type.  If the stored object implements <see cref="IConvertible"/> it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section whose default you want to inspect. Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <returns> The default value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException"/>
        /// <exception cref="KeyNotFoundException"/>
        T? GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);

        /// <summary>
        /// Returns the *baseline* value that was loaded from
        /// <paramref name="key"/> – ignoring any user-override that might currently exist.
        /// </summary>
        /// <typeparam name="T">Desired return type.  If the stored object implements <see cref="IConvertible"/> it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section whose default you want to inspect. Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns> The default value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException"/>
        /// <exception cref="KeyNotFoundException"/>
        T? GetDefault<T>(Type configType, string key, ITokenSet tokenSet);


        /// <summary>
        /// Returns the *effective* value for the property named <paramref name="key"/>
        /// </summary>
        /// <typeparam name="T">Desired return type. If the stored object implements <see cref="IConvertible"/>, it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section you want to query.Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <returns>The effective value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException"/>
        /// <exception cref="KeyNotFoundException"/>
        /// <exception cref="UnauthorizedAccessException"/>
        public T? GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);

        /// <summary>
        /// Returns the *effective* value for the property named <paramref name="key"/>
        /// </summary>
        /// <typeparam name="T">Desired return type. If the stored object implements <see cref="IConvertible"/>, it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section you want to query.Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>The effective value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="ConfigTypeNotFoundException"/>
        /// <exception cref="KeyNotFoundException"/>
        /// <exception cref="UnauthorizedAccessException"/>
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
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be saved.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>        
        public void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null);

        /// <summary>
        /// Saves the current user and default settings for the specified configuration type to disk asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be saved.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
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


        /// <summary>
        /// Raises the <see cref="SyncSaveCompleted"/> event after a configuration save operation completes successfully.
        /// Call this from within the implementing class to notify subscribers that a save has finished.
        /// </summary>
        /// <param name="configType">The type of configuration that was saved.</param>
        void OnSaveOperationComplete(Type configType);

        /// <summary>
        /// Raises the <see cref="SyncSaveErrors"/> event when a configuration save operation fails.
        /// Call this from within the implementing class to notify subscribers of the error.
        /// </summary>
        /// <param name="ex">The exception that occurred during the save operation.</param>
        /// <param name="operation">The kind of save operation that failed.</param>
        /// <param name="configType">The type of configuration involved in the failed operation.</param>
        void OnSaveOperationError(Exception ex, ConfigSaveOperation operation, Type configType);
    }
}