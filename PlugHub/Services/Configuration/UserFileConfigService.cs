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
        public string UserConfigPath { get; init; } = userConfigPath;
        public IConfiguration UserConfig { get; init; } = userConfig;
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
        }

        #region UserConfigService: Registration

        public override void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService)
        {
            if (configParams is not UserConfigServiceParams p)
                throw new ArgumentException($"Expected UserConfigServiceParams, got {configParams.GetType().Name}", nameof(configParams));

            (Token owner, Token read, Token write) = this.TokenService.CreateTokenSet(p.Owner, p.Read, p.Write);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (this.Configs.ContainsKey(configType))
                    throw new InvalidOperationException($"Configuration for {configType.Name} is already registered.");

                JsonSerializerOptions jsonOptions = p.JsonSerializerOptions ?? this.JsonOptions ?? configService.JsonOptions;

                bool reloadOnChange = p.ReloadOnChange;

                string defaultConfigFilePath =
                    this.ResolveLocalFilePath(p.ConfigUriOverride, configService.ConfigDataDirectory, configType, "json");
                string userConfigFilePath =
                    this.ResolveLocalFilePath(p.UserConfigUriOverride, configService.ConfigDataDirectory, configType, "user.json");

                this.EnsureFileExists(defaultConfigFilePath, jsonOptions, configType);
                this.EnsureFileExists(userConfigFilePath, jsonOptions);

                IConfiguration defaultConfig =
                    this.BuildConfig(defaultConfigFilePath, reloadOnChange);
                IConfiguration userConfig =
                    this.BuildConfig(userConfigFilePath, reloadOnChange);

                Dictionary<string, object?> values = this.BuildSettings(configType, defaultConfig, userConfig);

                UserConfigServiceConfig typeConfig = new(
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

                if (reloadOnChange)
                {
                    typeConfig.OnChanged =
                        defaultConfig.GetReloadToken()
                                     .RegisterChangeCallback(this.OnConfigHasChanged, configType);

                    typeConfig.UserOnChanged =
                        userConfig.GetReloadToken()
                                  .RegisterChangeCallback(this.OnConfigHasChanged, configType);
                }

                this.Configs[configType] = typeConfig;
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }
        }
        public override void UnregisterConfig(Type configType, Token? token = null)
        {
            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(token, null, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                config.OnChanged?.Dispose();
                config.UserOnChanged?.Dispose();

                this.Configs.TryRemove(configType, out object? _);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            this.ConfigLock.TryRemove(configType, out ReaderWriterLockSlim? configlock);

            configlock?.Dispose();
        }

        #endregion

        #region UserConfigService: Value Accessors and Mutators

        public override T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? rawConfig) || rawConfig is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                if (!config.Values.TryGetValue(key, out object? rawSetting) || rawSetting is not UserConfigServiceSetting setting)
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                T? result = this.CastStoredValue<T>(setting.DefaultValue);

                return result == null ? throw new InvalidOperationException($"Default value for '{key}' was null") : result;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public override T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                if (!config.Values.TryGetValue(key, out object? rawSetting) || rawSetting is not UserConfigServiceSetting setting)
                    throw new KeyNotFoundException(
                        $"Setting '{key}' not found in {configType.Name}.");

                if (!setting.ReadAccess)
                    throw new UnauthorizedAccessException($"Read access denied for '{key}'");

                object? value = setting.UserValue ?? setting.DefaultValue;

                T? result = this.CastStoredValue<T>(value);

                return result == null ? throw new InvalidOperationException($"Default value for '{key}' was null") : result;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }

        public override void SetDefault<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService configService;
            ConfigServiceSettingChangeEventArgs? args = null;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                if (!config.Values.TryGetValue(key, out object? rawSetting) || rawSetting is not FileConfigServiceSetting setting)
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                if (!setting.WriteAccess)
                    throw new UnauthorizedAccessException($"Write access denied for '{key}'");

                object? oldValue = setting.DefaultValue;

                try
                {
                    if (setting.DefaultValue != null)
                    {
                        setting.DefaultValue = (T)Convert.ChangeType(setting.DefaultValue, typeof(T));
                    }
                }
                catch { /* Ignore conversion errors */ }


                args = new ConfigServiceSettingChangeEventArgs(
                    configType: configType,
                    key: key,
                    oldValue: oldValue,
                    newValue: newValue);

                configService = config.ConfigService;

            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            configService.OnSettingChanged(this, args.ConfigType, args.Key, args.OldValue, args.OldValue);
        }
        public override void SetSetting<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;
            ConfigServiceSettingChangeEventArgs? changedArgs = null;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                if (!config.Values.TryGetValue(key, out object? rawSetting) || rawSetting is not UserConfigServiceSetting setting)
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                if (!setting.WriteAccess)
                    throw new UnauthorizedAccessException($"Write access denied for '{key}'");

                configService = config.ConfigService;

                object? oldValue = setting.UserValue ?? setting.DefaultValue;

                bool isDefaultValue = false;

                try
                {
                    if (setting.DefaultValue != null)
                    {
                        T defaultValue = setting.DefaultValue is T val ? val : (T)Convert.ChangeType(setting.DefaultValue, typeof(T));

                        isDefaultValue = EqualityComparer<T>.Default.Equals(newValue, defaultValue);
                    }
                }
                catch { /* Ignore conversion errors */ }


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
                    newValue: newValue
                );
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            if (configService != null && changedArgs != null)
                configService.OnSettingChanged(this, changedArgs.ConfigType, changedArgs.Key, changedArgs.OldValue, changedArgs.NewValue);
        }

        public override void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;
            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                        throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                    this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                    configService = config.ConfigService;

                    await this.SaveSettingsAsync(configType, nOwner, nWrite);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveSettings);
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
        public override async Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
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
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

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

                jsonOpts = config.JsonSerializerOptions ?? this.JsonOptions;
            }
            finally { if (rw.IsWriteLockHeld) rw.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await this.SaveSettingsToFileAsync(defaultPath, defaultSettings, jsonOpts, cancellationToken)
                      .ConfigureAwait(false);

                await this.SaveSettingsToFileAsync(userPath, userSettings, jsonOpts, cancellationToken)
                      .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save settings for '{configType.Name}'.", ex);
            }
        }

        #endregion

        #region UserConfigService: Instance Accesors and Mutators

        public override object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                object instance = Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");

                foreach (PropertyInfo prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite) continue;

                    if (config.Values.TryGetValue(prop.Name, out object? rawSetting) && rawSetting is UserConfigServiceSetting setting)
                    {
                        object? value = setting.UserValue ?? setting.DefaultValue;

                        try
                        {
                            if (value == null)
                            {
                                if (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null)
                                    prop.SetValue(instance, null);
                                continue;
                            }

                            if (prop.PropertyType.IsEnum)
                            {
                                prop.SetValue(instance, Enum.IsDefined(prop.PropertyType, value)
                                    ? Enum.ToObject(prop.PropertyType, value)
                                    : Activator.CreateInstance(prop.PropertyType));
                            }
                            else if (prop.PropertyType.IsInstanceOfType(value))
                            {
                                prop.SetValue(instance, value);
                            }
                            else
                            {
                                prop.SetValue(instance,
                                    Convert.ChangeType(value, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType));
                            }
                        }
                        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                        {
                            this.Logger.LogError(ex,
                                "Failed to set property {PropertyName} for type {ConfigType}",
                                prop.Name, configType.Name);
                        }
                    }
                }

                return instance;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public override void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;
            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                        throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                    this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                    configService = config.ConfigService;

                    await this.SaveConfigInstanceAsync(configType, updatedConfig, nOwner, nWrite);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveConfigInstance);
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
            ArgumentNullException.ThrowIfNull(updatedConfig);

            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                foreach (PropertyInfo prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    string key = prop.Name;
                    object? newValue = prop.GetValue(updatedConfig);

                    if (!config.Values.TryGetValue(key, out object? rawSetting) || rawSetting is not UserConfigServiceSetting setting)
                        throw new KeyNotFoundException($"Property '{key}' not found in settings.");

                    bool isDefaultValue = false;
                    try
                    {
                        if (setting.DefaultValue != null)
                        {
                            isDefaultValue = Equals(newValue, setting.DefaultValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogWarning(ex, "Comparison fallback failed for key '{Key}' on type {ConfigType}. Value: {Value}", key, configType.Name, newValue);
                    }

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
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            await this.SaveSettingsAsync(configType, nOwner, nWrite, cancellationToken);
        }

        #endregion

        #region UserConfigService: Default Config Mutation/Migration

        public override string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
        {
            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            string filePath;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                filePath = config.ConfigPath;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Default config file not found: {filePath}");
            }

            return File.ReadAllText(filePath);
        }
        public override void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null)
        {
            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            IConfigService? configService = null;
            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                        throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                    this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                    configService = config.ConfigService;

                    await this.SaveDefaultConfigFileContentsAsync(configType, contents, nOwner);
                }
                catch (Exception ex)
                {
                    errorArgs = new ConfigServiceSaveErrorEventArgs(ex, ConfigSaveOperation.SaveDefaultConfigFileContents);
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
        public override async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            string settingPath;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not UserConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                settingPath = config.ConfigPath;
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            try
            {
                JsonDocument.Parse(contents);
            }
            catch (ArgumentException ex)
            {
                throw new ArgumentException($"Provided content was seen as an illegal argument.", ex);
            }
            catch (JsonException ex)
            {
                throw new UnauthorizedAccessException($"Provided content failed to parse as JSON. Please check your contents.", ex);
            }

            cancellationToken.ThrowIfCancellationRequested();

            await Atomic.WriteAsync(settingPath, contents, cancellationToken: cancellationToken);
        }

        #endregion

        #region UserConfigService: Utilities

        public override void Dispose()
        {
            if (this.IsDisposed) return;

            List<ReaderWriterLockSlim> locks = [.. this.ConfigLock.Values];

            foreach (ReaderWriterLockSlim l in locks) l.EnterWriteLock();

            try
            {
                if (this.IsDisposed) return;

                foreach (Timer t in this.ReloadTimers.Values)
                    t.Dispose();
                this.ReloadTimers.Clear();

                foreach (UserConfigServiceConfig config in this.Configs.Values.Cast<UserConfigServiceConfig>())
                {
                    config.OnChanged?.Dispose();
                    config.UserOnChanged?.Dispose();
                }

                this.Configs.Clear();
                this.IsDisposed = true;
            }
            finally { foreach (ReaderWriterLockSlim l in locks) if (l.IsWriteLockHeld) l.ExitWriteLock(); }

            foreach (ReaderWriterLockSlim l in this.ConfigLock.Values)
                l.Dispose();

            GC.SuppressFinalize(this);
        }
        protected override void HandleConfigHasChanged(Type configType)
        {
            UserConfigServiceConfig? config = null;

            IConfigService? configService = null;

            bool found = false;
            bool reloaded = false;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                if (this.Configs.TryGetValue(configType, out object? raw) && raw is UserConfigServiceConfig cooked)
                {
                    config = cooked;
                    configService = cooked.ConfigService;

                    Dictionary<string, object?> newSettings =
                        this.BuildSettings(configType, config.Config, config.UserConfig);

                    config.Values = newSettings;

                    found = true;

                    if (config.ReloadOnChanged)
                    {
                        config.OnChanged?.Dispose();
                        config.UserOnChanged?.Dispose();

                        config.OnChanged = config.Config
                            .GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigHasChanged, configType);

                        config.UserOnChanged = config.UserConfig
                            .GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigHasChanged, configType);
                    }

                    reloaded = true;
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
                if (rw.IsWriteLockHeld) rw.ExitWriteLock();
            }

            if (configService != null && reloaded)
                configService.OnConfigReloaded(this, configType);

            if (!found)
                this.Logger.LogWarning("Unregistered config type – Type {Type}", configType.Name);
        }
        protected override Dictionary<string, object?> BuildSettings(Type configType, params IConfiguration[] configSources)
        {
            Dictionary<string, object?> settings = [];

            PropertyInfo[] properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                string key = prop.Name;
                object? defaultValue = null;
                object? userValue = null;

                foreach (IConfiguration cfg in configSources)
                {
                    IConfigurationSection section = cfg.GetSection(key);

                    if (!section.Exists())
                        continue;

                    object? val = null;
                    bool didSet = false;

                    // List-handling branch
                    if (prop.PropertyType.IsGenericType &&
                        typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType) &&
                        prop.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        // Build the list element-by-element and Bind
                        Type elementType = prop.PropertyType.GetGenericArguments()[0];
                        var listType = typeof(List<>).MakeGenericType(elementType);
                        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

                        foreach (var child in section.GetChildren())
                        {
                            var element = Activator.CreateInstance(elementType);
                            child.Bind(element);
                            list.Add(element);
                        }
                        val = list;
                        didSet = true;
                    }
                    else
                    {
                        try
                        {
                            val = section.Get(prop.PropertyType);
                            didSet = val != null;
                        }
                        catch
                        {
                            try
                            {
                                val = Convert.ChangeType(section.Value, prop.PropertyType);
                                didSet = val != null;
                            }
                            catch { /* ignore */ }
                        }
                    }

                    if (defaultValue is null)
                        defaultValue = val;
                    else
                        userValue = val;
                }

                settings[key] = new UserConfigServiceSetting(
                    valueType: prop.PropertyType,
                    value: defaultValue,
                    userValue: userValue,
                    readAccess: prop.CanRead,
                    writeAccess: prop.CanWrite
                );
            }

            return settings;
        }

        #endregion
    }
}