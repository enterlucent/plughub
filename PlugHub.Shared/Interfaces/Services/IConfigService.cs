using Microsoft.Extensions.Configuration;
using PlugHub.Shared.Models;

namespace PlugHub.Shared.Interfaces.Services
{
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
    /// Event arguments for when an application-level setting changes in the <see cref="ConfigService"/>.
    /// Provides the key and both the old and new values of the setting.
    /// </summary>
    /// <param name="key">The key of the setting that changed.</param>
    /// <param name="oldValue">The previous value of the setting.</param>
    /// <param name="newValue">The new value of the setting.</param>
    public class ConfigServiceAppSettingChangeEventArgs(string key, object? oldValue, object? newValue) : EventArgs
    {
        /// <summary>
        /// Gets the key of the application setting that changed.
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
    /// Event arguments for when a plugin or user setting changes in the <see cref="ConfigService"/>.
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
    /// Provides methods for managing application and plugin configuration settings.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Creates an <see cref="IConfigAccessor"/> for a single configuration type, 
        /// using the specified read and write tokens for access control.
        /// This accessor provides type-safe get/set/save operations for the registered config type.
        /// </summary>
        /// <param name="configTypes">The configuration type to access.</param>
        /// <param name="readToken">An optional token for read access; defaults to public if not specified.</param>
        /// <param name="writeToken">An optional token for write access; defaults to the read token if not specified.</param>
        /// <returns>An <see cref="IConfigAccessor"/> scoped to the specified type and tokens.</returns>
        public IConfigAccessor CreateAccessor(Type configTypes, Token? readToken = null, Token? writeToken = null);

        /// <summary>
        /// Creates an <see cref="IConfigAccessor"/> for multiple configuration types,
        /// using the specified read and write tokens for access control.
        /// This accessor enables batch or multi-type config operations with unified token validation.
        /// </summary>
        /// <param name="configTypes">The configuration types to access.</param>
        /// <param name="readToken">An optional token for read access; defaults to public if not specified.</param>
        /// <param name="writeToken">An optional token for write access; defaults to the read token if not specified.</param>
        /// <returns>An <see cref="IConfigAccessor"/> scoped to the specified types and tokens.</returns>
        public IConfigAccessor CreateAccessor(IEnumerable<Type> configTypes, Token? readToken = null, Token? writeTOken = null);


        /// <summary>
        /// Occurs when the application configuration file is reloaded from disk (for example, due to an external file change).
        /// </summary>
        public event EventHandler<EventArgs>? AppConfigReloaded;

        /// <summary>
        /// Occurs when a plugin configuration file is reloaded from disk (for example, due to an external file change).
        /// </summary>
        public event EventHandler<ConfigServiceConfigReloadedEventArgs>? ConfigReloaded;

        /// <summary>
        /// Occurs when an application-wide configuration value is changed via <see cref="SetAppSetting{T}(string, T)"/>.
        /// </summary>
        public event EventHandler<ConfigServiceAppSettingChangeEventArgs> AppSettingChanged;

        /// <summary>
        /// Occurs when a plugin-specific configuration value is changed via <see cref="SetSetting{T}(Type, string, T, bool)"/>.
        /// </summary>
        public event EventHandler<ConfigServiceSettingChangeEventArgs> SettingChanged;


        /// <summary>
        /// Registers a single plugin configuration type.
        /// </summary>
        /// <param name="configType">The type representing the plugin configuration.</param>
        public void RegisterConfig(Type configType, Token? readToken = null, Token? writeToken = null);

        /// <summary>
        /// Registers plugin configuration types.
        /// </summary>
        /// <param name="configTypes">The types representing plugin configurations.</param>
        public void RegisterConfigs(IEnumerable<Type> configTypes, Token? readToken = null, Token? writeToken = null);


        /// <summary>
        /// Gets the raw contents of the base configuration file for the specified configuration type.
        /// </summary>
        /// <param name="configType">The type of the configuration section.</param>
        /// <param name="token">
        /// (Optional) The security token used for access validation. If not provided, <see cref="Token.Public"/> is used.
        /// </param>
        /// <returns>The contents of the base configuration file as a string.</returns>
        public string GetBaseConfigFileContents(Type configType, Token? token = null);

        /// <summary>
        /// Gets the raw contents of the base configuration file for the specified generic configuration type.
        /// </summary>
        /// <typeparam name="TConfig">The configuration section type.</typeparam>
        /// <param name="token">
        /// (Optional) The security token used for access validation. If not provided, <see cref="Token.Public"/> is used.
        /// </param>
        /// <returns>The contents of the base configuration file as a string.</returns>
        public string GetBaseConfigFileContents<TConfig>(Token? token = null) where TConfig : class;


        /// <summary>
        /// Asynchronously saves the provided contents to the base configuration file for the specified configuration type.
        /// </summary>
        /// <param name="configType">The type of the configuration section.</param>
        /// <param name="contents">The raw string contents to write to the base configuration file.</param>
        /// <param name="token">
        /// (Optional) The security token used for access validation. If not provided, <see cref="Token.Public"/> is used.
        /// </param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public Task SaveBaseConfigFileContentsAsync(Type configType, string contents, Token? token = null);

        /// <summary>
        /// Asynchronously saves the provided contents to the base configuration file for the specified generic configuration type.
        /// </summary>
        /// <typeparam name="TConfig">The configuration section type.</typeparam>
        /// <param name="contents">The raw string contents to write to the base configuration file.</param>
        /// <param name="token">
        /// (Optional) The security token used for access validation. If not provided, <see cref="Token.Public"/> is used.
        /// </param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        public Task SaveBaseConfigFileContentsAsync<TConfig>(string contents, Token? token = null) where TConfig : class;


        /// <summary>
        /// Retrieves a fully populated configuration instance of the specified type.
        /// This method merges base and user settings according to PlugHub config rules,
        /// returning an object that reflects the effective configuration state.
        /// </summary>
        /// <param name="configType">The Type of configuration class to retrieve</param>
        /// <param name="token">Optional security token for access validation</param>
        /// <returns>A populated instance of the requested configuration type</returns>
        /// <exception cref="KeyNotFoundException">Thrown if configType is not registered</exception>
        /// <exception cref="InvalidOperationException">Thrown if instance creation fails</exception>
        public object GetConfigInstance(Type configType, Token? token = null);

        /// <summary>
        /// Persists property values from a configuration instance to storage.
        /// Applies each property using the config service's merge logic, ensuring user values
        /// are only stored when they differ from base values (minimal user config footprint).
        /// </summary>
        /// <param name="configType">The Type of configuration being updated</param>
        /// <param name="updatedConfig">Instance containing new property values</param>
        /// <param name="token">Optional security token for write validation</param>
        /// <exception cref="ArgumentNullException">Thrown if updatedConfig is null</exception>
        public void SaveConfigInstance(Type configType, object updatedConfig, Token? token = null);



        /// <summary>
        /// Gets a read-only <see cref="IConfiguration"/> representing the current environment variables and command-line arguments.
        /// </summary>
        /// <returns>
        /// An <see cref="IConfiguration"/> instance containing environment and command-line settings. This object is read-only.
        /// </returns>
        public IConfiguration GetEnvConfig();

        /// <summary>
        /// Gets an application-wide setting by key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value.</returns>
        public T GetAppSetting<T>(string key);


        /// <summary>
        /// Gets a plugin-specific setting by type and key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="configType">The plugin configuration type.</param>
        /// <param name="key">The setting key.</param>
        /// <returns>The setting value.</returns>
        public T GetSetting<T>(Type configType, string key, Token? token = null);

        /// <summary>
        /// Sets a plugin-specific setting by type and key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="configType">The plugin configuration type.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <param name="isBase">Indicates if this is a system-level setting.</param>
        public void SetSetting<T>(Type configType, string key, T value, Token? token = null);

        /// <summary>
        /// Saves settings for a specific plugin.
        /// </summary>
        /// <param name="configType">The plugin configuration type.</param>
        public Task SaveSettingsAsync(Type configType, Token? token = null);
    }
}
