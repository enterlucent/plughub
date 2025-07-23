using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace PlugHub.Services.Configuration
{
    public class UserConfigServiceConfig(IConfigService configService, string configPath, string userConfigPath, IConfiguration config, IConfiguration userConfig, Dictionary<string, object?> values, JsonSerializerOptions? jsonOptions, Token ownerToken, Token readToken, Token writeToken, bool reloadOnChange)
        : FileConfigServiceConfig(configService, configPath, config, values, jsonOptions, ownerToken, readToken, writeToken, reloadOnChange)
    {
        public string UserConfigPath { get; init; } = userConfigPath ?? throw new ArgumentNullException(nameof(userConfigPath));
        public IConfiguration UserConfig { get; init; } = userConfig ?? throw new ArgumentNullException(nameof(userConfig));
        public IDisposable? UserOnChanged { get; set; } = null;
    }

    public class UserConfigServiceSetting(Type? valueType, object? value, object? userValue, bool readAccess, bool writeAccess)
        : FileConfigServiceSetting(valueType, value, readAccess, writeAccess)
    {
        public object? UserValue { get; set; } = userValue;
    }

    public class UserFileConfigService : ConfigServiceBase, IConfigServiceProvider, IDisposable
    {
        public UserFileConfigService(ILogger<IConfigServiceProvider> logger, ITokenService tokenService)
            : base(logger, tokenService)
        {
            this.SupportedParamsTypes = [typeof(UserConfigServiceParams)];
            this.RequiredAccessorInterface = typeof(IFileConfigAccessor);

            this.Logger.LogDebug("UserFileConfigService initialized");
        }

        #region UserFileConfigService: Registration

        public override void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configParams);
            ArgumentNullException.ThrowIfNull(configService);

            if (configParams is not UserConfigServiceParams)
            {
                throw new ArgumentException($"Expected UserConfigServiceParams, got {configParams.GetType().Name}", nameof(configParams));
            }

            UserConfigServiceParams p = (UserConfigServiceParams)configParams;

            (Token owner, Token read, Token write) = this.TokenService.CreateTokenSet(p.Owner, p.Read, p.Write);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterWriteLock();

            try
            {
                bool alreadyRegistered = this.Configs.ContainsKey(configType);

                if (alreadyRegistered)
                {
                    throw new InvalidOperationException($"Configuration for {configType.Name} is already registered.");
                }

                UserConfigServiceConfig typeConfig = this.CreateUserConfigurationObject(
                    configType, p, configService, owner, read, write);

                this.SetupUserChangeNotifications(typeConfig, configType);
                this.Configs[configType] = typeConfig;

                this.Logger.LogDebug("Registered user configuration: {ConfigType}", configType.Name);
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
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                DisposeUserChangeNotifications(config);
                this.Configs.TryRemove(configType, out object? _);

                this.Logger.LogDebug("Unregistered user configuration: {ConfigType}", configType.Name);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            this.ConfigLock.TryRemove(configType, out ReaderWriterLockSlim? configlock);
            configlock?.Dispose();
        }

        #endregion

        #region UserFileConfigService: Value Operations

        public override T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                UserConfigServiceSetting setting = GetUserConfigSetting(config, key, configType);

                T? result = this.CastStoredValue<T>(setting.DefaultValue);

                return result ?? throw new InvalidOperationException($"Default value for '{key}' was null");
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public override T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                UserConfigServiceSetting setting = GetUserConfigSetting(config, key, configType);

                bool hasReadAccess = setting.ReadAccess;

                if (!hasReadAccess)
                {
                    throw new UnauthorizedAccessException($"Read access denied for '{key}'");
                }

                object? value = setting.UserValue ?? setting.DefaultValue;
                T? result = this.CastStoredValue<T>(value);

                return result ?? throw new InvalidOperationException($"Setting value for '{key}' was null");
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }

        public override void SetDefault<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
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
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                UserConfigServiceSetting setting = GetUserConfigSetting(config, key, configType);

                bool hasWriteAccess = setting.WriteAccess;

                if (!hasWriteAccess)
                {
                    throw new UnauthorizedAccessException($"Write access denied for '{key}'");
                }

                object? oldValue = setting.DefaultValue;

                try
                {
                    setting.DefaultValue = (T)Convert.ChangeType(newValue, typeof(T))!;
                }
                catch { /* nothing to see here */}

                args = new ConfigServiceSettingChangeEventArgs(
                    configType: configType,
                    key: key,
                    oldValue: oldValue,
                    newValue: newValue);

                configService = config.ConfigService;
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            configService.OnSettingChanged(this, args.ConfigType, args.Key, args.OldValue, args.NewValue);
        }
        public override void SetSetting<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;
            ConfigServiceSettingChangeEventArgs? changedArgs = null;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterWriteLock();

            try
            {
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                UserConfigServiceSetting setting = GetUserConfigSetting(config, key, configType);

                bool hasWriteAccess = setting.WriteAccess;

                if (!hasWriteAccess)
                {
                    throw new UnauthorizedAccessException($"Write access denied for '{key}'");
                }

                configService = config.ConfigService;
                object? oldValue = setting.UserValue ?? setting.DefaultValue;

                bool isDefaultValue = IsValueEqualToDefault(newValue, setting);

                if (isDefaultValue)
                {
                    setting.UserValue = null;
                }
                else
                {
                    setting.UserValue = newValue;
                }

                changedArgs = new ConfigServiceSettingChangeEventArgs(
                    configType: configType,
                    key: key,
                    oldValue: oldValue,
                    newValue: newValue);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            if (configService != null && changedArgs != null)
            {
                configService.OnSettingChanged(this, changedArgs.ConfigType, changedArgs.Key, changedArgs.OldValue, changedArgs.NewValue);
            }
        }

        public override void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;
            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                    this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                    configService = config.ConfigService;

                    await this.SaveSettingsAsync(configType, nOwner, nWrite).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveSettings, configType);
                }

                if (configService != null)
                {
                    if (errorArgs == null)
                    {
                        configService.OnSaveOperationComplete(this, configType);
                    }
                    else
                    {
                        configService.OnSaveOperationError(this, errorArgs.Exception, errorArgs.Operation, configType);
                    }
                }
            });
        }
        public override async Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configType);

            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            Dictionary<string, object?> defaultSettings;
            Dictionary<string, object?> userSettings;
            string defaultPath;
            string userPath;
            JsonSerializerOptions jsonOpts;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                defaultSettings = config.Values
                    .Select(kvp => (kvp.Key, setting: kvp.Value as UserConfigServiceSetting))
                    .Where(t => t.setting is not null)
                    .ToDictionary(
                        t => t.Key,
                        t => t.setting!.DefaultValue);

                userSettings = config.Values
                    .Select(kvp => (kvp.Key, Setting: kvp.Value as UserConfigServiceSetting))
                    .Where(x => x.Setting?.UserValue is not null)
                    .ToDictionary(
                        t => t.Key,
                        t => t.Setting!.UserValue);

                defaultPath = config.ConfigPath;
                userPath = config.UserConfigPath;

                jsonOpts = config.JsonSerializerOptions;
            }
            finally { if (rw.IsWriteLockHeld) rw.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await this.SaveSettingsToFileAsync(userPath, userSettings, jsonOpts, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save settings for '{configType.Name}'.", ex);
            }
        }

        #endregion

        #region UserFileConfigService: Instance Operations

        public override object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                object instance = Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");

                this.PopulateUserInstanceProperties(instance, config, configType);

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
                    UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                    this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                    configService = config.ConfigService;

                    await this.SaveConfigInstanceAsync(configType, updatedConfig, nOwner, nWrite).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveConfigInstance, configType);
                }

                if (configService != null)
                {
                    if (errorArgs == null)
                    {
                        configService.OnSaveOperationComplete(this, configType);
                    }
                    else
                    {
                        configService.OnSaveOperationError(this, errorArgs.Exception, errorArgs.Operation, configType);
                    }
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
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                UpdateUserConfigurationFromInstance(updatedConfig, config, configType);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            await this.SaveSettingsAsync(configType, nOwner, nWrite, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region UserFileConfigService: Default Configuration Operations

        public override string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            string filePath;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                filePath = config.ConfigPath;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }

            bool fileExists = File.Exists(filePath);

            if (!fileExists)
            {
                throw new FileNotFoundException($"Default config file not found: {filePath}");
            }

            return File.ReadAllText(filePath);
        }
        public override void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(contents);

            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            IConfigService? configService = null;
            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                    this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                    configService = config.ConfigService;

                    await this.SaveDefaultConfigFileContentsAsync(configType, contents, nOwner).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveDefaultConfigFileContents, configType);
                }

                if (configService != null)
                {
                    if (errorArgs == null)
                    {
                        configService.OnSaveOperationComplete(this, configType);
                    }
                    else
                    {
                        configService.OnSaveOperationError(this, errorArgs.Exception, errorArgs.Operation, configType);
                    }
                }
            });
        }
        public override async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(contents);

            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            string settingPath;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterWriteLock();

            try
            {
                UserConfigServiceConfig config = this.GetRegisteredUserConfig(configType);

                this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                settingPath = config.ConfigPath;
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            ValidateJsonContent(contents);

            cancellationToken.ThrowIfCancellationRequested();

            await Atomic.WriteAsync(settingPath, contents, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region UserFileConfigService: Resource Management

        public override void Dispose()
        {
            if (this.IsDisposed)
            {
                return;
            }

            List<ReaderWriterLockSlim> locks = [.. this.ConfigLock.Values];

            foreach (ReaderWriterLockSlim l in locks)
            {
                l.EnterWriteLock();
            }

            try
            {
                if (this.IsDisposed)
                {
                    return;
                }

                this.DisposeReloadTimers();
                this.DisposeUserConfigurationObjects();
                this.ClearCollections();
                this.IsDisposed = true;
            }
            finally
            {
                foreach (ReaderWriterLockSlim l in locks)
                {
                    if (l.IsWriteLockHeld)
                    {
                        l.ExitWriteLock();
                    }
                }
            }

            this.DisposeConfigLocks();

            GC.SuppressFinalize(this);
        }
        private void DisposeReloadTimers()
        {
            foreach (Timer t in this.ReloadTimers.Values)
            {
                t.Dispose();
            }
            this.ReloadTimers.Clear();
        }
        private void DisposeUserConfigurationObjects()
        {
            foreach (UserConfigServiceConfig config in this.Configs.Values.Cast<UserConfigServiceConfig>())
            {
                DisposeUserChangeNotifications(config);
            }
        }
        private void ClearCollections()
        {
            this.Configs.Clear();
        }
        private void DisposeConfigLocks()
        {
            foreach (ReaderWriterLockSlim l in this.ConfigLock.Values)
            {
                l.Dispose();
            }
        }

        #endregion

        #region UserFileConfigService: Configuration Change Handling

        protected override void HandleConfigHasChanged(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            UserConfigServiceConfig? config;
            IConfigService? configService = null;
            bool found = false;
            bool reloaded = false;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                try
                {
                    config = this.GetRegisteredUserConfig(configType);
                    configService = config.ConfigService;

                    Dictionary<string, object?> newSettings = this.BuildSettings(configType, config.Config, config.UserConfig);
                    config.Values = newSettings;
                    found = true;

                    bool shouldRebindEvents = config.ReloadOnChanged;

                    if (shouldRebindEvents)
                    {
                        DisposeUserChangeNotifications(config);
                        this.SetupUserChangeNotifications(config, configType);
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
                this.Logger.LogWarning(ex, "Config directory missing – Type {Type}", configType.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Config reload failed – Type {Type}", configType.Name);
            }
            finally { if (rw.IsWriteLockHeld) rw.ExitWriteLock(); }

            if (configService != null && reloaded)
            {
                configService.OnConfigReloaded(this, configType);
            }

            if (!found)
            {
                this.Logger.LogWarning("Unregistered config type – Type {Type}", configType.Name);
            }
        }

        #endregion

        #region UserFileConfigService: Configuration Management

        private UserConfigServiceConfig CreateUserConfigurationObject(Type configType, UserConfigServiceParams p, IConfigService configService, Token owner, Token read, Token write)
        {
            JsonSerializerOptions jsonOptions = p.JsonSerializerOptions ?? this.JsonOptions ?? configService.JsonOptions;
            bool reloadOnChange = p.ReloadOnChange;

            string defaultConfigFilePath = this.ResolveLocalFilePath(
                p.ConfigUriOverride,
                configService.ConfigDataDirectory,
                configType,
                "json");

            string userConfigFilePath = this.ResolveLocalFilePath(
                p.UserConfigUriOverride,
                configService.ConfigDataDirectory,
                configType,
                "user.json");

            this.EnsureFileExists(defaultConfigFilePath, jsonOptions, configType);
            this.EnsureFileExists(userConfigFilePath, jsonOptions);

            IConfiguration defaultConfig = this.BuildConfig(defaultConfigFilePath, reloadOnChange);
            IConfiguration userConfig = this.BuildConfig(userConfigFilePath, reloadOnChange);

            Dictionary<string, object?> values = this.BuildSettings(configType, defaultConfig, userConfig);

            return new UserConfigServiceConfig(
                configService,
                defaultConfigFilePath,
                userConfigFilePath,
                defaultConfig,
                userConfig,
                values,
                jsonOptions,
                owner,
                read,
                write,
                reloadOnChange);
        }

        private void SetupUserChangeNotifications(UserConfigServiceConfig typeConfig, Type configType)
        {
            bool shouldSetupNotifications = typeConfig.ReloadOnChanged;

            if (shouldSetupNotifications)
            {
                typeConfig.OnChanged = typeConfig.Config
                    .GetReloadToken()
                    .RegisterChangeCallback(this.OnConfigHasChanged, configType);

                typeConfig.UserOnChanged = typeConfig.UserConfig
                    .GetReloadToken()
                    .RegisterChangeCallback(this.OnConfigHasChanged, configType);
            }
        }
        private static void DisposeUserChangeNotifications(UserConfigServiceConfig config)
        {
            config.OnChanged?.Dispose();
            config.UserOnChanged?.Dispose();
        }

        private UserConfigServiceConfig GetRegisteredUserConfig(Type configType)
        {
            bool configExists = this.Configs.TryGetValue(configType, out object? rawConfig);
            bool isCorrectType = configExists && rawConfig is UserConfigServiceConfig;

            if (!isCorrectType)
            {
                throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");
            }

            return (UserConfigServiceConfig)rawConfig!;
        }
        private static UserConfigServiceSetting GetUserConfigSetting(UserConfigServiceConfig config, string key, Type configType)
        {
            bool settingExists = config.Values.TryGetValue(key, out object? rawSetting);
            bool isCorrectSettingType = settingExists && rawSetting is UserConfigServiceSetting;

            if (!isCorrectSettingType)
            {
                throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");
            }

            return (UserConfigServiceSetting)rawSetting!;
        }
        private static bool IsValueEqualToDefault<T>(T newValue, UserConfigServiceSetting setting)
        {
            if (setting.DefaultValue == null)
            {
                return newValue == null;
            }

            try
            {
                T defaultValue = setting.DefaultValue is T val
                    ? val
                    : (T)Convert.ChangeType(setting.DefaultValue, typeof(T));

                return EqualityComparer<T>.Default.Equals(newValue, defaultValue);
            }
            catch
            {
                return false;
            }
        }

        private void PopulateUserInstanceProperties(object instance, UserConfigServiceConfig config, Type configType)
        {
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                bool canWriteProperty = prop.CanWrite;

                if (!canWriteProperty)
                {
                    continue;
                }

                bool hasSettingValue = config.Values.TryGetValue(prop.Name, out object? rawSetting);
                bool isCorrectSettingType = hasSettingValue && rawSetting is UserConfigServiceSetting;

                if (isCorrectSettingType)
                {
                    UserConfigServiceSetting setting = (UserConfigServiceSetting)rawSetting!;
                    object? value = setting.UserValue ?? setting.DefaultValue;

                    this.SetPropertyValue(instance, prop, value, configType);
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

                    if (canAcceptNull)
                    {
                        prop.SetValue(instance, null);
                    }
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
                this.Logger.LogError(ex, "Failed to set property {PropertyName} for type {ConfigType}", prop.Name, configType.Name);
            }
        }

        private static void UpdateUserConfigurationFromInstance(object updatedConfig, UserConfigServiceConfig config, Type configType)
        {
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                string key = prop.Name;
                object? newValue = prop.GetValue(updatedConfig);

                UserConfigServiceSetting setting = GetUserConfigSetting(config, key, configType);

                bool isDefaultValue = IsValueEqualToDefault(newValue, setting);

                if (isDefaultValue)
                {
                    setting.UserValue = null;
                }
                else
                {
                    setting.UserValue = newValue;
                }
            }
        }
        private static void ValidateJsonContent(string contents)
        {
            try
            {
                JsonDocument.Parse(contents);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException("Provided content was seen as an illegal argument.", ex);
            }
            catch (JsonException ex)
            {
                throw new JsonException("Provided content failed to parse as JSON. Please check your contents.", ex);
            }
        }

        #endregion
    }
}