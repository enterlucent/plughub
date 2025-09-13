using Microsoft.Extensions.Configuration;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Models;
using System.Text.Json;

namespace PlugHub.Shared.Interfaces.Services.Configuration
{
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
    /// Represents metadata used during configuration registration to control access permissions and optional reload behavior.
    /// </summary>
    public interface IConfigServiceParams
    {
        /// <summary>
        /// Gets the token that identifies the owner of the configuration type.
        /// This token is used to assert privileged access (e.g., full registration/unregistration rights).
        /// </summary>
        Token? Owner { get; }

        /// <summary>
        /// Gets the token required to read settings from the configuration.
        /// May be <see cref="Token.Public"/> to allow unrestricted read access.
        /// </summary>
        Token? Read { get; }

        /// <summary>
        /// Gets the token required to write or override configuration settings.
        /// If set to <see cref="Token.Blocked"/>, the configuration is considered read-only to consumers.
        /// </summary>
        Token? Write { get; }

        /// <summary>
        /// Gets a value indicating whether this configuration type should be automatically reloaded
        /// when the underlying file changes on disk.
        /// </summary>
        bool ReloadOnChange { get; }
    }


    /// <summary>
    /// Provides methods for managing application and type-defaultd configuration settings.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Gets the fallback JSON serialization options used by configuration services.
        /// Consumers may override these at finer-grained levels.
        /// </summary>
        public JsonSerializerOptions JsonOptions { get; }

        /// <summary>
        /// Gets the directory path where the current application is executing. 
        /// </summary>
        public string ConfigAppDirectory { get; }

        /// <summary>
        /// Gets the platform-specific data directory.
        /// </summary>
        public string ConfigDataDirectory { get; }

        /// <summary>
        /// Builds and returns an <see cref="IConfiguration"/> that includes environment variables and command-line arguments for the current process.
        /// </summary>
        IConfiguration GetEnvConfig();

        #region COnfigService: Predicates

        /// <summary>
        /// Determines whether the specified configuration type is registered with the configuration service.
        /// </summary>
        /// <param name="configType">The configuration type to check.</param>
        /// <returns><see langword="true"/> if the configuration type is registered; otherwise, <see langword="false"/>.</returns>
        bool IsRegistered(Type configType);

        #endregion

        #region ConfigService: Accessors

        /// <summary>
        /// Retrieves an accessor for the given interface and multiple config types using a token set for access control.
        /// </summary>
        /// <param name="accessorInterface">Type of accessor interface requested.</param>
        /// <param name="configTypes">Collection of config types to be accessed.</param>
        /// <param name="tokenSet">Token set for ownership and permissions.</param>
        /// <returns>Configured accessor instance.</returns>
        IConfigAccessor GetAccessor(Type accessorInterface, IEnumerable<Type> configTypes, ITokenSet tokenSet);

        /// <summary>
        /// Retrieves an accessor for the given interface and multiple config types using explicit access tokens.
        /// </summary>
        /// <param name="accessorInterface">Type of accessor interface requested.</param>
        /// <param name="configTypes">Collection of config types to be accessed.</param>
        /// <param name="owner">Owner token.</param>
        /// <param name="read">Read permission token.</param>
        /// <param name="write">Write permission token.</param>
        /// <returns>Configured accessor instance.</returns>
        IConfigAccessor GetAccessor(Type accessorInterface, IEnumerable<Type> configTypes, Token? owner = null, Token? read = null, Token? write = null);


        /// <summary>Retrieves a configuration accessor for the specified configuration type <typeparamref name="TConfig"/>.</summary>
        /// <param name="owner">Optional owner token used for authorization. If <c>null</c>, a new token will be generated.</param>
        /// <param name="read">Optional read token used for authorization. If <c>null</c>, a new token will be generated.</param>
        /// <param name="write">Optional write token used for authorization. If <c>null</c>, a new token will be generated.</param>
        /// <typeparam name="TConfig">The type of the configuration to access. Must be a reference type.</typeparam>
        /// <returns>An <see cref="IConfigAccessorFor{TConfig}"/> instance to access and manipulate the specified configuration.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the configuration type <typeparamref name="TConfig"/> is not registered.</exception>
        /// <exception cref="InvalidOperationException">Thrown if no accessor provider is registered for the configuration's required accessor interface.</exception>
        IConfigAccessorFor<TConfig> GetAccessor<TConfig>(Token? owner = null, Token? read = null, Token? write = null) where TConfig : class;

        /// <summary>Retrieves a configuration accessor for the specified configuration type <typeparamref name="TConfig"/>, using the provided token set for authorization.</summary>
        /// <param name="tokenSet">The set of tokens (owner, read, write) used for authorization.</param>
        /// <typeparam name="TConfig">The type of the configuration to access. Must be a reference type.</typeparam>
        /// <returns>An <see cref="IConfigAccessorFor{TConfig}"/> instance to access and manipulate the specified configuration.</returns>
        IConfigAccessorFor<TConfig> GetAccessor<TConfig>(ITokenSet tokenSet) where TConfig : class;

        /// <summary>Retrieves a strongly-typed configuration accessor of type <typeparamref name="TAccessor"/> for the specified configuration type <typeparamref name="TConfig"/>.</summary>
        /// <param name="owner">Optional owner token used for authorization. If <c>null</c>, a new token will be generated.</param>
        /// <param name="read">Optional read token used for authorization. If <c>null</c>, a new token will be generated.</param>
        /// <param name="write">Optional write token used for authorization. If <c>null</c>, a new token will be generated.</param>
        /// <typeparam name="TAccessor">The specific accessor type to return, which must implement <see cref="IConfigAccessorFor{TConfig}"/>.</typeparam>
        /// <typeparam name="TConfig">The type of the configuration to access. Must be a reference type.</typeparam>
        /// <returns>An instance of <typeparamref name="TAccessor"/> to access and manipulate the specified configuration.</returns>
        /// <exception cref="InvalidCastException">Thrown if the default accessor cannot be cast to <typeparamref name="TAccessor"/>.</exception>
        TAccessor GetAccessor<TAccessor, TConfig>(Token? owner = null, Token? read = null, Token? write = null)
            where TAccessor : IConfigAccessorFor<TConfig>
            where TConfig : class;

        /// <summary>Retrieves a strongly-typed configuration accessor of type <typeparamref name="TAccessor"/> for the specified configuration type <typeparamref name="TConfig"/>, using the provided token set for authorization.</summary>
        /// <param name="tokenSet">The set of tokens (owner, read, write) used for authorization.</param>
        /// <typeparam name="TAccessor">The specific accessor type to return, which must implement <see cref="IConfigAccessorFor{TConfig}"/>.</typeparam>
        /// <typeparam name="TConfig">The type of the configuration to access. Must be a reference type.</typeparam>
        /// <returns>An instance of <typeparamref name="TAccessor"/> to access and manipulate the specified configuration.</returns>
        /// <exception cref="InvalidCastException">Thrown if the default accessor cannot be cast to <typeparamref name="TAccessor"/>.</exception>
        TAccessor GetAccessor<TAccessor, TConfig>(ITokenSet tokenSet)
            where TAccessor : IConfigAccessorFor<TConfig>
            where TConfig : class;

        #endregion

        #region IConfigService: Registration

        /// <summary>
        /// Registers a single configuration type with the configuration service,
        /// routing it to the appropriate provider based on the type of <paramref name="configParams"/>.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> representing the configuration section to register.</param>
        /// <param name="configParams">An <see cref="IConfigServiceParams"/> instance used to select the config provider and configure registration behavior.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no config provider is registered for the <see cref="Type"/> of <paramref name="configParams"/>,
        /// or if <paramref name="configType"/> is already registered.
        /// </exception>
        /// <remarks>
        /// This method adds <paramref name="configType"/> to the internal provider mapping keyed by config type,
        /// then invokes the provider's own registration logic. It ensures a one-to-one association between config types and providers.
        /// </remarks>
        public void RegisterConfig(Type configType, IConfigServiceParams configParams);

        /// <summary>
        /// Registers a single configuration type with parameters and returns a typed accessor for it.
        /// </summary>
        /// <typeparam name="TConfig">The configuration type to register.</typeparam>
        /// <param name="configParams">Parameters guiding registration and accessor behavior.</param>
        /// <param name="accessor">Output typed accessor for the registered config.</param>
        void RegisterConfig<TConfig>(IConfigServiceParams configParams, out IConfigAccessorFor<TConfig> accessor) where TConfig : class;

        /// <summary>
        /// Registers multiple configuration types in bulk by invoking <see cref="RegisterConfig(Type, IConfigServiceParams)"/> for each.
        /// </summary>
        /// <param name="configTypes">A collection of <see cref="Type"/> instances representing configuration sections to register.</param>
        /// <param name="configParams">An <see cref="IConfigServiceParams"/> instance used to select the config provider and configure registration behavior.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configTypes"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// This method iterates over <paramref name="configTypes"/> and registers each type individually,
        /// applying the same <paramref name="configParams"/> for all. Exceptions thrown by individual registrations
        /// propagate to the caller and halt the bulk operation.
        /// </remarks>
        public void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams);

        /// <summary>
        /// Registers multiple configuration types with associated parameters, returning an accessor covering them all.
        /// </summary>
        /// <param name="configTypes">Collection of config types to register.</param>
        /// <param name="configParams">Parameters guiding registration and accessor behavior.</param>
        /// <param name="accessor">Output accessor handling the registered configs.</param>
        void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams, out IConfigAccessor accessor);


        /// <summary>
        /// Attempts to register a single configuration type with the configuration service.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> representing the configuration section to register.</param>
        /// <param name="configParams">An <see cref="IConfigServiceParams"/> instance used to select the config provider and configure registration behavior.</param>
        /// <returns>
        /// <see langword="true"/> if registration succeeded; <see langword="false"/> if the configuration type is already registered 
        /// or no provider is registered for the params type, or an error occurred.
        /// </returns>
        /// <remarks>
        /// This method calls <see cref="RegisterConfig(Type, IConfigServiceParams)"/> internally and catches exceptions.
        /// </remarks>
        bool TryRegisterConfig(Type configType, IConfigServiceParams configParams);

        /// <summary>
        /// Attempts to register a single configuration type with parameters and outputs a typed accessor for it.
        /// </summary>
        /// <typeparam name="TConfig">The configuration type to register.</typeparam>
        /// <param name="configParams">Parameters guiding registration and accessor behavior.</param>
        /// <param name="accessor">When this method returns, contains the typed accessor if registration succeeded; otherwise, <see langword="null"/>.</param>
        /// <returns>
        /// <see langword="true"/> if registration succeeded and accessor is provided; <see langword="false"/> if the configuration type is already registered
        /// or no provider is registered for the params type, or an error occurred.
        /// </returns>
        /// <remarks>
        /// This method calls <see cref="RegisterConfig{TConfig}(IConfigServiceParams, out IConfigAccessorFor{TConfig})"/> internally and catches exceptions.
        /// </remarks>
        bool TryRegisterConfig<TConfig>(IConfigServiceParams configParams, out IConfigAccessorFor<TConfig>? accessor) where TConfig : class;


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
        /// Attempts to unregister a single configuration type from the service.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> of the configuration to unregister.</param>
        /// <param name="token">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <returns><see langword="true"/> if unregistration succeeded; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// This method calls <see cref="UnregisterConfig(Type, Token?)"/> internally and catches exceptions.
        /// </remarks>
        bool TryUnregsterConfig(Type configType, Token? token = null);

        /// <summary>
        /// Attempts to unregister a single configuration type from the service using a consolidated token set.
        /// </summary>
        /// <param name="configType">The <see cref="Type"/> of the configuration to unregister.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns><see langword="true"/> if unregistration succeeded; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// This method calls <see cref="UnregisterConfig(Type, ITokenSet)"/> internally and catches exceptions.
        /// </remarks>
        bool TryUnregsterConfig(Type configType, ITokenSet tokenSet);

        #endregion

        #region IConfigService: Value Accessors and Mutators

        /// <summary>
        /// Returns the value for the property named <paramref name="key"/>
        /// </summary>
        /// <typeparam name="T">Desired return type. If the stored object implements <see cref="IConvertible"/>, it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section you want to query.Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="readToken">Optional token for read operations. Defaults to <see cref="Token.Public"/> if not specified.</param>
        /// <returns>The effective value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="KeyNotFoundException"/>
        /// <exception cref="UnauthorizedAccessException"/>
        public T GetValue<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null);

        /// <summary>
        /// Returns the value for the property named <paramref name="key"/>
        /// </summary>
        /// <typeparam name="T">Desired return type. If the stored object implements <see cref="IConvertible"/>, it is converted to <typeparamref name="T"/>; otherwise the method attempts a direct cast.</typeparam>
        /// <param name="configType">CLR type that models the configuration section you want to query.Must have been registered with the configuration service.</param>
        /// <param name="key">Public property name declared on <paramref name="configType"/>.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        /// <returns>The effective value converted to <typeparamref name="T"/>; <see langword="default"/> when the conversion cannot be performed.</returns>
        /// <exception cref="KeyNotFoundException"/>
        /// <exception cref="UnauthorizedAccessException"/>
        public T GetValue<T>(Type configType, string key, ITokenSet tokenSet);


        /// <summary>
        /// Sets a type-specific setting by type and key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="configType">The configuration type.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        public void SetValue<T>(Type configType, string key, T value, Token? ownerToken = null, Token? writeToken = null);

        /// <summary>
        /// Sets a type-specific setting by type and key.
        /// </summary>
        /// <typeparam name="T">The type of the setting value.</typeparam>
        /// <param name="configType">The configuration type.</param>
        /// <param name="key">The setting key.</param>
        /// <param name="value">The setting value.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        public void SetValue<T>(Type configType, string key, T value, ITokenSet tokenSet);


        /// <summary>
        /// Saves the current user and default settings for the specified configuration type to disk asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be saved.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>        
        public void SaveValues(Type configType, Token? ownerToken = null, Token? writeToken = null);

        /// <summary>
        /// Saves the current user and default settings for the specified configuration type to disk asynchronously.
        /// This method initiates the save operation and returns immediately; errors are reported via the <see cref="SyncSaveErrors"/> event.
        /// </summary>
        /// <param name="configType">The configuration type whose settings should be saved.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        public void SaveValues(Type configType, ITokenSet tokenSet);


        /// <summary>
        /// Saves settings for a specific type.
        /// </summary>
        /// <param name="configType">The configuration type.</param>
        /// <param name="ownerToken">Optional token for owner operations. Defaults to a new <see cref="Token"/> if not specified.</param>
        /// <param name="writeToken">Optional token for write operations. Defaults to <see cref="Token.Blocked"/> if not specified.</param>
        public Task SaveValuesAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves settings for a specific type.
        /// </summary>
        /// <param name="configType">The configuration type.</param>
        /// <param name="tokenSet">Consolidated token container for owner/read/write permissions.</param>
        public Task SaveValuesAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default);

        #endregion

        #region IConfigService: Instance Accesors and Mutators

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

        #endregion

        #region IConfigService: Event Handlers

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
        /// Invoked when a save operation completes successfully.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="configType">The configuration type involved in the operation.</param>
        void OnSaveOperationComplete(object sender, Type configType);

        /// <summary>
        /// Invoked when a save operation encounters an error.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="ex">The exception thrown during the operation.</param>
        /// <param name="operation">The type of save operation that failed.</param>
        /// <param name="configType">The configuration type involved in the operation.</param>
        void OnSaveOperationError(object sender, Exception ex, ConfigSaveOperation operation, Type configType);

        /// <summary>
        /// Invoked when a configuration is reloaded.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="configType">The configuration type that was reloaded.</param>
        void OnConfigReloaded(object sender, Type configType);

        /// <summary>
        /// Invoked when a configuration setting changes.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="configType">The configuration type containing the changed setting.</param>
        /// <param name="key">The key/name of the changed setting.</param>
        /// <param name="oldValue">The previous value of the setting, or null if none.</param>
        /// <param name="newValue">The new value of the setting, or null if removed.</param>
        void OnSettingChanged(object sender, Type configType, string key, object? oldValue, object? newValue);

        #endregion
    }
}