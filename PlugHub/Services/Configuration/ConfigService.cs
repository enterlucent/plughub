using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Models;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
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

        protected readonly ConcurrentDictionary<Type, IConfigProvider> ProvidersByParamsType;
        protected readonly ConcurrentDictionary<Type, IConfigProvider> ProvidersByConfigType;
        protected readonly ConcurrentDictionary<Type, IConfigAccessor> AccessorByInterface;

        protected readonly ILogger<IConfigService> Logger;
        protected readonly ITokenService TokenService;

        protected bool IsDisposed = false;

        public static IConfigService GetInstance(IServiceCollection services, AppConfig appConfig)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(appConfig);

            using (ServiceProvider provider = services.BuildServiceProvider())
            {
                IEnumerable<IConfigProvider> providers = provider.GetRequiredService<IEnumerable<IConfigProvider>>();
                IEnumerable<IConfigAccessor> accessors = provider.GetRequiredService<IEnumerable<IConfigAccessor>>();
                ILogger<IConfigService> logger = provider.GetRequiredService<ILogger<IConfigService>>();
                ITokenService tokenService = provider.GetRequiredService<ITokenService>();

                return new ConfigService(
                        providers,
                        accessors,
                        logger,
                        tokenService,
                        AppContext.BaseDirectory,
                        appConfig.ConfigDirectory ?? AppContext.BaseDirectory,
                        appConfig.ConfigJsonOptions);
            }
        }

        public ConfigService(IEnumerable<IConfigProvider> providers, IEnumerable<IConfigAccessor> accessors, ILogger<IConfigService> logger, ITokenService tokenService, string configRootDirectory, string configDataDirectory, JsonSerializerOptions? jsonOptions = null)
        {
            ArgumentNullException.ThrowIfNull(providers);
            ArgumentNullException.ThrowIfNull(accessors);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(tokenService);
            ArgumentNullException.ThrowIfNull(configRootDirectory);
            ArgumentNullException.ThrowIfNull(configDataDirectory);

            this.ProvidersByParamsType = [];
            this.ProvidersByConfigType = [];
            this.AccessorByInterface = [];

            this.Logger = logger;
            this.TokenService = tokenService;
            this.ConfigAppDirectory = configRootDirectory;
            this.ConfigDataDirectory = configDataDirectory;
            this.JsonOptions = jsonOptions ?? new JsonSerializerOptions();

            this.RegisterConfigServiceProviders(providers);
            this.RegisterConfigAccessors(accessors);

            this.Logger.LogInformation("ConfigService initialized with {ProviderCount} providers and {AccessorCount} accessors",
                this.ProvidersByParamsType.Count, this.AccessorByInterface.Count);
        }

        public IConfiguration GetEnvConfig()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddEnvironmentVariables()
                .AddCommandLine(Environment.GetCommandLineArgs())
                .Build();
        }

        #region ConfigService: Accessor Management

        public IConfigAccessorFor<TConfig> GetAccessor<TConfig>(Token? owner = null, Token? read = null, Token? write = null) where TConfig : class
        {
            ArgumentNullException.ThrowIfNull(typeof(TConfig));

            Type configType = typeof(TConfig);
            IConfigProvider configProvider = this.GetProviderForConfigType(configType);
            IConfigAccessor accessorProvider = this.GetAccessorProviderByInterface(configProvider.RequiredAccessorInterface);

            (Token nOwner, Token nRead, Token nWrite) = this.TokenService.CreateTokenSet(owner, read, write);

            return accessorProvider.CreateFor<TConfig>(this.TokenService, this, nOwner, nRead, nWrite);
        }
        public IConfigAccessorFor<TConfig> GetAccessor<TConfig>(ITokenSet tokenSet) where TConfig : class
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetAccessor<TConfig>(tokenSet.Owner, tokenSet.Read, tokenSet.Write);
        }

        public TAccessor GetAccessor<TAccessor, TConfig>(Token? owner = null, Token? read = null, Token? write = null)
            where TConfig : class
            where TAccessor : IConfigAccessorFor<TConfig>
        {
            IConfigAccessorFor<TConfig> defaultAccessor = this.GetAccessor<TConfig>(owner, read, write);

            bool accessorIsCorrectType = defaultAccessor is TAccessor;

            if (accessorIsCorrectType)
            {
                return (TAccessor)defaultAccessor;
            }
            else
            {
                this.Logger.LogError("Accessor type mismatch: expected {ExpectedType}, got {ActualType}", typeof(TAccessor).Name, defaultAccessor.GetType().Name);

                throw new InvalidCastException($"Accessor for {typeof(TConfig).Name} is a {defaultAccessor.GetType().Name} and cannot be cast to {typeof(TAccessor).Name}");
            }
        }
        public TAccessor GetAccessor<TAccessor, TConfig>(ITokenSet tokenSet)
            where TConfig : class
            where TAccessor : IConfigAccessorFor<TConfig>
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetAccessor<TAccessor, TConfig>(tokenSet.Owner, tokenSet.Read, tokenSet.Write);
        }

        public IConfigAccessor GetAccessor(Type accessorInterface, IEnumerable<Type> configTypes, Token? owner = null, Token? read = null, Token? write = null)
        {
            ArgumentNullException.ThrowIfNull(accessorInterface);
            ArgumentNullException.ThrowIfNull(configTypes);

            IConfigAccessor accessorProvider = this.GetAccessorProviderByInterface(accessorInterface);

            (Token nOwner, Token nRead, Token nWrite) = this.TokenService.CreateTokenSet(owner, read, write);

            return accessorProvider
                .SetConfigTypes([.. configTypes])
                .SetConfigService(this)
                .SetAccess(nOwner, nRead, nWrite);
        }
        public IConfigAccessor GetAccessor(Type accessorInterface, IEnumerable<Type> configTypes, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetAccessor(accessorInterface, configTypes, tokenSet.Owner, tokenSet.Read, tokenSet.Write);
        }

        #endregion

        #region ConfigService: Predicate Operations

        public bool IsRegistered(Type configType)
        {
            return this.ProvidersByConfigType.ContainsKey(configType);
        }

        #endregion

        #region ConfigService: Registration Operations

        public void RegisterConfig(Type configType, IConfigServiceParams configParams)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(configParams);

            Type paramType = configParams.GetType();
            bool hasProvider = this.ProvidersByParamsType.TryGetValue(paramType, out IConfigProvider? provider);

            if (!hasProvider || provider == null)
            {
                this.Logger.LogError("No config implementation registered for parameter type: {ParamType}", paramType.Name);

                throw new InvalidOperationException($"No config implementation registered for parameter type {paramType.Name}");
            }

            bool registrationSucceeded = this.ProvidersByConfigType.TryAdd(configType, provider!);

            if (!registrationSucceeded)
            {
                this.Logger.LogError("Configuration type already registered: {ConfigType}", configType.Name);

                throw new InvalidOperationException($"{configType.Name} was already registered.");
            }

            provider.RegisterConfig(configType, configParams, this);

            this.Logger.LogDebug("Registered configuration: {ConfigType} with {ParamType}", configType.Name, paramType.Name);
        }
        public void RegisterConfig<TConfig>(IConfigServiceParams configParams, out IConfigAccessorFor<TConfig> accessor) where TConfig : class
        {
            ArgumentNullException.ThrowIfNull(configParams);

            this.RegisterConfig(typeof(TConfig), configParams);

            accessor = this.GetAccessor<TConfig>(configParams.Owner, configParams.Read, configParams.Write);
        }
        public void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams)
        {
            ArgumentNullException.ThrowIfNull(configTypes);
            ArgumentNullException.ThrowIfNull(configParams);

            foreach (Type configType in configTypes)
                this.RegisterConfig(configType, configParams);

            this.Logger.LogInformation("Bulk registered {Count} configuration types", this.ProvidersByConfigType.Count);
        }
        public void RegisterConfigs(IEnumerable<Type> configTypes, IConfigServiceParams configParams, out IConfigAccessor accessor)
        {
            ArgumentNullException.ThrowIfNull(configTypes);
            ArgumentNullException.ThrowIfNull(configParams);

            foreach (Type configType in configTypes)
                this.RegisterConfig(configType, configParams);

            Type paramType = configParams.GetType();
            IConfigProvider provider = this.GetProviderForParamsType(paramType);

            accessor = this.GetAccessor(provider.RequiredAccessorInterface, configTypes, configParams.Owner, configParams.Read, configParams.Write);
        }

        public bool TryRegisterConfig(Type configType, IConfigServiceParams configParams)
        {
            bool success = false;

            try
            {
                this.RegisterConfig(configType, configParams);

                success = true;
            }
            catch { /* nothing to see here */ }

            return success;
        }
        public bool TryRegisterConfig<TConfig>(IConfigServiceParams configParams, out IConfigAccessorFor<TConfig>? accessor) where TConfig : class
        {
            bool success = false;

            try
            {
                this.RegisterConfig(configParams, out accessor);

                success = true;
            }
            catch
            {
                accessor = null;
            }

            return success;
        }

        public void UnregisterConfig(Type configType, Token? token = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            bool hasRegistration = this.ProvidersByConfigType.TryGetValue(configType, out IConfigProvider? provider);

            if (!hasRegistration)
            {
                this.Logger.LogWarning("Attempted to unregister non-existent configuration type: {ConfigType}", configType.Name);

                throw new KeyNotFoundException($"Type configuration for {configType.Name} was not registered.");
            }

            provider?.UnregisterConfig(configType, token);

            bool removalSucceeded = this.ProvidersByConfigType.TryRemove(configType, out IConfigProvider? _);

            if (removalSucceeded)
                this.Logger.LogDebug("Unregistered configuration: {ConfigType}", configType.Name);
        }
        public void UnregisterConfig(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.UnregisterConfig(configType, tokenSet.Owner);
        }
        public void UnregisterConfigs(IEnumerable<Type> configTypes, Token? token = null)
        {
            ArgumentNullException.ThrowIfNull(configTypes);

            foreach (Type configType in configTypes)
                this.UnregisterConfig(configType, token);

            this.Logger.LogInformation("Bulk unregistered configuration types");
        }
        public void UnregisterConfigs(IEnumerable<Type> configTypes, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(configTypes);
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.UnregisterConfigs(configTypes, tokenSet.Owner);
        }

        public bool TryUnregsterConfig(Type configType, Token? token = null)
        {
            bool success = false;

            try
            {
                this.UnregisterConfig(configType, token);

                success = true;
            }
            catch { /* nothing to see here */ }

            return success;
        }
        public bool TryUnregsterConfig(Type configType, ITokenSet tokenSet)
        {
            bool success = false;

            try
            {
                this.UnregisterConfig(configType, tokenSet);

                success = true;
            }
            catch { /* nothing to see here */ }

            return success;
        }

        #endregion

        #region ConfigService: Value Operations

        public T GetValue<T>(Type configType, string key, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            return this.GetProviderForConfigType(configType).GetValue<T>(configType, key, ownerToken, readToken);
        }
        public T GetValue<T>(Type configType, string key, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetValue<T>(configType, key, tokenSet.Owner, tokenSet.Read);
        }

        public void SetValue<T>(Type configType, string key, T newValue, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            this.GetProviderForConfigType(configType).SetValue(configType, key, newValue, ownerToken, writeToken);
        }
        public void SetValue<T>(Type configType, string key, T newValue, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SetValue(configType, key, newValue, tokenSet.Owner, tokenSet.Write);
        }

        public void SaveValues(Type configType, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            this.GetProviderForConfigType(configType).SaveValues(configType, ownerToken, writeToken);
        }
        public void SaveValues(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SaveValues(configType, tokenSet.Owner, tokenSet.Write);
        }

        public async Task SaveValuesAsync(Type configType, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configType);

            await this.GetProviderForConfigType(configType).SaveValuesAsync(configType, ownerToken, writeToken, cancellationToken).ConfigureAwait(false);
        }
        public async Task SaveValuesAsync(Type configType, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            await this.SaveValuesAsync(configType, tokenSet.Owner, tokenSet.Write, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region ConfigService: Instance Operations

        public virtual object GetConfigInstance(Type configType, Token? ownerToken = null, Token? readToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);

            return this.GetProviderForConfigType(configType).GetConfigInstance(configType, ownerToken, readToken);
        }
        public virtual object GetConfigInstance(Type configType, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            return this.GetConfigInstance(configType, tokenSet.Owner, tokenSet.Read);
        }

        public virtual void SaveConfigInstance(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(updatedConfig);

            this.GetProviderForConfigType(configType).SaveConfigInstance(configType, updatedConfig, ownerToken, writeToken);
        }
        public virtual void SaveConfigInstance(Type configType, object updatedConfig, ITokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            this.SaveConfigInstance(configType, updatedConfig, tokenSet.Owner, tokenSet.Write);
        }

        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, Token? ownerToken = null, Token? writeToken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(updatedConfig);

            await this.GetProviderForConfigType(configType).SaveConfigInstanceAsync(configType, updatedConfig, ownerToken, writeToken, cancellationToken).ConfigureAwait(false);
        }
        public virtual async Task SaveConfigInstanceAsync(Type configType, object updatedConfig, ITokenSet tokenSet, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(tokenSet);

            await this.SaveConfigInstanceAsync(configType, updatedConfig, tokenSet.Owner, tokenSet.Write, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region ConfigService: Event Handlers

        public virtual void OnSaveOperationComplete(object sender, Type configType)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(configType);

            this.SyncSaveCompleted?.Invoke(sender, new ConfigServiceSaveCompletedEventArgs(configType));
        }
        public virtual void OnSaveOperationError(object sender, Exception ex, ConfigSaveOperation operation, Type configType)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(ex);
            ArgumentNullException.ThrowIfNull(configType);

            this.Logger.LogError(ex, "Configuration save operation failed: {Operation} for {ConfigType}", operation, configType.Name);

            this.SyncSaveErrors?.Invoke(sender, new ConfigServiceSaveErrorEventArgs(ex, operation, configType));
        }

        public virtual void OnSettingChanged(object sender, Type configType, string key, object? oldValue, object? newValue)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(configType);
            ArgumentNullException.ThrowIfNull(key);

            this.Logger.LogDebug("Setting changed: {ConfigType}.{Key} from {OldValue} to {NewValue}",
                configType.Name, key, oldValue, newValue);

            this.SettingChanged?.Invoke(sender, new ConfigServiceSettingChangeEventArgs(configType, key, oldValue, newValue));
        }

        public virtual void OnConfigReloaded(object sender, Type configType)
        {
            ArgumentNullException.ThrowIfNull(sender);
            ArgumentNullException.ThrowIfNull(configType);

            this.Logger.LogDebug("Configuration reloaded: {ConfigType}", configType.Name);

            this.ConfigReloaded?.Invoke(sender, new ConfigServiceConfigReloadedEventArgs(configType));
        }

        #endregion

        #region ConfigService: Resource Management

        public virtual void Dispose()
        {
            if (this.IsDisposed)
                return;

            this.DisposeProviders();
            this.IsDisposed = true;

            GC.SuppressFinalize(this);

            this.Logger.LogDebug("ConfigService disposed");
        }
        private void DisposeProviders()
        {
            foreach (KeyValuePair<Type, IConfigProvider> kvp in this.ProvidersByParamsType)
            {
                try
                {
                    ((IDisposable)kvp.Value).Dispose();
                }
                catch (Exception ex)
                {
                    this.Logger.LogWarning(ex, "Error disposing provider for {ProviderType}", kvp.Key.Name);
                }
            }
        }

        #endregion

        #region ConfigService: Provider and Accessor Management

        private void RegisterConfigServiceProviders(IEnumerable<IConfigProvider> providers)
        {
            ArgumentNullException.ThrowIfNull(providers);

            foreach (IConfigProvider provider in providers)
            {
                foreach (Type paramType in provider.SupportedParamsTypes)
                {
                    bool hasExistingProvider = this.ProvidersByParamsType.ContainsKey(paramType);

                    if (hasExistingProvider)
                    {
                        this.Logger.LogError("Multiple implementations claim support for parameter type: {ParamType}", paramType.Name);

                        throw new InvalidOperationException($"Multiple implementations claim support for {paramType.Name}");
                    }

                    this.ProvidersByParamsType[paramType] = provider;
                }
            }
        }

        private void RegisterConfigAccessors(IEnumerable<IConfigAccessor> accessors)
        {
            ArgumentNullException.ThrowIfNull(accessors);

            foreach (IConfigAccessor accessor in accessors)
            {
                bool hasExistingAccessor = this.AccessorByInterface.ContainsKey(accessor.AccessorInterface);

                if (hasExistingAccessor)
                {
                    this.Logger.LogError("Multiple implementations claim support for accessor interface: {AccessorInterface}", accessor.AccessorInterface.Name);

                    throw new InvalidOperationException($"Multiple implementations claim support for {accessor.AccessorInterface}");
                }

                this.AccessorByInterface[accessor.AccessorInterface] = accessor;
            }
        }

        #endregion

        #region ConfigService: Validation and Helpers

        protected IConfigProvider GetProviderForParamsType(Type paramType)
        {
            ArgumentNullException.ThrowIfNull(paramType);

            bool hasProvider = this.ProvidersByParamsType.TryGetValue(paramType, out IConfigProvider? provider);

            if (!hasProvider)
            {
                this.Logger.LogError("No provider registered for parameter type: {ParamType}", paramType.Name);

                throw new KeyNotFoundException($"Parameters for {paramType.Name} is not registered.");
            }

            return provider!;
        }
        protected IConfigProvider GetProviderForConfigType(Type configType)
        {
            ArgumentNullException.ThrowIfNull(configType);

            bool hasProvider = this.ProvidersByConfigType.TryGetValue(configType, out IConfigProvider? provider);

            if (!hasProvider)
            {
                this.Logger.LogError("No provider registered for configuration type: {ConfigType}", configType.Name);

                throw new KeyNotFoundException($"Configuration for {configType.Name} is not registered.");
            }

            return provider!;
        }

        protected IConfigAccessor GetAccessorProviderByInterface(Type accessorInterface)
        {
            ArgumentNullException.ThrowIfNull(accessorInterface);

            bool hasAccessor = this.AccessorByInterface.TryGetValue(accessorInterface, out IConfigAccessor? accessorProvider);

            if (!hasAccessor)
            {
                this.Logger.LogError("No accessor registered for interface: {AccessorInterface}", accessorInterface.Name);

                throw new InvalidOperationException($"No accessor registered for interface {accessorInterface.Name}");
            }

            return accessorProvider!;
        }

        #endregion
    }
}