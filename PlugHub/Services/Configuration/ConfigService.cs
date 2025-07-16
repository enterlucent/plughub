using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace PlugHub.Services.Configuration
{
    public class ConfigService : IConfigService, IDisposable
    {
        public event EventHandler<ConfigServiceSaveCompletedEventArgs>? SyncSaveCompleted;
        public event EventHandler<ConfigServiceSaveErrorEventArgs>? SyncSaveErrors;
        public event EventHandler<ConfigServiceConfigReloadedEventArgs>? ConfigReloaded;
        public event EventHandler<ConfigServiceSettingChangeEventArgs>? SettingChanged;

        public JsonSerializerOptions JsonOptions { get; init; }
        public string ConfigAppDirectory { get; init; }
        public string ConfigDataDirectory { get; init; }

        protected readonly ConcurrentDictionary<Type, IConfigServiceProvider> ProvidersByParamsType;
        protected readonly ConcurrentDictionary<Type, IConfigServiceProvider> ProvidersByConfigType;
        protected readonly ConcurrentDictionary<Type, IConfigAccessor> AccessorByInterface;

        protected readonly ILogger<IConfigService> Logger;
        protected readonly ITokenService TokenService;

        protected bool IsDisposed = false;

        public ConfigService(IEnumerable<IConfigServiceProvider> providers, IEnumerable<IConfigAccessor> accessors, ILogger<IConfigService> logger, ITokenService tokenService, string configRootDirectory, string configDataDirectory, JsonSerializerOptions? jsonOptions = null)
        {
            this.ProvidersByParamsType = [];
            this.ProvidersByConfigType = [];
            this.AccessorByInterface = [];

            foreach (IConfigServiceProvider provider in providers)
            {
                foreach (Type paramType in provider.SupportedParamsTypes)
                {
                    if (this.ProvidersByParamsType.ContainsKey(paramType))
                        throw new InvalidOperationException($"Multiple implementations claim support for {paramType.Name}");

                    this.ProvidersByParamsType[paramType] = provider;
                }
            }

            foreach (IConfigAccessor accessorProv in accessors)
            {
                if (this.AccessorByInterface.ContainsKey(accessorProv.AccessorInterface))
                    throw new InvalidOperationException($"Multiple implementations claim support for {accessorProv.AccessorInterface}");

                this.AccessorByInterface[accessorProv.AccessorInterface] = accessorProv;
            }

            this.Logger = logger;
            this.TokenService = tokenService;

            this.ConfigAppDirectory = configRootDirectory;
            this.ConfigDataDirectory = configDataDirectory;

            this.JsonOptions = jsonOptions ?? new JsonSerializerOptions();
        }

        #region ConfigService: Accessors

        public IConfigAccessorFor<TConfig> GetAccessor<TConfig>(Token? owner = null, Token? read = null, Token? write = null) where TConfig : class
        {
            Type configType = typeof(TConfig);
            IConfigServiceProvider configProvider = this.GetProviderForConfigType(configType);

            IConfigAccessor accessorProvider = this.GetAccessorProviderByInterface(configProvider.RequiredAccessorInterface);

            (Token nOwner, Token nRead, Token nWrite) = this.TokenService.CreateTokenSet(owner, read, write);

            return accessorProvider.CreateFor<TConfig>(this, nOwner, nRead, nWrite);
        }
        public IConfigAccessorFor<TConfig> GetAccessor<TConfig>(ITokenSet tokenSet) where TConfig : class
            => this.GetAccessor<TConfig>(tokenSet.Owner, tokenSet.Read, tokenSet.Write);

        public TAccessor GetAccessor<TAccessor, TConfig>(Token? owner = null, Token? read = null, Token? write = null)
            where TConfig : class
            where TAccessor : IConfigAccessorFor<TConfig>
        {
            IConfigAccessorFor<TConfig> defaultAccessor = this.GetAccessor<TConfig>(owner, read, write);

            if (defaultAccessor is TAccessor typed)
                return typed;

            throw new InvalidCastException(
                $"Accessor for {typeof(TConfig).Name} is a {defaultAccessor.GetType().Name} " +
                $"and cannot be cast to {typeof(TAccessor).Name}");
        }
        public TAccessor GetAccessor<TAccessor, TConfig>(ITokenSet tokenSet)
            where TConfig : class
            where TAccessor : IConfigAccessorFor<TConfig>
        {
            return this.GetAccessor<TAccessor, TConfig>(tokenSet.Owner, tokenSet.Read, tokenSet.Write);
        }

        public IConfigAccessor GetAccessor(Type accessorInterface, IEnumerable<Type> configTypes, Token? owner = null, Token? read = null, Token? write = null)
        {
            ArgumentNullException.ThrowIfNull(configTypes);

            IConfigAccessor accessorProvider = this.GetAccessorProviderByInterface(accessorInterface);

            (Token nOwner, Token nRead, Token nWrite) = this.TokenService.CreateTokenSet(owner, read, write);

            return accessorProvider
                .SetConfigTypes([.. configTypes])
                .SetConfigService(this)
                .SetAccess(nOwner, nRead, nWrite);
        }
        public IConfigAccessor GetAccessor(Type accessorInterface, IEnumerable<Type> configTypes, ITokenSet tokenSet)
            => this.GetAccessor(accessorInterface, configTypes, tokenSet.Owner, tokenSet.Read, tokenSet.Write);

        #endregion

        #region ConfigService: Registration

        public void RegisterConfig(Type configType, IConfigServiceParams configParams)
        {
            Type paramType = configParams.GetType();

            if (!this.ProvidersByParamsType.TryGetValue(paramType, out IConfigServiceProvider? provider))
                throw new InvalidOperationException($"No config implementation registered for parameter type {paramType.Name}");

            if (!this.ProvidersByConfigType.TryAdd(configType, provider))
                throw new InvalidOperationException($"{configType.Name} was already registered.");

            provider.RegisterConfig(configType, configParams, this);
        }
        public void RegisterConfig<TConfig>(IConfigServiceParams configParams, out IConfigAccessorFor<TConfig> accessor) where TConfig : class
        {
            this.RegisterConfig(typeof(TConfig), configParams);

            accessor = this.GetAccessor<TConfig>(configParams.Owner, configParams.Read, configParams.Write);
        }
        public void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams)
        {
            if (configTypes == null)
            {
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");
            }

            foreach (Type type in configTypes)
                this.RegisterConfig(type, configParams);
        }
        public void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams, out IConfigAccessor accessor)
        {
            ArgumentNullException.ThrowIfNull(configTypes);

            foreach (Type type in configTypes)
                this.RegisterConfig(type, configParams);

            Type paramType = configParams.GetType();

            IConfigServiceProvider provider = this.GetProviderForParamsType(paramType);

            accessor = this.GetAccessor(provider.RequiredAccessorInterface, configTypes, configParams.Owner, configParams.Read, configParams.Write);
        }

        public void UnregisterConfig(Type configType, Token? token = null)
        {
            if (!this.ProvidersByConfigType.TryGetValue(configType, out IConfigServiceProvider? provider))
                throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");

            provider?.UnregisterConfig(configType, token);

            this.ProvidersByConfigType.TryRemove(configType, out IConfigServiceProvider? _);
        }
        public void UnregisterConfig(Type configType, ITokenSet tokenSet)
            => this.UnregisterConfig(configType, tokenSet.Owner);

        public void UnregisterConfigs(IEnumerable<Type> configTypes, Token? token = null)
        {
            if (configTypes == null)
            {
                throw new ArgumentNullException(nameof(configTypes), "Configuration types collection cannot be null.");
            }

            foreach (Type type in configTypes)
                this.UnregisterConfig(type, token);
        }
        public void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet)
            => this.UnregisterConfigs(configTypes, tokenSet.Owner);

        #endregion

        public static IConfiguration GetEnvConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddCommandLine(Environment.GetCommandLineArgs())
                .Build();
        }

        #region ConfigService: Value Accessors and Mutators

        public T GetDefault<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => this.GetProviderForConfigType(configType).GetDefault<T>(configType, key, ownerToken, readToken);
        public T GetDefault<T>(Type configType, string key, ITokenSet tokenSet)
            => this.GetDefault<T>(configType, key, tokenSet.Owner, tokenSet.Read);

        public T GetSetting<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
            => this.GetProviderForConfigType(configType).GetSetting<T>(configType, key, ownerToken, readToken);
        public T GetSetting<T>(Type configType, string key, ITokenSet tokenSet)
            => this.GetSetting<T>(configType, key, tokenSet.Owner, tokenSet.Read);

        public void SetDefault<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
            => this.GetProviderForConfigType(configType).SetDefault(configType, key, newValue, ownerToken, writeToken);
        public void SetDefault<T>(Type configType, string key, T newValue, ITokenSet tokenSet)
            => this.SetDefault<T>(configType, key, newValue, tokenSet.Owner, tokenSet.Read);

        public void SetSetting<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
            => this.GetProviderForConfigType(configType).SetSetting(configType, key, newValue, ownerToken, writeToken);
        public void SetSetting<T>(Type configType, string key, T newValue, ITokenSet tokenSet)
            => this.SetSetting(configType, key, newValue, tokenSet.Owner, tokenSet.Write);

        public void SaveSettings(Type configType, Token? ownerToken = null, Token? writeToken = null)
            => this.GetProviderForConfigType(configType).SaveSettings(configType, ownerToken, writeToken);
        public void SaveSettings(Type configType, ITokenSet tokenSet)
            => this.SaveSettings(configType, tokenSet.Owner, tokenSet.Write);

        public async Task SaveSettingsAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
            => await this.GetProviderForConfigType(configType).SaveSettingsAsync(configType, ownerToken, writeToken, cancellationToken);
        public async Task SaveSettingsAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default)
            => await this.SaveSettingsAsync(configType, tokenSet.Owner, tokenSet.Write, cancellationToken);

        #endregion

        #region ConfigService: Instance Accesors and Mutators

        public virtual object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
            => this.GetProviderForConfigType(configType).GetConfigInstance(configType, ownerToken, readToken);
        public virtual object GetConfigInstance(Type configType, ITokenSet tokenSet)
            => this.GetConfigInstance(configType, tokenSet.Owner, tokenSet.Read);

        public virtual void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null)
            => this.GetProviderForConfigType(configType).SaveConfigInstance(configType, updatedConfig, ownerToken, writeToken);
        public virtual void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet)
            => this.SaveConfigInstance(configType, updatedConfig, tokenSet.Owner, tokenSet.Write);

        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
            => await this.GetProviderForConfigType(configType).SaveConfigInstanceAsync(configType, updatedConfig, ownerToken, writeToken, cancellationToken);
        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default)
            => await this.SaveConfigInstanceAsync(configType, updatedConfig, tokenSet.Owner, tokenSet.Write, cancellationToken);

        #endregion

        #region ConfigService: Default Config Mutation/Migration

        public virtual string GetDefaultConfigFileContents(Type configType, Token? ownerToken = null)
            => this.GetProviderForConfigType(configType).GetDefaultConfigFileContents(configType, ownerToken);
        public virtual string GetDefaultConfigFileContents(Type configType, ITokenSet tokenSet)
            => this.GetDefaultConfigFileContents(configType, tokenSet.Owner);

        public virtual void SaveDefaultConfigFileContents(Type configType, string contents, Token? ownerToken = null)
            => this.GetProviderForConfigType(configType).SaveDefaultConfigFileContents(configType, contents, ownerToken);
        public virtual void SaveDefaultConfigFileContents(Type configType, string contents, ITokenSet tokenSet)
            => this.SaveDefaultConfigFileContents(configType, contents, tokenSet.Owner);

        public virtual async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, Token? ownerToken = null, CancellationToken cancellationToken = default)
            => await this.GetProviderForConfigType(configType).SaveDefaultConfigFileContentsAsync(configType, contents, ownerToken, cancellationToken);
        public virtual async Task SaveDefaultConfigFileContentsAsync(Type configType, string contents, ITokenSet tokenSet, CancellationToken cancellationToken = default)
            => await this.SaveDefaultConfigFileContentsAsync(configType, contents, tokenSet.Owner, cancellationToken);

        #endregion

        public virtual void Dispose()
        {
            if (this.IsDisposed == true) return;

            foreach (KeyValuePair<Type, IConfigServiceProvider> kvp in this.ProvidersByParamsType)
                ((IDisposable)kvp.Value).Dispose();

            GC.SuppressFinalize(this);

            this.IsDisposed = true;
        }

        #region ConfigService: Event Handlers

        public virtual void OnSaveOperationComplete(object sender, Type configType)
            => this.SyncSaveCompleted?.Invoke(sender, new ConfigServiceSaveCompletedEventArgs(configType));
        public virtual void OnSaveOperationError(object sender, Exception ex, ConfigSaveOperation operation, Type configType)
            => this.SyncSaveErrors?.Invoke(sender, new ConfigServiceSaveErrorEventArgs(ex, operation, configType));
        public virtual void OnSettingChanged(object sender, Type configType, string key, object? oldValue, object? newValue)
            => this.SettingChanged?.Invoke(sender, new ConfigServiceSettingChangeEventArgs(configType, key, oldValue, newValue));
        public virtual void OnConfigReloaded(object sender, Type configType)
            => this.ConfigReloaded?.Invoke(sender, new ConfigServiceConfigReloadedEventArgs(configType));

        #endregion

        #region ConfigService: Utilities

        protected IConfigServiceProvider GetProviderForParamsType(Type configType)
        {
            if (!this.ProvidersByParamsType.TryGetValue(configType, out IConfigServiceProvider? provider))
                throw new KeyNotFoundException($"Parameters for {configType.Name} is not registered.");

            return provider;
        }
        protected IConfigServiceProvider GetProviderForConfigType(Type paramsType)
        {
            if (!this.ProvidersByConfigType.TryGetValue(paramsType, out IConfigServiceProvider? provider))
                throw new KeyNotFoundException($"Configuration for {paramsType.Name} is not registered.");

            return provider;
        }
        protected IConfigAccessor GetAccessorProviderByInterface(Type accessorInterface)
        {
            if (!this.AccessorByInterface.TryGetValue(accessorInterface, out IConfigAccessor? accessorProvider))
                throw new InvalidOperationException($"No accessor registered for interface {accessorInterface.Name}");

            return accessorProvider;
        }

        #endregion
    }
}