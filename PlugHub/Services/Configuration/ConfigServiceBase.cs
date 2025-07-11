using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using PlugHub.Shared.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlugHub.Services.Configuration
{
    public abstract class ConfigServiceBase(ILogger<IConfigServiceProvider> logger, ITokenService tokenService) : IConfigServiceProvider, IDisposable
    {
        public IEnumerable<Type> SupportedParamsTypes { get; init; } = [];
        public Type RequiredAccessorInterface { get; init; } = typeof(IFileConfigAccessor);

        protected readonly ILogger<IConfigServiceProvider> Logger = logger;
        protected readonly ITokenService TokenService = tokenService;

        protected readonly ConcurrentDictionary<Type, Timer> ReloadTimers = new();
        protected readonly ConcurrentDictionary<Type, object?> Configs = [];
        protected readonly ConcurrentDictionary<Type, ReaderWriterLockSlim> ConfigLock = new();

        protected JsonSerializerOptions JsonOptions { get; init; } = new JsonSerializerOptions();
        protected bool IsDisposed = false;

        #region ConfigServiceBase: Registration

        public virtual void RegisterConfig(Type configType, IConfigServiceParams configParams, IConfigService service)
            => throw new NotImplementedException();
        public virtual void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams, IConfigService service)
        {
            if (configTypes == null)
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");

            foreach (Type configType in configTypes)
                this.RegisterConfig(configType, configParams, service);
        }

        public virtual void UnregisterConfig(Type configType, Token? token = null)
            => throw new NotImplementedException();
        public virtual void UnregisterConfig(Type configType, ITokenSet tokenSet)
            => this.UnregisterConfig(configType, tokenSet.Owner);

        public virtual void UnregisterConfigs(IEnumerable<Type> configTypes, Token? token = null)
        {
            if (configTypes == null)
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");

            foreach (Type configType in configTypes)
                this.UnregisterConfig(configType, token);
        }
        public virtual void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet)
            => this.UnregisterConfigs(configTypes, tokenSet.Owner);

        #endregion

        #region ConfigServiceBase: Value Accessors and Mutators

        public virtual T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => throw new NotImplementedException();
        public virtual T GetDefault<T>(Type configType, string key, ITokenSet tokenSet)
            => this.GetDefault<T>(configType, key, tokenSet.Owner, tokenSet.Read);

        public virtual T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => throw new NotImplementedException();
        public virtual T GetSetting<T>(Type configType, string key, ITokenSet tokenSet)
            => this.GetSetting<T>(configType, key, tokenSet.Owner, tokenSet.Read);

        public virtual void SetDefault<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SetDefault<T>(Type configType, string key, T newValue, ITokenSet tokenSet)
            => this.SetDefault<T>(configType, key, newValue, tokenSet.Owner, tokenSet.Write);

        public virtual void SetSetting<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SetSetting<T>(Type configType, string key, T value, ITokenSet tokenSet)
            => this.SetSetting(configType, key, value, tokenSet.Owner, tokenSet.Write);

        public virtual void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SaveSettings(Type configType, ITokenSet tokenSet)
            => this.SaveSettings(configType, tokenSet.Owner, tokenSet.Write);

        public virtual async Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
            => await Task.FromException(new NotImplementedException());
        public virtual async Task SaveSettingsAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default)
            => await this.SaveSettingsAsync(configType, tokenSet.Owner, tokenSet.Write, cancellationToken);

        #endregion

        #region ConfigServiceBase: Instance Accesors and Mutators

        public virtual object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
            => throw new NotImplementedException();
        public virtual object GetConfigInstance(Type configType, ITokenSet tokenSet)
            => this.GetConfigInstance(configType, tokenSet.Owner, tokenSet.Read);

        public virtual void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null)
            => throw new NotImplementedException();
        public virtual void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet)
            => this.SaveConfigInstance(configType, updatedConfig, tokenSet.Owner, tokenSet.Write);

        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
            => await Task.FromException(new NotImplementedException());
        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default)
            => await this.SaveConfigInstanceAsync(configType, updatedConfig, tokenSet.Owner, tokenSet.Write, cancellationToken);

        #endregion

        #region ConfigServiceBase: Default Config Mutation/Migration

        public virtual string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
            => throw new NotImplementedException();
        public virtual string GetDefaultConfigFileContents(Type configType, ITokenSet tokenSet)
            => this.GetDefaultConfigFileContents(configType, tokenSet.Owner);

        public virtual void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null)
            => throw new NotImplementedException();
        public virtual void SaveDefaultConfigFileContents(Type configType, string contents, ITokenSet tokenSet)
            => this.SaveDefaultConfigFileContents(configType, contents, tokenSet.Owner);

        public virtual async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default)
            => await Task.FromException(new NotImplementedException());
        public virtual async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, ITokenSet tokenSet, CancellationToken cancellationToken = default)
            => await this.SaveDefaultConfigFileContentsAsync(configType, contents, tokenSet.Owner, cancellationToken);

        #endregion

        public virtual void Dispose()
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

        #region ConfigServiceBase: Utilities

        [return: MaybeNull]
        protected virtual T CastStoredValue<T>(object? raw)
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

        protected virtual ReaderWriterLockSlim GetConfigTypeLock(Type configType)
            => this.ConfigLock.GetOrAdd(configType, _ => new ReaderWriterLockSlim());

        protected virtual void HandleConfigHasChanged(Type configType)
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
        protected virtual void OnConfigHasChanged(object? state)
        {
            if (state is not Type configType)
                return;

            Timer timer = this.ReloadTimers.GetOrAdd(configType, CreateDebounceTimer);

            timer.Change(TimeSpan.FromMilliseconds(300), Timeout.InfiniteTimeSpan);

            Timer CreateDebounceTimer(Type key)
            {
                return new Timer(
                    _ => this.HandleConfigHasChanged(key),
                    null,
                    TimeSpan.FromMilliseconds(300),
                    Timeout.InfiniteTimeSpan);
            }
        }

        protected virtual IConfiguration BuildConfig(string filePath, bool reloadOnChange = false)
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
        protected virtual Dictionary<string, object?> BuildSettings(Type configType, params IConfiguration[] configSources)
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

                    object? val;

                    try
                    {
                        val = section.Get(prop.PropertyType);
                    }
                    catch
                    {
                        val = Convert.ChangeType(section.Value, prop.PropertyType);
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
        protected virtual object? GetBuildSettingsValue(IConfiguration config, PropertyInfo prop)
        {
            IConfigurationSection section = config.GetSection(prop.Name);

            if (!section.Exists()) return null;

            try
            {
                return section.Get(prop.PropertyType);
            }
            catch
            {
                return Convert.ChangeType(section.Value, prop.PropertyType);
            }
        }

        protected virtual void EnsureDirectoryExists(string filePath)
        {
            string? directory = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
        protected virtual void EnsureFileExists(string filePath, JsonSerializerOptions options, Type? configType = null)
        {
            this.EnsureDirectoryExists(filePath);

            if (!File.Exists(filePath))
            {
                try
                {
                    string content = "{}";

                    if (configType != null)
                        content = configType.SerializeToJson(options ?? this.JsonOptions);

                    Atomic.Write(filePath, content);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Failed to create the required configuration file at '{filePath}'.", ex);
                }
            }
        }
        protected virtual string ResolveLocalFilePath(string? overridePath, string root, Type t, string suffix)
        {
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                if (Path.IsPathRooted(overridePath))
                    return overridePath;
                return Path.Combine(root, overridePath);
            }
            return Path.Combine(root, $"{t.Name}.{suffix}");
        }

        protected virtual async Task SaveSettingsToFileAsync(string filePath, Dictionary<string, object?> settings, JsonSerializerOptions options, CancellationToken cancellationToken = default)
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

        #endregion
    }
}