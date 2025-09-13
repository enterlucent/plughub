using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Models.Configuration.Parameters;
using PlugHub.Shared.Services.Configuration.Providers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Services.Configuration.Providers
{
    public class FileConfigProvider : BaseConfigProvider, IConfigProvider, IDisposable
    {
        public FileConfigProvider(ILogger<IConfigProvider> logger, ITokenService tokenService)
            : base(logger, tokenService)
        {
            this.SupportedParamsTypes = [typeof(ConfigFileParams)];
            this.RequiredAccessorInterface = typeof(IFileConfigAccessor);

            this.Logger.LogDebug("[FileConfigProvider] Initialized");
        }

        #region FileConfigService: Registration

        public override void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configParams);
            ArgumentNullException.ThrowIfNull(configService);

            if (configParams is not ConfigFileParams)
                throw new ArgumentException($"Expected FileConfigServiceParams, got {configParams.GetType().Name}", nameof(configParams));

            ConfigFileParams p = (ConfigFileParams)configParams;

            (Token owner, Token read, Token write) = this.TokenService.CreateTokenSet(p.Owner, p.Read, p.Write);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterWriteLock();

            try
            {
                bool alreadyRegistered = this.Sources.ContainsKey(configType);

                if (alreadyRegistered)
                    throw new InvalidOperationException($"Configuration for {configType.Name} is already registered.");

                ConfigSource source = this.CreateSourceObject(configType, p, configService, owner, read, write);

                bool shouldSetupNotifications = source.ReloadOnChanged;

                if (shouldSetupNotifications)
                {
                    source.OnChanged = source.Configuration
                        .GetReloadToken()
                        .RegisterChangeCallback(this.OnConfigHasChanged, configType);
                }

                this.Sources[configType] = source;
                this.Logger.LogDebug("[FileConfigProvider] Registered configuration: {ConfigType}", configType.Name);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }
        }
        public override void UnregisterConfig(Type configType, Token? token = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(token, null, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterWriteLock();

            try
            {
                ConfigSource source =
                    this.GetSourceWithValidation(
                        configType,
                        nOwner,
                        null,
                        TokenValidationType.Owner);

                source.OnChanged?.Dispose();

                this.Sources.TryRemove(configType, out object? _);

                this.Logger.LogDebug("[FileConfigProvider] Unregistered configuration: {ConfigType}", configType.Name);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            this.ConfigLock.TryRemove(configType, out ReaderWriterLockSlim? configlock);

            configlock?.Dispose();
        }

        #endregion

        #region FileConfigService: Value Operations

        public override T GetValue<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                ConfigSource source =
                    this.GetSourceWithValidation(
                        configType,
                        nOwner,
                        nRead,
                        TokenValidationType.Read);

                bool settingExists = source.Values.TryGetValue(key, out object? rawSetting);
                bool isCorrectSettingType = settingExists && rawSetting is ConfigValue;

                if (!isCorrectSettingType)
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                ConfigValue setting = (ConfigValue)rawSetting!;

                try
                {
                    T? result = this.CastStoredValue<T>(setting.Value);

                    return result!;
                }
                catch (InvalidCastException)
                {
                    return default!;
                }
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public override void SetValue<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService configService;
            ConfigServiceSettingChangeEventArgs? args = null;
            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterWriteLock();

            try
            {
                ConfigSource source =
                    this.GetSourceWithValidation(
                        configType,
                        nOwner,
                        nWrite,
                        TokenValidationType.Write);

                bool settingExists = source.Values.TryGetValue(key, out object? rawSetting);
                bool isCorrectSettingType = settingExists && rawSetting is ConfigValue;

                if (!isCorrectSettingType)
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                ConfigValue setting = (ConfigValue)rawSetting!;

                bool hasWriteAccess = setting.CanWrite;

                if (!hasWriteAccess)
                    throw new UnauthorizedAccessException($"Write access denied for '{key}'");

                object? oldValue = setting.Value;

                try
                {
                    setting.Value = (T)Convert.ChangeType(newValue, typeof(T))!;
                }
                catch { /* nothing to see here */ }

                args = new ConfigServiceSettingChangeEventArgs(
                    configType: configType,
                    key: key,
                    oldValue: oldValue,
                    newValue: newValue);

                configService = source.ConfigService;
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            configService.OnSettingChanged(this, args.ConfigType, args.Key, args.OldValue, args.NewValue);
        }
        public override void SaveValues(Type configType, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;
            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    ConfigSource source =
                        this.GetSourceWithValidation(
                            configType,
                            nOwner,
                            nWrite,
                            TokenValidationType.Write);

                    configService = source.ConfigService;

                    await this.SaveValuesAsync(configType, nOwner, nWrite).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveSettings, configType);
                }

                if (configService != null)
                {
                    if (errorArgs == null)
                        configService.OnSaveOperationComplete(this, configType);
                    else
                        configService.OnSaveOperationError(this, errorArgs.Exception, errorArgs.Operation, configType);
                }
            });
        }
        public override async Task SaveValuesAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configType);

            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            Dictionary<string, object?> configValues;
            string sourceLocation;
            JsonSerializerOptions jsonOpts;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                ConfigSource source =
                    this.GetSourceWithValidation(
                        configType,
                        nOwner,
                        nWrite,
                        TokenValidationType.Write);

                configValues = source.Values
                    .Select(kvp => (kvp.Key, setting: kvp.Value as ConfigValue))
                    .Where(t => t.setting is not null)
                    .ToDictionary(
                        t => t.Key,
                        t => t.setting!.Value);

                sourceLocation = source.SourceLocation;
                jsonOpts = source.JsonSerializerOptions;
            }
            finally
            {
                if (rw.IsWriteLockHeld)
                    rw.ExitWriteLock();
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await this.SaveSettingsToFileAsync(sourceLocation, configValues, jsonOpts, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save settings for '{configType.Name}'.", ex);
            }
        }

        #endregion

        #region FileConfigService: Instance Operations

        public override object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                ConfigSource source =
                    this.GetSourceWithValidation(
                        configType,
                        nOwner,
                        nRead,
                        TokenValidationType.Read);

                object instance = Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");

                this.PopulateInstanceProperties(instance, source, configType);

                return instance;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public override void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(updatedConfig);

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;

            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    ConfigSource source =
                        this.GetSourceWithValidation(
                            configType,
                            nOwner,
                            nWrite,
                            TokenValidationType.Write);

                    configService = source.ConfigService;

                    await this.SaveConfigInstanceAsync(configType, updatedConfig, nOwner, nWrite).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveConfigInstance, configType);
                }

                if (configService != null)
                {
                    if (errorArgs == null)
                        configService.OnSaveOperationComplete(this, configType);
                    else
                        configService.OnSaveOperationError(this, errorArgs.Exception, errorArgs.Operation, configType);
                }
            });
        }
        public override async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(updatedConfig);

            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterWriteLock();

            try
            {
                ConfigSource source =
                    this.GetSourceWithValidation(
                        configType,
                        nOwner,
                        nWrite,
                        TokenValidationType.Write);

                this.UpdateConfigurationFromInstance(updatedConfig, source, configType);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            await this.SaveValuesAsync(configType, nOwner, nWrite, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region FileConfigService: Configuration Retrieval Helpers

        private enum TokenValidationType
        {
            Owner,
            Read,
            Write
        }
        private ConfigSource GetRegisteredSource(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            bool configExists = this.Sources.TryGetValue(configType, out object? raw);
            bool isCorrectType = configExists && raw is ConfigSource;

            if (!isCorrectType)
            {
                this.Logger.LogError("[FileConfigProvider] Configuration not found or invalid type for {ConfigType}", configType.Name);

                throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");
            }

            return (ConfigSource)raw!;
        }
        private ConfigSource GetSourceWithValidation(Type configType, Token ownerToken, Token? accessToken, TokenValidationType validationType)
        {
            ConfigSource source = this.GetRegisteredSource(configType);

            Token? validationAccessToken = validationType switch
            {
                TokenValidationType.Owner => null,
                TokenValidationType.Read => source.Read,
                TokenValidationType.Write => source.Write,
                _ => throw new ArgumentException($"Unknown validation type: {validationType}", nameof(validationType))
            };

            this.TokenService.AllowAccess(source.Owner, validationAccessToken, ownerToken, accessToken);

            return source;
        }

        #endregion

        #region FileConfigService: Configuration Management

        private ConfigSource CreateSourceObject(Type configType, ConfigFileParams p, IConfigService configService, Token owner, Token read, Token write)
        {
            JsonSerializerOptions jsonOptions = p.JsonSerializerOptions ?? this.JsonOptions ?? configService.JsonOptions;
            bool reloadOnChange = p.ReloadOnChange;

            string defaultConfigFilePath = this.ResolveLocalFilePath(
                p.ConfigUriOverride,
                configService.ConfigDataDirectory,
                configType,
                "json");

            this.EnsureFileExists(defaultConfigFilePath, jsonOptions, configType);

            IConfiguration defaultConfig = this.BuildConfig(defaultConfigFilePath, reloadOnChange);
            Dictionary<string, object?> values = this.BuildSettings(configType, [defaultConfig]);

            return new ConfigSource(
                configService,
                defaultConfigFilePath,
                configType,
                defaultConfig,
                values,
                jsonOptions,
                owner,
                read,
                write,
                reloadOnChange);
        }
        private void PopulateInstanceProperties(object instance, ConfigSource source, Type configType)
        {
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                bool canWriteProperty = prop.CanWrite;

                if (!canWriteProperty)
                    continue;

                bool hasSettingValue = source.Values.TryGetValue(prop.Name, out object? rawSetting);
                bool isCorrectSettingType = hasSettingValue && rawSetting is ConfigValue;

                if (isCorrectSettingType)
                {
                    ConfigValue setting = (ConfigValue)rawSetting!;

                    this.SetPropertyValue(instance, prop, setting.Value, configType);
                }
            }
        }
        private void SetPropertyValue(object instance, PropertyInfo prop, object? value, Type configType)
        {
            try
            {
                if (value == null)
                {
                    bool canAcceptNull = !prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null;
                    bool isCollection = typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && prop.PropertyType != typeof(string);

                    if (canAcceptNull && !isCollection)
                        prop.SetValue(instance, null);

                    return;
                }

                if (prop.PropertyType.IsEnum)
                {
                    bool enumValueIsValid = Enum.IsDefined(prop.PropertyType, value);
                    object enumValue = enumValueIsValid
                        ? Enum.ToObject(prop.PropertyType, value)
                        : Activator.CreateInstance(prop.PropertyType)!;

                    prop.SetValue(instance, enumValue);
                }
                else if (prop.PropertyType.IsInstanceOfType(value))
                {
                    prop.SetValue(instance, value);
                }
                else
                {
                    Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                    object convertedValue = Convert.ChangeType(value, targetType);
                    prop.SetValue(instance, convertedValue);
                }
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                this.Logger.LogError(ex, "[FileConfigProvider] Failed to set property {PropertyName} for type {ConfigType}", prop.Name, configType.Name);
            }
        }
        private void UpdateConfigurationFromInstance(object updatedConfig, ConfigSource source, Type configType)
        {
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                string key = prop.Name;
                object? newValue = prop.GetValue(updatedConfig);

                bool settingExists = source.Values.TryGetValue(key, out object? rawSetting);
                bool isCorrectType = settingExists && rawSetting is ConfigValue;

                if (!isCorrectType)
                    throw new KeyNotFoundException($"Property '{key}' not found in settings.");

                ConfigValue setting = (ConfigValue)rawSetting!;

                try
                {
                    setting.Value = newValue;
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "[FileConfigProvider] Comparison fallback failed for key '{Key}' on type {ConfigType}. Value: {Value}", key, configType.Name, newValue);
                }
            }
        }

        #endregion

        #region FileConfigService: Resource Management

        public override void Dispose()
        {
            if (this.IsDisposed)
                return;

            List<ReaderWriterLockSlim> locks = [.. this.ConfigLock.Values];

            foreach (ReaderWriterLockSlim slimLock in locks)
                slimLock.EnterWriteLock();

            try
            {
                if (this.IsDisposed)
                    return;

                this.DisposeReloadTimers();
                this.DisposeConfigurationObjects();
                this.Sources.Clear();
                this.IsDisposed = true;
            }
            finally
            {
                foreach (ReaderWriterLockSlim slimLock in locks)
                    if (slimLock.IsWriteLockHeld)
                        slimLock.ExitWriteLock();
            }

            this.DisposeConfigLocks();

            GC.SuppressFinalize(this);
        }

        private void DisposeReloadTimers()
        {
            foreach (Timer timer in this.ReloadTimers.Values)
                timer.Dispose();

            this.ReloadTimers.Clear();
        }
        private void DisposeConfigurationObjects()
        {
            foreach (ConfigSource source in this.Sources.Values.Cast<ConfigSource>())
                source.OnChanged?.Dispose();
        }
        private void DisposeConfigLocks()
        {
            foreach (ReaderWriterLockSlim slimLock in this.ConfigLock.Values)
                slimLock.Dispose();
        }

        #endregion

        #region FileConfigService: Configuration Change Handling

        protected override void HandleConfigHasChanged(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            ConfigSource? source = null;
            IConfigService? configService = null;

            bool found = false;
            bool reloaded = false;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                try
                {
                    source = this.GetRegisteredSource(configType);
                    configService = source.ConfigService;

                    Dictionary<string, object?> newSettings = this.BuildSettings(configType, [source.Configuration]);
                    source.Values = newSettings;
                    found = true;

                    bool shouldRebindEvents = source.ReloadOnChanged;

                    if (shouldRebindEvents)
                    {
                        source.OnChanged?.Dispose();

                        source.OnChanged = source.Configuration
                            .GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigHasChanged, configType);
                    }

                    reloaded = true;
                }
                catch (KeyNotFoundException)
                {
                    found = false;
                }
            }
            catch (FileNotFoundException ex)
            {
                this.Logger.LogWarning(ex, "[FileConfigProvider] Config directory missing – Type {Type}", configType.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "[FileConfigProvider] Config reload failed – Type {Type}", configType.Name);
            }
            finally { if (rw.IsWriteLockHeld) rw.ExitWriteLock(); }

            if (configService != null)
            {
                if (reloaded)
                    configService.OnConfigReloaded(this, configType);
            }

            if (!found)
            {
                this.Logger.LogWarning("[FileConfigProvider] Unregistered config type – Type {Type}", configType.Name);
            }
        }

        protected override Dictionary<string, object?> BuildSettings(Type configType, params IConfiguration[] configurations)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configurations);

            Dictionary<string, object?> settings = [];
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                string key = prop.Name;
                object? value = null;

                foreach (IConfiguration configuration in configurations)
                {
                    object? v = ExtractConfigurationValue(configuration, prop);

                    if (v != null) value = v;
                }

                ConfigValue configValue = new()
                {
                    ValueType = prop.PropertyType,
                    Value = value,
                    CanRead = prop.CanRead,
                    CanWrite = prop.CanWrite
                };

                settings[key] = configValue;
            }

            return settings;
        }
        private static object? ExtractConfigurationValue(IConfiguration cfg, PropertyInfo prop)
        {
            IConfigurationSection section = cfg.GetSection(prop.Name);

            bool sectionExists = section.Exists();

            if (!sectionExists)
                return null;

            bool isGenericType = prop.PropertyType.IsGenericType;
            bool isEnumerable = typeof(IEnumerable).IsAssignableFrom(prop.PropertyType);

            if (isGenericType && isEnumerable && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                return CreateListFromConfiguration(section, prop.PropertyType);
            else
                return ConvertSectionValue(section, prop.PropertyType);
        }
        private static object? CreateListFromConfiguration(IConfigurationSection section, Type propertyType)
        {
            Type elementType = propertyType.GetGenericArguments()[0];
            Type listType = typeof(List<>).MakeGenericType(elementType);
            IList list = (IList)Activator.CreateInstance(listType)!;

            foreach (IConfigurationSection child in section.GetChildren())
            {
                object? element = Activator.CreateInstance(elementType);
                child.Bind(element);
                list.Add(element);
            }

            return list;
        }
        private static object? ConvertSectionValue(IConfigurationSection section, Type propertyType)
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
                catch
                {
                    return null;
                }
            }
        }

        #endregion
    }
}