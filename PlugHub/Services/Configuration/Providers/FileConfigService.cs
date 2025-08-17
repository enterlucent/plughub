using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Utility;
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
    public class FileConfigServiceConfig(
        IConfigService configService,
        string configPath,
        IConfiguration config,
        Dictionary<string, object?> values,
        JsonSerializerOptions? jsonOptions,
        Token ownerToken,
        Token readToken,
        Token writeToken,
        bool reloadOnChanged)
    {
        public IConfigService ConfigService { get; init; } = configService ?? throw new ArgumentNullException(nameof(configService));
        public string ConfigPath { get; init; } = configPath ?? throw new ArgumentNullException(nameof(configPath));
        public IConfiguration Config { get; init; } = config ?? throw new ArgumentNullException(nameof(config));

        public Token Owner { get; init; } = ownerToken;
        public Token Read { get; init; } = readToken;
        public Token Write { get; init; } = writeToken;

        public bool ReloadOnChanged { get; init; } = reloadOnChanged;
        public JsonSerializerOptions JsonSerializerOptions { get; init; } = jsonOptions ?? new JsonSerializerOptions();
        public IDisposable? OnChanged { get; set; } = null;
        public Dictionary<string, object?> Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public class FileConfigServiceSetting(Type? valueType, object? defaultValue, bool readValue, bool writeValue)
    {
        public Type? ValueType { get; init; } = valueType;
        public object? DefaultValue { get; set; } = defaultValue;

        public bool ReadAccess { get; init; } = readValue;
        public bool WriteAccess { get; init; } = writeValue;
    }

    public class FileConfigService : ConfigServiceBase, IConfigServiceProvider, IDisposable
    {
        public FileConfigService(ILogger<IConfigServiceProvider> logger, ITokenService tokenService)
            : base(logger, tokenService)
        {
            this.SupportedParamsTypes = [typeof(FileConfigServiceParams)];
            this.RequiredAccessorInterface = typeof(IFileConfigAccessor);

            this.Logger.LogDebug("FileConfigService initialized");
        }

        #region FileConfigService: Registration

        public override void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configParams);
            ArgumentNullException.ThrowIfNull(configService);

            if (configParams is not FileConfigServiceParams)
            {
                throw new ArgumentException($"Expected FileConfigServiceParams, got {configParams.GetType().Name}", nameof(configParams));
            }

            FileConfigServiceParams p = (FileConfigServiceParams)configParams;

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

                FileConfigServiceConfig typeConfig = this.CreateConfigurationObject(
                    configType, p, configService, owner, read, write);

                this.SetupChangeNotifications(typeConfig, configType);
                this.Configs[configType] = typeConfig;

                this.Logger.LogDebug("Registered configuration: {ConfigType}", configType.Name);
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
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        null,
                        TokenValidationType.Owner);

                config.OnChanged?.Dispose();

                this.Configs.TryRemove(configType, out object? _);

                this.Logger.LogDebug("Unregistered configuration: {ConfigType}", configType.Name);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            this.ConfigLock.TryRemove(configType, out ReaderWriterLockSlim? configlock);
            configlock?.Dispose();
        }

        #endregion

        #region FileConfigService: Value Operations

        public override T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        nRead,
                        TokenValidationType.Read);

                bool settingExists = config.Values.TryGetValue(key, out object? rawSetting);
                bool isCorrectSettingType = settingExists && rawSetting is FileConfigServiceSetting;

                if (!isCorrectSettingType)
                {
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");
                }

                FileConfigServiceSetting setting = (FileConfigServiceSetting)rawSetting!;

                try
                {
                    T? result = this.CastStoredValue<T>(setting.DefaultValue);

                    return result!;
                }
                catch (InvalidCastException)
                {
                    return default!;
                }
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }

        public override T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => this.GetDefault<T>(configType, key, ownerToken, readToken);

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
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        nWrite,
                        TokenValidationType.Write);

                bool settingExists = config.Values.TryGetValue(key, out object? rawSetting);
                bool isCorrectSettingType = settingExists && rawSetting is FileConfigServiceSetting;

                if (!isCorrectSettingType)
                {
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");
                }

                FileConfigServiceSetting setting = (FileConfigServiceSetting)rawSetting!;

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
                catch { /* nothing to see here */ }

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
            => this.SetDefault(configType, key, newValue, ownerToken, writeToken);

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
                    FileConfigServiceConfig config =
                        this.GetRegisteredConfigWithTokenValidation(
                            configType,
                            nOwner,
                            nWrite,
                            TokenValidationType.Write);

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
            string defaultPath;
            JsonSerializerOptions jsonOpts;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        nWrite,
                        TokenValidationType.Write);

                defaultSettings = config.Values
                    .Select(kvp => (kvp.Key, setting: kvp.Value as FileConfigServiceSetting))
                    .Where(t => t.setting is not null)
                    .ToDictionary(
                        t => t.Key,
                        t => t.setting!.DefaultValue);

                defaultPath = config.ConfigPath;
                jsonOpts = config.JsonSerializerOptions;
            }
            finally
            {
                if (rw.IsWriteLockHeld)
                {
                    rw.ExitWriteLock();
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await this.SaveSettingsToFileAsync(defaultPath, defaultSettings, jsonOpts, cancellationToken).ConfigureAwait(false);
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
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        nRead,
                        TokenValidationType.Read);

                object instance = Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");

                this.PopulateInstanceProperties(instance, config, configType);

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
                    FileConfigServiceConfig config =
                        this.GetRegisteredConfigWithTokenValidation(
                            configType,
                            nOwner,
                            nWrite,
                            TokenValidationType.Write);

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
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        nWrite,
                        TokenValidationType.Write);

                this.UpdateConfigurationFromInstance(updatedConfig, config, configType);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            await this.SaveSettingsAsync(configType, nOwner, nWrite, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region FileConfigService: Default Configuration Operations

        public override string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            string filePath;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);

            rwLock.EnterReadLock();

            try
            {
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        null,
                        TokenValidationType.Owner);

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
                    FileConfigServiceConfig config =
                        this.GetRegisteredConfigWithTokenValidation(
                            configType,
                            nOwner,
                            null,
                            TokenValidationType.Owner);

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
                FileConfigServiceConfig config =
                    this.GetRegisteredConfigWithTokenValidation(
                        configType,
                        nOwner,
                        null,
                        TokenValidationType.Owner);

                settingPath = config.ConfigPath;
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            ValidateJsonContent(contents);

            cancellationToken.ThrowIfCancellationRequested();

            await Atomic.WriteAsync(settingPath, contents, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region FileConfigService: Configuration Retrieval Helpers

        private enum TokenValidationType
        {
            Owner,
            Read,
            Write
        }

        private FileConfigServiceConfig GetRegisteredConfig(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            bool configExists = this.Configs.TryGetValue(configType, out object? raw);
            bool isCorrectType = configExists && raw is FileConfigServiceConfig;

            if (!isCorrectType)
            {
                this.Logger.LogError("Configuration not found or invalid type for {ConfigType}", configType.Name);
                throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");
            }

            return (FileConfigServiceConfig)raw!;
        }

        private FileConfigServiceConfig GetRegisteredConfigWithTokenValidation(Type configType, Token ownerToken, Token? accessToken, TokenValidationType validationType)
        {
            FileConfigServiceConfig config = this.GetRegisteredConfig(configType);

            Token? validationAccessToken = validationType switch
            {
                TokenValidationType.Owner => null,
                TokenValidationType.Read => config.Read,
                TokenValidationType.Write => config.Write,
                _ => throw new ArgumentException($"Unknown validation type: {validationType}", nameof(validationType))
            };

            this.TokenService.AllowAccess(config.Owner, validationAccessToken, ownerToken, accessToken);

            return config;
        }

        #endregion

        #region FileConfigService: Configuration Management

        private FileConfigServiceConfig CreateConfigurationObject(Type configType, FileConfigServiceParams p, IConfigService configService, Token owner, Token read, Token write)
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
            Dictionary<string, object?> values = this.BuildSettings(configType, defaultConfig);

            return new FileConfigServiceConfig(
                configService,
                defaultConfigFilePath,
                defaultConfig,
                values,
                jsonOptions,
                owner,
                read,
                write,
                reloadOnChange);
        }

        private void SetupChangeNotifications(FileConfigServiceConfig typeConfig, Type configType)
        {
            bool shouldSetupNotifications = typeConfig.ReloadOnChanged;

            if (shouldSetupNotifications)
            {
                typeConfig.OnChanged = typeConfig.Config
                    .GetReloadToken()
                    .RegisterChangeCallback(this.OnConfigHasChanged, configType);
            }
        }

        private void PopulateInstanceProperties(object instance, FileConfigServiceConfig config, Type configType)
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
                bool isCorrectSettingType = hasSettingValue && rawSetting is FileConfigServiceSetting;

                if (isCorrectSettingType)
                {
                    FileConfigServiceSetting setting = (FileConfigServiceSetting)rawSetting!;

                    this.SetPropertyValue(instance, prop, setting.DefaultValue, configType);
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

        private void UpdateConfigurationFromInstance(object updatedConfig, FileConfigServiceConfig config, Type configType)
        {
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                string key = prop.Name;
                object? newValue = prop.GetValue(updatedConfig);

                bool settingExists = config.Values.TryGetValue(key, out object? rawSetting);
                bool isCorrectType = settingExists && rawSetting is FileConfigServiceSetting;

                if (!isCorrectType)
                {
                    throw new KeyNotFoundException($"Property '{key}' not found in settings.");
                }

                FileConfigServiceSetting setting = (FileConfigServiceSetting)rawSetting!;

                try
                {
                    setting.DefaultValue = newValue;
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Comparison fallback failed for key '{Key}' on type {ConfigType}. Value: {Value}", key, configType.Name, newValue);
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
                throw new UnauthorizedAccessException("Provided content failed to parse as JSON. Please check your contents.", ex);
            }
        }

        #endregion

        #region FileConfigService: Resource Management

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
                this.DisposeConfigurationObjects();
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

        private void DisposeConfigurationObjects()
        {
            foreach (FileConfigServiceConfig config in this.Configs.Values.Cast<FileConfigServiceConfig>())
            {
                config.OnChanged?.Dispose();
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

        #region FileConfigService: Configuration Change Handling

        protected override void HandleConfigHasChanged(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            FileConfigServiceConfig? config = null;
            IConfigService? configService = null;
            bool found = false;
            bool reloaded = false;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                try
                {
                    config = this.GetRegisteredConfig(configType);
                    configService = config.ConfigService;

                    Dictionary<string, object?> newSettings = this.BuildSettings(configType, config.Config);
                    config.Values = newSettings;
                    found = true;

                    bool shouldRebindEvents = config.ReloadOnChanged;

                    if (shouldRebindEvents)
                    {
                        config.OnChanged?.Dispose();

                        config.OnChanged = config.Config
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
                this.Logger.LogWarning(ex, "Config directory missing – Type {Type}", configType.Name);
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Config reload failed – Type {Type}", configType.Name);
            }
            finally { if (rw.IsWriteLockHeld) rw.ExitWriteLock(); }

            if (configService != null)
            {
                if (reloaded)
                    configService.OnConfigReloaded(this, configType);
            }

            if (!found)
            {
                this.Logger.LogWarning("Unregistered config type – Type {Type}", configType.Name);
            }
        }

        protected override Dictionary<string, object?> BuildSettings(Type configType, params IConfiguration[] configSources)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configSources);

            Dictionary<string, object?> settings = [];
            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                string key = prop.Name;
                object? defaultValue = null;

                foreach (IConfiguration cfg in configSources)
                {
                    object? val = ExtractConfigurationValue(cfg, prop);

                    if (val != null)
                    {
                        defaultValue = val;
                    }
                }

                FileConfigServiceSetting setting = new(
                    valueType: prop.PropertyType,
                    defaultValue: defaultValue,
                    readValue: prop.CanRead,
                    writeValue: prop.CanWrite);

                settings[key] = setting;
            }

            return settings;
        }

        private static object? ExtractConfigurationValue(IConfiguration cfg, PropertyInfo prop)
        {
            IConfigurationSection section = cfg.GetSection(prop.Name);

            bool sectionExists = section.Exists();

            if (!sectionExists)
            {
                return null;
            }

            bool isGenericType = prop.PropertyType.IsGenericType;
            bool isEnumerable = typeof(IEnumerable).IsAssignableFrom(prop.PropertyType);
            bool isListType = isGenericType && isEnumerable && prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>);

            if (isListType)
            {
                return CreateListFromConfiguration(section, prop.PropertyType);
            }
            else
            {
                return ConvertSectionValue(section, prop.PropertyType);
            }
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