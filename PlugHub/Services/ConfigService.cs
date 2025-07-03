using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Services
{
    internal class ConfigServiceConfig(IConfiguration defaultConfig, IConfiguration userConfig, Dictionary<string, ConfigServiceSettingValue> values, JsonSerializerOptions? jsonOptions, Token ownerToken, Token readToken, Token writeToken, bool reloadOnChange)
    {
        public IConfiguration DefaultConfig = defaultConfig;
        public IConfiguration UserConfig = userConfig;

        public Token Owner = ownerToken;
        public Token Read = readToken;
        public Token Write = writeToken;

        public bool ReloadOnChange = reloadOnChange;
        public JsonSerializerOptions? JsonSerializerOptions = jsonOptions;

        public IDisposable? DefaultOnChange;
        public IDisposable? UserOnChange;

        public Dictionary<string, ConfigServiceSettingValue> Values = values;
    }
    internal class ConfigServiceSettingValue(Type? valueType, object? defaultValue, object? userValue, bool readValue, bool writeValue)
    {
        public Type? ValueType { get; set; } = valueType;

        public object? DefaultValue { get; set; } = defaultValue;
        public object? UserValue { get; set; } = userValue;

        public bool ReadAccess { get; set; } = readValue;
        public bool WriteAccess { get; set; } = writeValue;
    }


    public class ConfigService : IConfigService, IDisposable
    {
        public event EventHandler<ConfigServiceSaveErrorEventArgs>? SyncSaveOpErrors;
        public event EventHandler<ConfigServiceConfigReloadedEventArgs>? ConfigReloaded;
        public event EventHandler<ConfigServiceSettingChangeEventArgs>? SettingChanged;

        private readonly ILogger<IConfigService> logger;
        private readonly ITokenService tokenService;

        private readonly ConcurrentDictionary<Type, Timer> reloadTimers = new();
        private readonly ConcurrentDictionary<Type, ConfigServiceConfig> configs = [];

        private readonly JsonSerializerOptions jsonOptions;
        private readonly string configRootDirectory;
        private readonly string configUserDirectory;

        private bool isDesposed = false;

        private readonly ConcurrentDictionary<Type, ReaderWriterLockSlim> configLock = new();

        public ConfigService(ILogger<IConfigService> logger, ITokenService tokenService, string configRootDirectory, string configUserDirectory)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.tokenService = tokenService;

            this.configRootDirectory = configRootDirectory;
            this.configUserDirectory = configUserDirectory;

            this.jsonOptions ??= new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.WriteAsString
            };
        }

        #region ConfigService: Registration

        public void RegisterConfig(Type configType, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            (Token nOwner, Token nRead, Token nWrite) = this.tokenService.CreateTokenSet(ownerToken, readToken, writeToken);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (this.configs.ContainsKey(configType))
                {
                    throw new InvalidOperationException($"Configuration for {configType.Name} is already registered. Use UnregisterConfig before re-registering.");
                }

                jsonOptions ??= this.jsonOptions;

                string defaultConfigFilePath = this.GetDefaultSettingsPath(configType);
                string userConfigFilePath = this.GetUserSettingsPath(configType);

                this.EnsureFileExists(defaultConfigFilePath, configType, jsonOptions);
                this.EnsureFileExists(userConfigFilePath, options: jsonOptions);

                IConfiguration defaultConfig = BuildConfig(defaultConfigFilePath, reloadOnChange);
                IConfiguration userConfig = BuildConfig(userConfigFilePath, reloadOnChange);

                this.configs[configType] = new(
                    defaultConfig,
                    userConfig,
                    BuildSettings(configType, defaultConfig, userConfig),
                    jsonOptions,
                    nOwner,
                    nRead,
                    nWrite,
                    reloadOnChange);

                if (reloadOnChange)
                {
                    this.configs[configType].DefaultOnChange =
                        defaultConfig.GetReloadToken().RegisterChangeCallback(this.OnConfigChanged, configType);

                    this.configs[configType].UserOnChange =
                        userConfig.GetReloadToken().RegisterChangeCallback(this.OnConfigChanged, configType);
                }
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }
        }
        public void RegisterConfig(Type configType, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            this.RegisterConfig(configType, tokenSet?.Owner, tokenSet?.Read, tokenSet?.Write, jsonOptions, reloadOnChange);
        }

        public void RegisterConfigs(IEnumerable<Type> configTypes, Token? ownerToken = null, Token? readToken = null, Token? writeToken = null, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            if (configTypes == null)
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");

            foreach (Type configType in configTypes)
                this.RegisterConfig(configType, ownerToken, readToken, writeToken, jsonOptions, reloadOnChange);
        }
        public void RegisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet, JsonSerializerOptions? jsonOptions = null, bool reloadOnChange = false)
        {
            this.RegisterConfigs(configTypes, tokenSet?.Owner, tokenSet?.Read, tokenSet?.Write, jsonOptions, reloadOnChange);
        }


        public void UnregisterConfig(Type configType, Token? token = null)
        {
            (Token nOwner, _, _) = this.tokenService.CreateTokenSet(token, null, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, null, nOwner, null);

                config.DefaultOnChange?.Dispose();
                config.UserOnChange?.Dispose();

                this.configs.TryRemove(configType, out ConfigServiceConfig? _);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            this.configLock.TryRemove(configType, out ReaderWriterLockSlim? configlock);

            configlock?.Dispose();
        }
        public void UnregisterConfig(Type configType, ITokenSet tokenSet)
        {
            this.UnregisterConfig(configType, tokenSet.Owner);
        }

        public void UnregisterConfigs(IEnumerable<Type> configTypes, Token? token = null)
        {
            if (configTypes == null)
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");

            foreach (Type configType in configTypes)
                this.UnregisterConfig(configType, token);
        }
        public void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet)
        {
            this.UnregisterConfigs(configTypes, tokenSet.Owner);
        }

        #endregion

        public IConfiguration GetEnvConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddCommandLine(Environment.GetCommandLineArgs())
                .Build();
        }

        #region ConfigService: Value Accessors and Mutators

        public T? GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.tokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException(
                        $"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                if (!config.Values.TryGetValue(key, out ConfigServiceSettingValue? setting))
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                return CastStoredValue<T>(setting.DefaultValue);
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public T? GetDefault<T>(Type configType, string key, ITokenSet tokenSet)
        {
            return this.GetDefault<T>(configType, key, tokenSet.Owner, tokenSet.Read);
        }

        public T? GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.tokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException(
                        $"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                if (!config.Values.TryGetValue(key, out ConfigServiceSettingValue? setting))
                    throw new KeyNotFoundException(
                        $"Setting '{key}' not found in {configType.Name}.");

                if (!setting.ReadAccess)
                    throw new UnauthorizedAccessException($"Read access denied for '{key}'");

                object? raw = setting.UserValue ?? setting.DefaultValue;

                return CastStoredValue<T>(raw);
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public T? GetSetting<T>(Type configType, string key, ITokenSet tokenSet)
        {
            return this.GetSetting<T>(configType, key, tokenSet.Owner, tokenSet.Read);
        }


        private static T? CastStoredValue<T>(object? raw)
        {
            if (raw is null) return default;

            if (raw is T typed) return typed;

            try
            {
                if (raw is IConvertible)
                    return (T)Convert.ChangeType(raw, typeof(T));
            }
            catch (InvalidCastException) { }
            catch (FormatException) { }
            catch (OverflowException) { }

            return default;
        }


        public void SetSetting<T>(Type configType, string key, T? newValue, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.tokenService.CreateTokenSet(ownerToken, null, writeToken);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                if (!config.Values.TryGetValue(key, out ConfigServiceSettingValue? settingValue))
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                if (!settingValue.WriteAccess)
                    throw new UnauthorizedAccessException($"Write access denied for '{key}'");

                object? oldValue = settingValue.UserValue ?? settingValue.DefaultValue;

                bool isDefaultValue = false;

                try
                {
                    if (settingValue.DefaultValue != null)
                    {
                        T defaultValue = settingValue.DefaultValue is T val ? val : (T)Convert.ChangeType(settingValue.DefaultValue, typeof(T));

                        isDefaultValue = EqualityComparer<T>.Default.Equals(newValue, defaultValue);
                    }
                }
                catch { /* Ignore conversion errors */ }


                if (isDefaultValue)
                {
                    settingValue.UserValue = null;
                }
                else
                {
                    settingValue.UserValue = newValue;
                }

                SettingChanged?.Invoke(this, new ConfigServiceSettingChangeEventArgs(
                    configType: configType,
                    key: key,
                    oldValue: oldValue,
                    newValue: newValue
                ));
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }
        }
        public void SetSetting<T>(Type configType, string key, T? value, ITokenSet tokenSet)
        {
            this.SetSetting<T>(configType, key, value, tokenSet.Owner, tokenSet.Write);
        }

        public void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.tokenService.CreateTokenSet(ownerToken, null, writeToken);

            Task.Run(async () =>
            {
                try
                {
                    if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                        throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                    this.tokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                    await this.SaveSettingsAsync(configType, nOwner, nWrite);
                }
                catch (Exception ex)
                {
                    this.OnSaveOperationError(ex, ConfigSaveOperation.SaveSettings, configType);
                }
            });
        }
        public void SaveSettings(Type configType, ITokenSet tokenSet)
        {
            this.SaveSettings(configType, tokenSet.Owner, tokenSet.Write);
        }

        public async Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, Token nWrite) = this.tokenService.CreateTokenSet(ownerToken, null, writeToken);

            Dictionary<string, object?> defaultSettings;
            Dictionary<string, object?> userSettings;
            string defaultPath;
            string userPath;
            JsonSerializerOptions jsonOpts;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);
            rw.EnterWriteLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException(
                        $"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                defaultSettings = config.Values.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.DefaultValue);

                userSettings = config.Values
                    .Where(kvp => kvp.Value.UserValue is not null)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.UserValue);

                defaultPath = this.GetDefaultSettingsPath(configType);
                userPath = this.GetUserSettingsPath(configType);
                jsonOpts = config.JsonSerializerOptions ?? this.jsonOptions;
            }
            finally { if (rw.IsWriteLockHeld) rw.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await SaveSettingsToFileAsync(defaultPath, defaultSettings, jsonOpts, cancellationToken)
                      .ConfigureAwait(false);

                await SaveSettingsToFileAsync(userPath, userSettings, jsonOpts, cancellationToken)
                      .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save settings for '{configType.Name}'.", ex);
            }
        }
        public async Task SaveSettingsAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            await this.SaveSettingsAsync(configType, tokenSet.Owner, tokenSet.Write, cancellationToken);
        }

        #endregion

        #region ConfigService: Instance Accesors and Mutators

        public object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.tokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                object instance = Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");

                foreach (PropertyInfo prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite) continue;

                    if (config.Values.TryGetValue(prop.Name, out ConfigServiceSettingValue? setting))
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
                            this.logger.LogError(ex,
                                "Failed to set property {PropertyName} for type {ConfigType}",
                                prop.Name, configType.Name);
                        }
                    }
                }

                return instance;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public object GetConfigInstance(Type configType, ITokenSet tokenSet)
        {
            return this.GetConfigInstance(configType, tokenSet.Owner, tokenSet.Read);
        }

        public void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.tokenService.CreateTokenSet(ownerToken, null, writeToken);

            Task.Run(async () =>
            {
                try
                {
                    if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                        throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                    this.tokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                    await this.SaveConfigInstanceAsync(configType, updatedConfig, nOwner, nWrite);
                }
                catch (Exception ex)
                {
                    this.OnSaveOperationError(ex, ConfigSaveOperation.SaveConfigInstance, configType);
                }
            });
        }
        public void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet)
        {
            this.SaveConfigInstance(configType, updatedConfig, tokenSet.Owner, tokenSet.Write);
        }

        public async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(updatedConfig);

            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, Token nWrite) = this.tokenService.CreateTokenSet(ownerToken, null, writeToken);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                foreach (PropertyInfo prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    string key = prop.Name;
                    object? newValue = prop.GetValue(updatedConfig);

                    if (!config.Values.TryGetValue(key, out ConfigServiceSettingValue? setting))
                        throw new KeyNotFoundException($"Property '{key}' not found in settings.");

                    bool isDefaultValue = false;
                    try
                    {
                        if (setting.DefaultValue != null)
                        {
                            isDefaultValue = object.Equals(newValue, setting.DefaultValue);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Comparison fallback failed for key '{Key}' on type {ConfigType}. Value: {Value}", key, configType.Name, newValue);
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
        public async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            await this.SaveConfigInstanceAsync(configType, updatedConfig, tokenSet.Owner, tokenSet.Write, cancellationToken);
        }

        #endregion

        #region ConfigService: Default Config Mutation/Migration

        public string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
        {
            (Token nOwner, _, _) = this.tokenService.CreateTokenSet(ownerToken, null, null);

            string filePath;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, null, nOwner, null);
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }

            filePath = this.GetDefaultSettingsPath(configType);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Default config file not found: {filePath}");
            }

            return File.ReadAllText(filePath);
        }
        public string GetDefaultConfigFileContents(Type configType, ITokenSet tokenSet)
        {
            return this.GetDefaultConfigFileContents(configType, tokenSet.Owner);
        }

        public void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null)
        {
            (Token nOwner, _, _) = this.tokenService.CreateTokenSet(ownerToken, null, null);

            Task.Run(async () =>
            {
                try
                {
                    if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                        throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                    this.tokenService.AllowAccess(config.Owner, null, nOwner, null);

                    await this.SaveDefaultConfigFileContentsAsync(configType, contents, nOwner);
                }
                catch (Exception ex)
                {
                    this.OnSaveOperationError(ex, ConfigSaveOperation.SaveDefaultConfigFileContents, configType);
                }
            });
        }
        public void SaveDefaultConfigFileContents(Type configType, string contents, ITokenSet tokenSet)
        {
            this.SaveDefaultConfigFileContents(configType, contents, tokenSet.Owner);
        }

        public async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (Token nOwner, _, _) = this.tokenService.CreateTokenSet(ownerToken, null, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterWriteLock();

            try
            {
                if (!this.configs.TryGetValue(configType, out ConfigServiceConfig? config))
                    throw new ConfigTypeNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.tokenService.AllowAccess(config.Owner, null, nOwner, null);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            string settingPath = this.GetDefaultSettingsPath(configType);

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
        public async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            await this.SaveDefaultConfigFileContentsAsync(configType, contents, tokenSet.Owner, cancellationToken);
        }

        #endregion

        void IDisposable.Dispose()
        {
            if (this.isDesposed) return;

            List<ReaderWriterLockSlim> locks = [.. this.configLock.Values];

            foreach (ReaderWriterLockSlim l in locks) l.EnterWriteLock();

            try
            {
                if (this.isDesposed) return;

                foreach (Timer t in this.reloadTimers.Values)
                    t.Dispose();
                this.reloadTimers.Clear();

                foreach (ConfigServiceConfig config in this.configs.Values)
                {
                    config.DefaultOnChange?.Dispose();
                    config.UserOnChange?.Dispose();
                }

                this.configs.Clear();
                this.isDesposed = true;
            }
            finally { foreach (ReaderWriterLockSlim l in locks) if (l.IsWriteLockHeld) l.ExitWriteLock(); }

            foreach (ReaderWriterLockSlim l in this.configLock.Values)
                l.Dispose();

            GC.SuppressFinalize(this);
        }

        #region ConfigService: Event Handlers

        private void OnConfigChanged(object? state)
        {
            if (state is not Type configType)
                return;

            var timer = this.reloadTimers.GetOrAdd(configType, CreateDebounceTimer);

            timer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);

            Timer CreateDebounceTimer(Type key)
            {
                return new Timer(
                    _ => this.HandleReload(key),
                    null,
                    TimeSpan.FromMilliseconds(300),
                    Timeout.InfiniteTimeSpan);
            }
        }

        private void OnSaveOperationError(Exception ex, ConfigSaveOperation operation, Type configType)
        {
            this.SyncSaveOpErrors?.Invoke(this, new ConfigServiceSaveErrorEventArgs(ex, operation, configType));
        }

        private void HandleReload(Type configType)
        {
            ConfigServiceConfig? config = null;

            bool found = false;
            bool reloaded = false;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                if (this.configs.TryGetValue(configType, out config))
                {
                    Dictionary<string, ConfigServiceSettingValue> newSettings =
                        BuildSettings(configType, config.DefaultConfig, config.UserConfig);

                    config.Values = newSettings;

                    found = true;

                    if (config.ReloadOnChange)
                    {
                        config.DefaultOnChange?.Dispose();
                        config.UserOnChange?.Dispose();

                        config.DefaultOnChange = config.DefaultConfig
                            .GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigChanged, configType);

                        config.UserOnChange = config.UserConfig
                            .GetReloadToken()
                            .RegisterChangeCallback(this.OnConfigChanged, configType);
                    }

                    reloaded = true;
                }
            }
            catch (FileNotFoundException ex)
            {
                this.logger.LogWarning(ex, "Config directory missing – Type {Type}", configType.Name);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Config reload failed – Type {Type}", configType.Name);
            }
            finally
            {
                if (rw.IsWriteLockHeld) rw.ExitWriteLock();
            }

            if (reloaded)
                ConfigReloaded?.Invoke(this, new ConfigServiceConfigReloadedEventArgs(configType));

            if (!found)
                this.logger.LogWarning("Unregistered config type – Type {Type}", configType.Name);
        }

        #endregion

        private ReaderWriterLockSlim GetConfigTypeLock(Type sectionType)
            => this.configLock.GetOrAdd(sectionType, _ => new ReaderWriterLockSlim());

        private static IConfiguration BuildConfig(string filePath, bool reloadOnChange = false)
        {
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
        private static Dictionary<string, ConfigServiceSettingValue> BuildSettings(Type configType, IConfiguration defaultConfig, IConfiguration userConfig)
        {
            Dictionary<string, ConfigServiceSettingValue> settings = [];
            PropertyInfo[] properties = configType.GetProperties();

            foreach (PropertyInfo prop in properties)
            {
                string key = prop.Name;

                object? defaultValue = GetConfigurationValue(defaultConfig, key, prop.PropertyType);
                object? userValue = GetConfigurationValue(userConfig, key, prop.PropertyType);

                bool isReadable = prop.CanRead;
                bool isWritable = prop.CanWrite;

                settings[key] = new ConfigServiceSettingValue(
                    valueType: prop.PropertyType,
                    defaultValue: defaultValue,
                    userValue: userValue,
                    readValue: isReadable,
                    writeValue: isWritable
                );
            }

            return settings;
        }
        private static object? GetConfigurationValue(IConfiguration config, string key, Type targetType)
        {
            IConfigurationSection section = config.GetSection(key);
            if (!section.Exists()) return null;

            try
            {
                return section.Get(targetType);
            }
            catch
            {
                return Convert.ChangeType(section.Value, targetType);
            }
        }

        private string GetDefaultSettingsPath(Type configType)
            => Path.Combine(this.configRootDirectory, $"{configType.Name}.DefaultSettings.json");
        private string GetUserSettingsPath(Type configType)
            => Path.Combine(this.configUserDirectory, $"{configType.Name}.UserSettings.json");

        private static void EnsureDirectoryExists(string filePath)
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
        private void EnsureFileExists(string filePath, Type? configType = null, JsonSerializerOptions? options = null)
        {
            EnsureDirectoryExists(filePath);

            if (!File.Exists(filePath))
            {
                try
                {
                    string content = "{}";

                    if (configType != null)
                        content = configType.SerializeToJson(options ?? this.jsonOptions);

                    Atomic.Write(filePath, content);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Failed to create the required configuration file at '{filePath}'.", ex);
                }
            }
        }

        private static async Task SaveSettingsToFileAsync(string filePath, Dictionary<string, object?> settings, JsonSerializerOptions options, CancellationToken cancellationToken = default)
        {
            string serialized;

            try
            {
                serialized = JsonSerializer.Serialize(settings, options);
            }
            catch (NotSupportedException ex)
            {
                throw new InvalidOperationException("Failed to serialize the provided settings object.", ex);
            }

            await Atomic.WriteAsync(filePath, serialized, cancellationToken: cancellationToken);
        }
    }
}