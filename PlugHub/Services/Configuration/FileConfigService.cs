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
    public class FileConfigServiceConfig(IConfigService configService, string configPath, IConfiguration config, Dictionary<string, object?> values, JsonSerializerOptions? jsonOptions, Token ownerToken, Token readToken, Token writeToken, bool reloadOnChanged)
    {
        public IConfigService ConfigService { get; init; } = configService;

        public Token Owner { get; init; } = ownerToken;
        public Token Read { get; init; } = readToken;
        public Token Write { get; init; } = writeToken;

        public bool ReloadOnChanged { get; init; } = reloadOnChanged;
        public JsonSerializerOptions JsonSerializerOptions { get; init; } = jsonOptions ?? new JsonSerializerOptions();

        public string ConfigPath { get; init; } = configPath;
        public IConfiguration Config { get; init; } = config;
        public IDisposable? OnChanged { get; set; } = null;

        public Dictionary<string, object?> Values = values;
    }
    public class FileConfigServiceSetting(Type? valueType, object? defaultValue, bool readValue, bool writeValue)
    {
        public Type? ValueType { get; init; } = valueType;

        public object? DefaultValue { get; set; } = defaultValue;

        public bool ReadAccess { get; init; } = readValue;
        public bool WriteAccess { get; init; } = writeValue;
    }

    public class FileConfigService
        : ConfigServiceBase, IConfigServiceProvider, IDisposable
    {
        public FileConfigService(ILogger<IConfigServiceProvider> logger, ITokenService tokenService)
            : base(logger, tokenService)
        {
            this.SupportedParamsTypes = [typeof(FileConfigServiceParams)];
            this.RequiredAccessorInterface = typeof(IFileConfigAccessor);
        }

        #region FileConfigService: Registration

        public override void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService configService)
        {
            if (configParams is not FileConfigServiceParams p)
                throw new ArgumentException($"Expected FileConfigServiceParams, got {configParams.GetType().Name}", nameof(configParams));

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

                this.EnsureFileExists(defaultConfigFilePath, jsonOptions, configType);

                IConfiguration defaultConfig =
                    this.BuildConfig(defaultConfigFilePath, reloadOnChange);

                Dictionary<string, object?> values = this.BuildSettings(configType, defaultConfig);

                FileConfigServiceConfig typeConfig = new(
                    configService,
                    defaultConfigFilePath,
                    defaultConfig,
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
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, null, nOwner, null);

                config.OnChanged?.Dispose();

                this.Configs.TryRemove(configType, out object? _);
            }
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            this.ConfigLock.TryRemove(configType, out ReaderWriterLockSlim? configlock);

            configlock?.Dispose();
        }

        #endregion

        #region FileConfigService: Value Accessors and Mutators

        public override T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? rawConfig) || rawConfig is not FileConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                if (!config.Values.TryGetValue(key, out object? rawSetting) || rawSetting is not FileConfigServiceSetting setting)
                    throw new KeyNotFoundException($"Setting '{key}' not found in {configType.Name}.");

                T? result;

                try
                {
                    result = this.CastStoredValue<T>(setting.DefaultValue);
                }
                catch (InvalidCastException)
                {
                    return default!;
                }

                return result!;
            }
            finally { if (rwLock.IsReadLockHeld) rwLock.ExitReadLock(); }
        }
        public override T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => this.GetDefault<T>(configType, key, ownerToken, readToken);

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
                    setting.DefaultValue = (T)Convert.ChangeType(newValue, typeof(T))!;
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
            => this.SetDefault<T>(configType, key, newValue, ownerToken, writeToken);

        public override void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null)
        {
            (Token nOwner, _, Token nWrite) = this.TokenService.CreateTokenSet(ownerToken, null, writeToken);

            IConfigService? configService = null;
            ConfigServiceSaveErrorEventArgs? errorArgs = null;

            Task.Run(async () =>
            {
                try
                {
                    if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
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
            string defaultPath;
            JsonSerializerOptions jsonOpts;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);
            rw.EnterWriteLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                defaultSettings = config.Values
                    .Select(kvp => (kvp.Key, setting: kvp.Value as FileConfigServiceSetting))
                    .Where(t => t.setting is not null)
                    .ToDictionary(
                        t => t.Key,
                        t => t.setting!.DefaultValue);

                defaultPath = config.ConfigPath;

                jsonOpts = config.JsonSerializerOptions;
            }
            finally { if (rw.IsWriteLockHeld) rw.ExitWriteLock(); }

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

        #region FileConfigService: Instance Accesors and Mutators

        public override object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
        {
            (Token nOwner, Token nRead, _) = this.TokenService.CreateTokenSet(ownerToken, readToken, null);

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Read, nOwner, nRead);

                object instance = Activator.CreateInstance(configType)
                    ?? throw new InvalidOperationException($"Failed to create instance of {configType.Name}");

                foreach (PropertyInfo prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!prop.CanWrite) continue;

                    if (config.Values.TryGetValue(prop.Name, out object? rawSetting) && rawSetting is FileConfigServiceSetting setting)
                    {
                        object? value = setting.DefaultValue;

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
                    if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
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
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
                    throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

                this.TokenService.AllowAccess(config.Owner, config.Write, nOwner, nWrite);

                foreach (PropertyInfo prop in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    string key = prop.Name;
                    object? newValue = prop.GetValue(updatedConfig);

                    if (!config.Values.TryGetValue(key, out object? rawSetting) || rawSetting is not FileConfigServiceSetting setting)
                        throw new KeyNotFoundException($"Property '{key}' not found in settings.");

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
            finally { if (rwLock.IsWriteLockHeld) rwLock.ExitWriteLock(); }

            cancellationToken.ThrowIfCancellationRequested();

            await this.SaveSettingsAsync(configType, nOwner, nWrite, cancellationToken);
        }

        #endregion

        #region FileConfigService: Default Config Mutation/Migration

        public override string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
        {
            (Token nOwner, _, _) = this.TokenService.CreateTokenSet(ownerToken, null, null);

            string filePath;

            ReaderWriterLockSlim rwLock = this.GetConfigTypeLock(configType);
            rwLock.EnterReadLock();

            try
            {
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
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
                    if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
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
                if (!this.Configs.TryGetValue(configType, out object? raw) || raw is not FileConfigServiceConfig config)
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

        #region FileConfigService: Utilities

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

                foreach (FileConfigServiceConfig config in this.Configs.Values.Cast<FileConfigServiceConfig>())
                {
                    config.OnChanged?.Dispose();
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
            FileConfigServiceConfig? config = null;

            IConfigService? configService = null;
            bool found = false;
            bool reloaded = false;

            ReaderWriterLockSlim rw = this.GetConfigTypeLock(configType);

            rw.EnterWriteLock();

            try
            {
                if (this.Configs.TryGetValue(configType, out object? raw) && raw is FileConfigServiceConfig cooked)
                {
                    config = cooked;
                    configService = config.ConfigService;

                    Dictionary<string, object?> newSettings =
                        this.BuildSettings(configType, config.Config);

                    config.Values = newSettings;

                    found = true;

                    if (config.ReloadOnChanged)
                    {
                        config.OnChanged?.Dispose();

                        config.OnChanged = config.Config
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

            if (reloaded && configService != null)
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

                foreach (IConfiguration cfg in configSources)
                {
                    IConfigurationSection section = cfg.GetSection(key);

                    if (!section.Exists())
                        continue;

                    object? val;

                    try
                    {
                        val = section.Get(prop.PropertyType);
                    }
                    catch
                    {
                        val = Convert.ChangeType(section.Value, prop.PropertyType);
                    }

                    defaultValue = val;
                }

                settings[key] = new FileConfigServiceSetting(
                    valueType: prop.PropertyType,
                    defaultValue: defaultValue,
                    readValue: prop.CanRead,
                    writeValue: prop.CanWrite
                );
            }

            return settings;
        }

        #endregion
    }
}