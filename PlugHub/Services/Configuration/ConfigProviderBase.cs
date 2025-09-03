using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Services.Configuration.Providers;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace PlugHub.Services.Configuration
{
    public abstract class ConfigProviderBase : IConfigServiceProvider, IDisposable
    {
        private class ConfigChangeContext(Type configType, ReaderWriterLockSlim configLock, UserConfigServiceConfig? config, bool configFound)
        {
            public Type ConfigType { get; } = configType;
            public ReaderWriterLockSlim ConfigLock { get; } = configLock;
            public UserConfigServiceConfig? Config { get; } = config;
            public bool ConfigFound { get; } = configFound;
        }

        public IEnumerable<Type> SupportedParamsTypes { get; init; } = [];
        public Type RequiredAccessorInterface { get; init; } = typeof(IFileConfigAccessor);

        protected readonly ILogger<IConfigServiceProvider> Logger;
        protected readonly ITokenService TokenService;
        protected readonly ConcurrentDictionary<Type, Timer> ReloadTimers = new();
        protected readonly ConcurrentDictionary<Type, object?> Configs = [];
        protected readonly ConcurrentDictionary<Type, ReaderWriterLockSlim> ConfigLock = new();

        protected JsonSerializerOptions JsonOptions { get; init; } = new JsonSerializerOptions();
        protected bool IsDisposed = false;

        public ConfigProviderBase(ILogger<IConfigServiceProvider> logger, ITokenService tokenService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(tokenService);

            this.Logger = logger;
            this.TokenService = tokenService;
        }

        #region ConfigServiceBase: Registration

        public virtual void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService service)
            => throw new NotImplementedException();
        public virtual void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams, IConfigService service)
        {
            ArgumentNullException.ThrowIfNull(configTypes);
            ArgumentNullException.ThrowIfNull(configParams);
            ArgumentNullException.ThrowIfNull(service);

            foreach (Type configType in configTypes)
            {
                this.RegisterConfig(configType, configParams, service);
            }
        }

        public virtual void UnregisterConfig(Type configType, Token? token = null)
            => throw new NotImplementedException();
        public virtual void UnregisterConfig(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.UnregisterConfig(configType, tokenSet.Owner);
        }
        public virtual void UnregisterConfigs(IEnumerable<Type> configTypes, Token? token = null)
        {
            ArgumentNullException.ThrowIfNull(configTypes);

            foreach (Type configType in configTypes)
            {
                this.UnregisterConfig(configType, token);
            }
        }
        public virtual void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(configTypes);
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.UnregisterConfigs(configTypes, tokenSet.Owner);
        }

        #endregion

        #region ConfigServiceBase: Value Operations

        public virtual T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => throw new NotImplementedException();
        public virtual T GetDefault<T>(Type configType, string key, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetDefault<T>(configType, key, tokenSet.Owner, tokenSet.Read);
        }

        public virtual T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => throw new NotImplementedException();
        public virtual T GetSetting<T>(Type configType, string key, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetSetting<T>(configType, key, tokenSet.Owner, tokenSet.Read);
        }

        public virtual void SetDefault<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SetDefault<T>(Type configType, string key, T newValue, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SetDefault(configType, key, newValue, tokenSet.Owner, tokenSet.Write);
        }

        public virtual void SetSetting<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SetSetting<T>(Type configType, string key, T value, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SetSetting(configType, key, value, tokenSet.Owner, tokenSet.Write);
        }

        public virtual void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SaveSettings(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SaveSettings(configType, tokenSet.Owner, tokenSet.Write);
        }

        public virtual async Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
            => await Task.FromException(new NotImplementedException()).ConfigureAwait(false);
        public virtual async Task SaveSettingsAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            await this.SaveSettingsAsync(configType, tokenSet.Owner, tokenSet.Write, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region ConfigServiceBase: Instance Operations

        public virtual object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
            => throw new NotImplementedException();

        public virtual object GetConfigInstance(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetConfigInstance(configType, tokenSet.Owner, tokenSet.Read);
        }

        public virtual void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();

        public virtual void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SaveConfigInstance(configType, updatedConfig, tokenSet.Owner, tokenSet.Write);
        }

        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
            => await Task.FromException(new NotImplementedException()).ConfigureAwait(false);

        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            await this.SaveConfigInstanceAsync(configType, updatedConfig, tokenSet.Owner, tokenSet.Write, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region ConfigServiceBase: Default Configuration Operations

        public virtual string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
            => throw new NotImplementedException();
        public virtual string GetDefaultConfigFileContents(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetDefaultConfigFileContents(configType, tokenSet.Owner);
        }

        public virtual void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null)
            => throw new NotImplementedException();
        public virtual void SaveDefaultConfigFileContents(Type configType, string contents, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SaveDefaultConfigFileContents(configType, contents, tokenSet.Owner);
        }

        public virtual async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default)
            => await Task.FromException(new NotImplementedException()).ConfigureAwait(false);
        public virtual async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            await this.SaveDefaultConfigFileContentsAsync(configType, contents, tokenSet.Owner, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region ConfigServiceBase: Configuration Change Handling

        protected virtual void HandleConfigHasChanged(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            ConfigChangeContext context = this.AcquireConfigChangeContext(configType);

            if (!context.ConfigFound)
            {
                this.Logger.LogWarning("Unregistered config type – Type {Type}", configType.Name);

                return;
            }

            try
            {
                bool reloadSucceeded = this.ProcessConfigReload(context);

                if (reloadSucceeded)
                {
                    this.NotifyConfigReloaded(context);
                }
            }
            catch (FileNotFoundException ex)
            {
                this.Logger.LogWarning(ex, "Config directory missing – Type {Type}", configType.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Config reload failed – Type {Type}", configType.Name);
            }
            finally
            {
                ReleaseConfigChangeContext(context);
            }
        }
        protected virtual void OnConfigHasChanged(object? state)
        {
            if (state is not Type configType)
            {
                return;
            }

            Timer debounceTimer = this.GetOrCreateDebounceTimer(configType);

            debounceTimer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);
        }

        private ConfigChangeContext AcquireConfigChangeContext(Type configType)
        {
            ReaderWriterLockSlim configLock = this.GetConfigTypeLock(configType);
            configLock.EnterWriteLock();

            bool configFound = this.Configs.TryGetValue(configType, out object? rawConfig);
            bool isCorrectType = configFound && rawConfig is UserConfigServiceConfig;

            if (isCorrectType)
            {
                UserConfigServiceConfig config = (UserConfigServiceConfig)rawConfig!;
                return new ConfigChangeContext(configType, configLock, config, true);
            }
            else
            {
                return new ConfigChangeContext(configType, configLock, null, false);
            }
        }

        private bool ProcessConfigReload(ConfigChangeContext context)
        {
            if (!context.ConfigFound || context.Config == null)
            {
                return false;
            }

            Dictionary<string, object?> newSettings = this.BuildSettings(
                context.ConfigType,
                context.Config.Config,
                context.Config.UserConfig);

            context.Config.Values = newSettings;

            bool shouldRebindChangeEvents = context.Config.ReloadOnChanged;

            if (shouldRebindChangeEvents)
            {
                this.RebindConfigChangeEvents(context.Config);
            }

            return true;
        }

        private void RebindConfigChangeEvents(UserConfigServiceConfig config)
        {
            DisposeExistingChangeTokens(config);

            config.OnChanged = config.Config
                .GetReloadToken()
                .RegisterChangeCallback(this.OnConfigHasChanged, config.GetType());

            config.UserOnChanged = config.UserConfig
                .GetReloadToken()
                .RegisterChangeCallback(this.OnConfigHasChanged, config.GetType());
        }

        private static void DisposeExistingChangeTokens(UserConfigServiceConfig config)
        {
            config.OnChanged?.Dispose();
            config.UserOnChanged?.Dispose();
        }

        private void NotifyConfigReloaded(ConfigChangeContext context)
        {
            if (context.Config?.ConfigService != null)
            {
                context.Config.ConfigService.OnConfigReloaded(this, context.ConfigType);
            }
        }

        private static void ReleaseConfigChangeContext(ConfigChangeContext context)
        {
            if (context.ConfigLock.IsWriteLockHeld)
            {
                context.ConfigLock.ExitWriteLock();
            }
        }

        private Timer GetOrCreateDebounceTimer(Type configType)
        {
            return this.ReloadTimers.GetOrAdd(configType, this.CreateDebounceTimer);
        }

        private Timer CreateDebounceTimer(Type configType)
        {
            return new Timer(
                _ => this.HandleConfigHasChanged(configType),
                null,
                TimeSpan.FromMilliseconds(300),
                Timeout.InfiniteTimeSpan);
        }

        #endregion

        #region ConfigServiceBase: Configuration Building

        protected virtual Dictionary<string, object?> BuildSettings(Type configType, params IConfiguration[] configSources)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configSources);

            Dictionary<string, object?> settings = [];
            PropertyInfo[] properties = GetConfigurationProperties(configType);

            foreach (PropertyInfo property in properties)
            {
                UserConfigServiceSetting setting = this.BuildPropertySetting(property, configSources);
                settings[property.Name] = setting;
            }

            return settings;
        }
        private static PropertyInfo[] GetConfigurationProperties(Type configType)
        {
            return configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }
        private UserConfigServiceSetting BuildPropertySetting(PropertyInfo property, IConfiguration[] configSources)
        {
            object? defaultValue = null;
            object? userValue = null;

            foreach (IConfiguration configSource in configSources)
            {
                object? configValue = this.GetBuildSettingsValue(configSource, property);

                bool hasConfigValue = configValue != null;

                if (hasConfigValue)
                {
                    bool isFirstValue = defaultValue == null;

                    if (isFirstValue)
                    {
                        defaultValue = configValue;
                    }
                    else
                    {
                        userValue = configValue;
                    }
                }
            }

            return new UserConfigServiceSetting(
                valueType: property.PropertyType,
                value: defaultValue,
                userValue: userValue,
                readAccess: property.CanRead,
                writeAccess: property.CanWrite);
        }

        protected virtual object? GetBuildSettingsValue(IConfiguration config, PropertyInfo property)
        {
            IConfigurationSection section = config.GetSection(property.Name);

            bool sectionExists = section.Exists();

            if (!sectionExists)
            {
                return null;
            }

            return this.ConvertConfigurationValue(section, property.PropertyType);
        }
        private object? ConvertConfigurationValue(IConfigurationSection section, Type propertyType)
        {
            try
            {
                return section.Get(propertyType);
            }
            catch
            {
                try
                {
                    return Convert.ChangeType(section.Value, propertyType);
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to convert configuration value {Value} to type {Type}", section.Value, propertyType.Name);

                    return null;
                }
            }
        }
        protected virtual IConfiguration BuildConfig(string filePath, bool reloadOnChange = false)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            try
            {
                return new ConfigurationBuilder()
                    .AddJsonFile(filePath, optional: true, reloadOnChange: reloadOnChange)
                    .Build();
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to build configuration from file '{filePath}'.", ex);
            }
        }

        #endregion

        #region ConfigServiceBase: File Operations

        protected virtual void EnsureDirectoryExists(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            string? directoryPath = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directoryPath))
            {
                this.Logger.LogDebug("No directory component found in file path: {FilePath}", filePath);
                return;
            }

            bool directoryExists = Directory.Exists(directoryPath);

            if (!directoryExists)
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);

                    this.Logger.LogDebug("Created directory: {Directory}", directoryPath);
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "Failed to create directory: {Directory}", directoryPath);

                    throw;
                }
            }
        }
        protected virtual void EnsureFileExists(string filePath, JsonSerializerOptions options, Type? configType = null)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(options);

            this.EnsureDirectoryExists(filePath);

            bool fileExists = File.Exists(filePath);

            if (!fileExists)
            {
                try
                {
                    string fileContent = this.CreateDefaultFileContent(configType, options);

                    Atomic.Write(filePath, fileContent);

                    this.Logger.LogDebug("Created configuration file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Failed to create the required configuration file at '{filePath}'.", ex);
                }
            }
        }

        private string CreateDefaultFileContent(Type? configType, JsonSerializerOptions options)
        {
            if (configType != null)
            {
                try
                {
                    return configType.SerializeToJson(options);
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to serialize default configuration for type {ConfigType}", configType.Name);
                }
            }

            return "{}";
        }

        protected virtual string ResolveLocalFilePath(string? overridePath, string root, Type configType, string suffix)
        {
            ArgumentNullException.ThrowIfNull(root);
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(suffix);

            if (string.IsNullOrWhiteSpace(overridePath))
            {
                return Path.Combine(root, $"{configType.Name}.{suffix}");
            }

            bool isRootedPath = Path.IsPathRooted(overridePath);

            if (isRootedPath)
            {
                return overridePath;
            }
            else
            {
                return Path.Combine(root, overridePath);
            }
        }

        protected virtual async Task SaveSettingsToFileAsync(string filePath, Dictionary<string, object?> settings, JsonSerializerOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(options);

            string serializedContent = await SerializeSettingsAsync(settings, options).ConfigureAwait(false);

            try
            {
                await Atomic.WriteAsync(filePath, serializedContent, cancellationToken: cancellationToken).ConfigureAwait(false);

                this.Logger.LogDebug("Saved configuration to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Failed to save settings to file: {FilePath}", filePath);
                throw;
            }
        }

        private static async Task<string> SerializeSettingsAsync(Dictionary<string, object?> settings, JsonSerializerOptions options)
        {
            try
            {
                return await Task.Run(() => JsonSerializer.Serialize(settings, options)).ConfigureAwait(false);
            }
            catch (NotSupportedException ex)
            {
                throw new InvalidOperationException("Failed to serialize the provided settings object.", ex);
            }
        }

        #endregion

        #region ConfigServiceBase: Resource Management

        public virtual void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }

            try
            {
                this.PerformDisposal();
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Error during ConfigServiceBase disposal");
            }

            GC.SuppressFinalize(this);
            this.Logger.LogDebug("ConfigServiceBase disposed");
        }
        private void PerformDisposal()
        {
            List<ReaderWriterLockSlim> locks = this.AcquireAllConfigLocks();

            try
            {
                this.DisposeReloadTimers();
                this.DisposeConfigurationObjects();
                this.ClearCollections();
                this.IsDisposed = true;
            }
            finally
            {
                this.ReleaseAllConfigLocks(locks);
                this.DisposeConfigLocks();
            }
        }

        private List<ReaderWriterLockSlim> AcquireAllConfigLocks()
        {
            List<ReaderWriterLockSlim> locks = [.. this.ConfigLock.Values];

            foreach (ReaderWriterLockSlim configLock in locks)
            {
                try
                {
                    configLock.EnterWriteLock();
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to acquire write lock during disposal");
                }
            }

            return locks;
        }

        private void DisposeReloadTimers()
        {
            foreach (Timer timer in this.ReloadTimers.Values)
            {
                try
                {
                    timer.Dispose();
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Error disposing reload timer");
                }
            }
        }
        private void DisposeConfigurationObjects()
        {
            foreach (object? configObject in this.Configs.Values)
            {
                if (configObject is UserConfigServiceConfig config)
                {
                    try
                    {
                        DisposeExistingChangeTokens(config);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogWarning(ex, "Error disposing configuration change tokens");
                    }
                }
            }
        }

        private void ClearCollections()
        {
            this.ReloadTimers.Clear();
            this.Configs.Clear();
        }
        private void ReleaseAllConfigLocks(List<ReaderWriterLockSlim> locks)
        {
            foreach (ReaderWriterLockSlim configLock in locks)
            {
                try
                {
                    if (configLock.IsWriteLockHeld)
                    {
                        configLock.ExitWriteLock();
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Failed to release write lock during disposal");
                }
            }
        }
        private void DisposeConfigLocks()
        {
            foreach (ReaderWriterLockSlim configLock in this.ConfigLock.Values)
            {
                try
                {
                    configLock.Dispose();
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Error disposing configuration lock");
                }
            }
        }

        #endregion

        #region ConfigServiceBase: Validation and Helpers

        [return: MaybeNull]
        protected virtual T CastStoredValue<T>(object? raw)
        {
            if (raw is null)
            {
                return default;
            }

            bool isCorrectType = raw is T;

            if (isCorrectType)
            {
                return (T)raw;
            }

            try
            {
                bool isConvertible = raw is IConvertible;

                if (isConvertible)
                {
                    return (T)Convert.ChangeType(raw, typeof(T));
                }
            }
            catch (InvalidCastException ex)
            {
                this.Logger.LogWarning(ex, "Invalid cast when converting stored value");
            }
            catch (FormatException ex)
            {
                this.Logger.LogWarning(ex, "Format error when converting stored value");
            }
            catch (OverflowException ex)
            {
                this.Logger.LogWarning(ex, "Overflow error when converting stored value");
            }

            return default;
        }

        protected virtual ReaderWriterLockSlim GetConfigTypeLock(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            return this.ConfigLock.GetOrAdd(configType, _ => new ReaderWriterLockSlim());
        }

        #endregion
    }
}