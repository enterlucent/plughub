using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Utility;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;


namespace PlugHub.Shared.Services.Configuration.Providers
{
    public abstract class BaseConfigProvider : IConfigProvider, IDisposable
    {
        private class ConfigChangeContext(Type sourceType, ReaderWriterLockSlim typeLock, ConfigSource? source, bool sourceFound)
        {
            public Type SourceType { get; } = sourceType;
            public ReaderWriterLockSlim TypeLock { get; } = typeLock;
            public ConfigSource? Source { get; } = source;
            public bool ConfigFound { get; } = sourceFound;
        }

        public IEnumerable<Type> SupportedParamsTypes { get; init; } = [];
        public Type RequiredAccessorInterface { get; init; } = typeof(IConfigAccessor);

        protected readonly ILogger<IConfigProvider> Logger;
        protected readonly ITokenService TokenService;
        protected readonly ConcurrentDictionary<Type, Timer> ReloadTimers = new();
        protected readonly ConcurrentDictionary<Type, object?> Sources = [];
        protected readonly ConcurrentDictionary<Type, ReaderWriterLockSlim> ConfigLock = new();

        protected JsonSerializerOptions JsonOptions { get; init; } = new JsonSerializerOptions();
        protected bool IsDisposed = false;

        public BaseConfigProvider(ILogger<IConfigProvider> logger, ITokenService tokenService)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(tokenService);

            this.Logger = logger;
            this.TokenService = tokenService;
        }

        #region BaseConfigProvider: Registration

        public virtual void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService service)
            => throw new NotImplementedException();
        public virtual void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams, IConfigService service)
        {
            ArgumentNullException.ThrowIfNull(configTypes);
            ArgumentNullException.ThrowIfNull(configParams);
            ArgumentNullException.ThrowIfNull(service);

            foreach (Type configType in configTypes)
                this.RegisterConfig(configType, configParams, service);
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
                this.UnregisterConfig(configType, token);
        }
        public virtual void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(configTypes);
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.UnregisterConfigs(configTypes, tokenSet.Owner);
        }

        #endregion

        #region BaseConfigProvider: Value Operations

        public virtual T GetValue<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => throw new NotImplementedException();
        public virtual T GetValue<T>(Type configType, string key, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetValue<T>(configType, key, tokenSet.Owner, tokenSet.Read);
        }

        public virtual void SetValue<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SetValue<T>(Type configType, string key, T value, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SetValue(configType, key, value, tokenSet.Owner, tokenSet.Write);
        }

        public virtual void SaveValues(Type configType, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SaveValues(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SaveValues(configType, tokenSet.Owner, tokenSet.Write);
        }

        public virtual async Task SaveValuesAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
            => await Task.FromException(new NotImplementedException()).ConfigureAwait(false);
        public virtual async Task SaveValuesAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            await this.SaveValuesAsync(configType, tokenSet.Owner, tokenSet.Write, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region BaseConfigProvider: Instance Operations

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

        #region BaseConfigProvider: Configuration Change Handling

        protected virtual void HandleConfigHasChanged(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            ConfigChangeContext context = this.AcquireConfigChangeContext(configType);

            if (!context.ConfigFound)
            {
                this.Logger.LogWarning("[ConfigProviderBase] Unregistered config type – Type {Type}", configType.Name);

                return;
            }

            try
            {
                bool reloadSucceeded = this.ProcessConfigReload(context);

                if (reloadSucceeded && context.Source?.ConfigService != null)
                    context.Source.ConfigService.OnConfigReloaded(this, context.SourceType);
            }
            catch (FileNotFoundException ex)
            {
                this.Logger.LogWarning(ex, "[ConfigProviderBase] Config directory missing – Type {Type}", configType.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "[ConfigProviderBase] Config reload failed – Type {Type}", configType.Name);
            }
            finally
            {
                if (context.TypeLock.IsWriteLockHeld)
                    context.TypeLock.ExitWriteLock();
            }
        }
        private ConfigChangeContext AcquireConfigChangeContext(Type configType)
        {
            ReaderWriterLockSlim configLock = this.GetConfigTypeLock(configType);
            configLock.EnterWriteLock();

            bool configFound = this.Sources.TryGetValue(configType, out object? rawSource);
            bool isCorrectType = configFound && rawSource is ConfigSource;

            if (isCorrectType)
            {
                ConfigSource source = (ConfigSource)rawSource!;

                return new ConfigChangeContext(configType, configLock, source, true);
            }
            else
            {
                return new ConfigChangeContext(configType, configLock, null, false);
            }
        }
        private bool ProcessConfigReload(ConfigChangeContext context)
        {
            if (!context.ConfigFound || context.Source == null || context.Source.Configuration == null)
                return false;

            Dictionary<string, object?> newSettings = this.BuildSettings(context.SourceType, [context.Source.Configuration]);

            context.Source.Values = newSettings;

            bool shouldRebindChangeEvents = context.Source.ReloadOnChanged;

            if (shouldRebindChangeEvents)
            {
                context.Source.OnChanged?.Dispose();

                if (context.Source.Configuration != null)
                {
                    context.Source.OnChanged =
                        context.Source.Configuration
                            .GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigHasChanged, context.Source.Configuration.GetType());
                }
            }

            return true;
        }

        protected virtual void OnConfigHasChanged(object? state)
        {
            if (state is not Type configType)
                return;

            Timer debounceTimer = this.ReloadTimers.GetOrAdd(configType, this.CreateDebounceTimer);

            debounceTimer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);
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

        #region BaseConfigProvider: Configuration Building

        protected virtual Dictionary<string, object?> BuildSettings(Type configType, IConfiguration[] configSources)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configSources);

            Dictionary<string, object?> settings = [];
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo property in properties)
            {
                ConfigValue setting = this.BuildPropertySetting(property, configSources);

                settings[property.Name] = setting;
            }

            return settings;
        }
        protected virtual ConfigValue BuildPropertySetting(PropertyInfo property, IConfiguration[] configSources)
        {
            object? lastValue = null;

            foreach (IConfiguration configSource in configSources)
            {
                object? configValue = this.GetBuildSettingsValue(configSource, property);

                if (configValue != null)
                    lastValue = configValue;
            }
            return new ConfigValue
            {
                ValueType = property.PropertyType,
                Value = lastValue,
                CanRead = property.CanRead,
                CanWrite = property.CanWrite
            };
        }

        protected virtual object? GetBuildSettingsValue(IConfiguration config, PropertyInfo property)
        {
            IConfigurationSection section = config.GetSection(property.Name);

            bool sectionExists = section.Exists();

            if (!sectionExists)
                return null;

            return this.ConvertConfigurationValue(section, property.PropertyType);
        }
        protected virtual object? ConvertConfigurationValue(IConfigurationSection section, Type propertyType)
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
                    this.Logger.LogWarning(ex, "[ConfigProviderBase] Failed to convert configuration value {Value} to type {Type}", section.Value, propertyType.Name);

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

        #region BaseConfigProvider: File Operations

        protected virtual void EnsureDirectoryExists(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            string? directoryPath = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(directoryPath))
            {
                this.Logger.LogDebug("[ConfigProviderBase] No directory component found in file path: {FilePath}", filePath);

                return;
            }

            bool directoryExists = Directory.Exists(directoryPath);

            if (!directoryExists)
            {
                try
                {
                    Directory.CreateDirectory(directoryPath);

                    this.Logger.LogDebug("[ConfigProviderBase] Created directory: {Directory}", directoryPath);
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, "[ConfigProviderBase] Failed to create directory: {Directory}", directoryPath);

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
                    string fileContent = "{}";

                    if (configType != null)
                    {
                        try
                        {
                            fileContent = configType.SerializeToJson(options);
                        }
                        catch (Exception ex)
                        {
                            this.Logger.LogWarning(ex, "[ConfigProviderBase] Failed to serialize default configuration for type {ConfigType}", configType.Name);
                        }
                    }

                    Atomic.Write(filePath, fileContent);

                    this.Logger.LogDebug("[ConfigProviderBase] Created configuration file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    throw new IOException($"[ConfigProviderBase] Failed to create the required configuration file at '{filePath}'.", ex);
                }
            }
        }

        protected virtual string ResolveLocalFilePath(string? overridePath, string root, Type configType, string suffix)
        {
            ArgumentNullException.ThrowIfNull(root);
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(suffix);

            if (string.IsNullOrWhiteSpace(overridePath))
                return Path.Combine(root, $"{configType.Name}.{suffix}");

            bool isRootedPath = Path.IsPathRooted(overridePath);

            if (isRootedPath)
                return overridePath;
            else
                return Path.Combine(root, overridePath);
        }

        protected virtual async Task SaveSettingsToFileAsync(string filePath, Dictionary<string, object?> settings, JsonSerializerOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(options);

            string serializedContent;

            try
            {
                serializedContent = await Task.Run(() => JsonSerializer.Serialize(settings, options)).ConfigureAwait(false);
            }
            catch (NotSupportedException ex)
            {
                throw new InvalidOperationException("[ConfigProviderBase] Failed to serialize the provided settings object.", ex);
            }

            try
            {
                await Atomic.WriteAsync(filePath, serializedContent, cancellationToken: cancellationToken).ConfigureAwait(false);

                this.Logger.LogDebug("[ConfigProviderBase] Saved configuration to file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "[ConfigProviderBase] Failed to save settings to file: {FilePath}", filePath);

                throw;
            }
        }

        #endregion

        #region BaseConfigProvider: Resource Management

        public virtual void Dispose()
        {
            if (this.IsDisposed)
                return;

            try
            {
                List<ReaderWriterLockSlim> locks = this.AcquireAllConfigLocks();

                try
                {
                    this.DisposeReloadTimers();
                    this.DisposeConfigurationObjects();

                    this.ReloadTimers.Clear();
                    this.Sources.Clear();

                    this.IsDisposed = true;
                }
                finally
                {
                    this.ReleaseAllConfigLocks(locks);
                    this.DisposeConfigLocks();
                }
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "[ConfigProviderBase] Error during BaseConfigProvider disposal");
            }

            GC.SuppressFinalize(this);

            this.Logger.LogDebug("[ConfigProviderBase] BaseConfigProvider disposed");
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
                    this.Logger.LogWarning(ex, "[ConfigProviderBase] Failed to acquire write lock during disposal");
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
                    this.Logger.LogWarning(ex, "[ConfigProviderBase] Error disposing reload timer");
                }
            }
        }
        private void DisposeConfigurationObjects()
        {
            foreach (object? configObject in this.Sources.Values)
            {
                if (configObject is ConfigSource source)
                {
                    try
                    {
                        source.OnChanged?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogWarning(ex, "[ConfigProviderBase] Error disposing configuration change tokens");
                    }
                }
            }
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
                    this.Logger.LogWarning(ex, "[ConfigProviderBase] Failed to release write lock during disposal");
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
                    this.Logger.LogWarning(ex, "[ConfigProviderBase] Error disposing configuration lock");
                }
            }
        }

        #endregion

        #region BaseConfigProvider: Validation and Helpers

        [return: MaybeNull]
        protected virtual T CastStoredValue<T>(object? raw)
        {
            if (raw is null)
                return default;

            bool isCorrectType = raw is T;

            if (isCorrectType)
                return (T)raw;

            try
            {
                bool isConvertible = raw is IConvertible;

                if (isConvertible)
                    return (T)Convert.ChangeType(raw, typeof(T));
            }
            catch (InvalidCastException ex)
            {
                this.Logger.LogWarning(ex, "[ConfigProviderBase] Invalid cast when converting stored value");
            }
            catch (FormatException ex)
            {
                this.Logger.LogWarning(ex, "[ConfigProviderBase] Format error when converting stored value");
            }
            catch (OverflowException ex)
            {
                this.Logger.LogWarning(ex, "[ConfigProviderBase] Overflow error when converting stored value");
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