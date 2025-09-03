using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Models;
using PlugHub.Services.Configuration;
using PlugHub.Services.Plugins;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration;
using PlugHub.Shared.Models.Plugins;
using PlugHub.Shared.Utility;
using PlugHub.ViewModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PlugHub.Bootstrap
{
    internal class Bootstrapper
    {
        public static (IServiceProvider, AppConfig) BuildEnv(IServiceCollection services, IConfigService configService, TokenSet tokenSet, AppConfig baseConfig)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(baseConfig);

            IServiceProvider provider;

            CollectServices(services, tokenSet);
            CollectViewModels(services);

            PluginManifest baseManifest = GetPluginManifest(configService, tokenSet, AppContext.BaseDirectory);

            IEnumerable<PluginReference> plugins = [];

            if (baseConfig.PluginFolderPath != null)
                plugins = RegisterPlugins(services, baseManifest, baseConfig.PluginFolderPath, plugins);

            provider = services.BuildServiceProvider();

            AppConfig userConfig = PluginsAppConfig(provider, baseConfig);

            PluginManifest userManifest =
                ResolvePluginManifest(configService, provider, tokenSet, userConfig, baseManifest);

            if (userConfig.PluginFolderPath != null)
                plugins = RegisterPlugins(services, userManifest, userConfig.PluginFolderPath, plugins);

            services.AddSingleton<IPluginCache>(new PluginCache(plugins));

            SaveAppConfig(configService, tokenSet, userConfig);
            SavePluginManifest(configService, tokenSet, userConfig, userManifest);

            services.AddSingleton(ConfigService.GetInstance(services, userConfig));

            provider = services.BuildServiceProvider();

            PluginsConfigs(provider, configService);
            PluginAppServices(provider, baseConfig);

            return (provider, userConfig);
        }

        #region Bootstrapper: Service Additions

        private static void CollectServices(IServiceCollection services, TokenSet tokenSet)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<IPluginResolver, PluginResolver>();
            services.AddSingleton<IPluginService, PluginService>();
            services.AddSingleton<IPluginRegistrar>(provider =>
            {
                ILogger<IPluginRegistrar> logger = provider.GetRequiredService<ILogger<IPluginRegistrar>>();
                IPluginService pluginService = provider.GetRequiredService<IPluginService>();
                IPluginCache pluginCache = provider.GetRequiredService<IPluginCache>();
                IConfigService configService = provider.GetRequiredService<IConfigService>();

                configService.RegisterConfig(
                    new FileConfigServiceParams(Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                    out IConfigAccessorFor<PluginManifest> accessor);

                return new PluginRegistrar(logger, accessor, pluginService, pluginCache);
            });
        }
        private static void CollectViewModels(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
        }

        #endregion

        private static IEnumerable<PluginReference> RegisterPlugins(IServiceCollection services, PluginManifest? pluginManifest, string pluginFolderPath, IEnumerable<PluginReference> pluginReferences)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pluginFolderPath);

            IEnumerable<PluginReference> enabled = [];

            using (ServiceProvider tempProvider = services.BuildServiceProvider())
            {
                ILogger<IPluginRegistrar> logger = tempProvider.GetRequiredService<ILogger<IPluginRegistrar>>();

                IPluginService pluginService = tempProvider.GetRequiredService<IPluginService>();
                IPluginResolver pluginSorter = tempProvider.GetRequiredService<IPluginResolver>();

                if (!pluginReferences.Any())
                    pluginReferences = pluginService.Discover(pluginFolderPath);

                if (pluginManifest != null)
                    enabled = GetEnabledInterfaces(logger, pluginManifest, pluginReferences);

                RegisterInjectors(logger, pluginService, pluginSorter, services, enabled);
                RegisterPlugins(logger, services, enabled);

                return pluginReferences;
            }
        }

        #region Bootstrapper: Interface Consumers

        private static AppConfig PluginsAppConfig(IServiceProvider provider, AppConfig appConfig)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(appConfig);

            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginAppConfig> brandingPlugins = provider.GetServices<IPluginAppConfig>();

            List<PluginAppConfigDescriptor> allDescriptors = [];

            foreach (IPluginAppConfig brandingPlugin in brandingPlugins)
            {
                IEnumerable<PluginAppConfigDescriptor> descriptors = brandingPlugin.GetAppConfigDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            PluginAppConfigDescriptor[] reverseSortedDescriptors = [.. pluginResolver.ResolveDescriptors(allDescriptors).Reverse()];

            foreach (PluginAppConfigDescriptor descriptor in reverseSortedDescriptors)
            {
                try
                {
                    descriptor.AppConfiguration?.Invoke(appConfig);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to apply configuration branding for plugin {PluginID}", descriptor.PluginID);
                }
            }

            return appConfig;
        }
        private static void PluginAppServices(IServiceProvider provider, AppConfig appConfig)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(appConfig);

            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginAppConfig> brandingPlugins = provider.GetServices<IPluginAppConfig>();

            List<PluginAppConfigDescriptor> allDescriptors = [];

            foreach (IPluginAppConfig brandingPlugin in brandingPlugins)
            {
                IEnumerable<PluginAppConfigDescriptor> descriptors = brandingPlugin.GetAppConfigDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            PluginAppConfigDescriptor[] reverseSortedDescriptors = [.. pluginResolver.ResolveDescriptors(allDescriptors).Reverse()];

            foreach (PluginAppConfigDescriptor descriptor in reverseSortedDescriptors)
            {
                try
                {
                    descriptor.AppServices?.Invoke(provider);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to apply service branding for plugin {PluginID}", descriptor.PluginID);
                }
            }
        }
        private static void PluginsConfigs(IServiceProvider provider, IConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(configService);

            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            ITokenService tokenService = provider.GetRequiredService<ITokenService>();

            List<PluginConfigurationDescriptor> allDescriptors = [];

            IEnumerable<IPluginConfiguration> configurationPlugins = provider.GetServices<IPluginConfiguration>();

            foreach (IPluginConfiguration configurationPlugin in configurationPlugins)
            {
                IEnumerable<PluginConfigurationDescriptor> descriptors = configurationPlugin.GetConfigurationDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            PluginConfigurationDescriptor[] sortedDescriptors = [.. pluginResolver.ResolveDescriptors(allDescriptors)];

            foreach (PluginConfigurationDescriptor descriptor in sortedDescriptors)
            {
                configService.RegisterConfig(descriptor.ConfigType, descriptor.ConfigServiceParams(tokenService));
            }
        }

        #endregion

        #region Bootstrapper: Configuration Handlers

        private static PluginManifest ResolvePluginManifest(IConfigService configService, IServiceProvider provider, TokenSet tokenSet, AppConfig appConfig, PluginManifest baseManifest)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(appConfig);
            ArgumentNullException.ThrowIfNull(baseManifest);

            ILogger<IPluginRegistrar> logger = provider.GetRequiredService<ILogger<IPluginRegistrar>>();
            IPluginService pluginService = provider.GetRequiredService<IPluginService>();

            PluginManifest userManifest = new();
            IEnumerable<PluginReference> discoveredPlugins = [];
            HashSet<(Guid PluginId, string InterfaceName)> mergedKeys = [];
            List<PluginLoadState> mergedStates = [];

            if (!string.IsNullOrWhiteSpace(appConfig.ConfigDirectory))
                userManifest = GetPluginManifest(configService, tokenSet, appConfig.ConfigDirectory);

            if (!string.IsNullOrWhiteSpace(appConfig.PluginFolderPath))
                discoveredPlugins = pluginService.Discover(appConfig.PluginFolderPath);

            Dictionary<(Guid PluginId, string InterfaceName), PluginLoadState> userStatesByKey =
                userManifest.InterfaceStates.ToDictionary(s => (s.PluginId, s.InterfaceName), s => s);

            foreach (PluginLoadState systemState in baseManifest.InterfaceStates)
            {
                (Guid PluginId, string InterfaceName) key =
                    (systemState.PluginId, systemState.InterfaceName);

                if (userStatesByKey.TryGetValue(key, out PluginLoadState? userState))
                {
                    userState.AssemblyName = systemState.AssemblyName;
                    userState.ImplementationName = systemState.ImplementationName;
                    userState.Enabled = systemState.Enabled;
                    userState.LoadOrder = systemState.LoadOrder;
                    userState.System = true;

                    mergedStates.Add(userState);
                }
                else
                {
                    PluginLoadState imposedCopy = new(
                        systemState.PluginId,
                        systemState.AssemblyName,
                        systemState.ImplementationName,
                        systemState.InterfaceName,
                        true,
                        systemState.Enabled,
                        systemState.LoadOrder);

                    mergedStates.Add(imposedCopy);

                    logger.LogInformation("Added missing default plugin state for {InterfaceName}", systemState.InterfaceName);
                }

                mergedKeys.Add(key);
            }

            foreach (KeyValuePair<(Guid PluginId, string InterfaceName), PluginLoadState> kvp in userStatesByKey)
                if (!mergedKeys.Contains(kvp.Key))
                    mergedStates.Add(kvp.Value);

            userManifest.InterfaceStates = mergedStates;

            return SynchronizePluginConfig(logger, userManifest, discoveredPlugins);
        }
        private static void SaveAppConfig(IConfigService configService, TokenSet tokenSet, AppConfig appConfig)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(appConfig);
            ArgumentNullException.ThrowIfNull(tokenSet);

            JsonSerializerOptions options = new() { WriteIndented = false };

            string configPath = Path.Combine(appConfig.ConfigDirectory ?? AppContext.BaseDirectory, "AppConfig.json");

            configService.RegisterConfig(
                new FileConfigServiceParams(configPath, Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                out IConfigAccessorFor<AppConfig>? accessor);

            AppConfig persistAppConfig = accessor.Get();

            string appLocalJson = JsonSerializer.Serialize(appConfig, options);
            string appPersistJson = JsonSerializer.Serialize(persistAppConfig, options);

            if (appLocalJson != appPersistJson)
                accessor.Save(appConfig);
        }
        private static void SavePluginManifest(IConfigService configService, TokenSet tokenSet, AppConfig appConfig, PluginManifest pluginManifest)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(appConfig);
            ArgumentNullException.ThrowIfNull(pluginManifest);

            JsonSerializerOptions options = new() { WriteIndented = false };

            string configPath = Path.Combine(appConfig.ConfigDirectory ?? AppContext.BaseDirectory, "PluginManifest.json");

            configService.RegisterConfig(
                new FileConfigServiceParams(configPath, Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                out IConfigAccessorFor<PluginManifest>? accessor);

            PluginManifest persistManifest = accessor.Get();

            string manifestLocalJson = JsonSerializer.Serialize(pluginManifest, options);
            string manifestPersistJson = JsonSerializer.Serialize(persistManifest, options);

            if (manifestLocalJson != manifestPersistJson)
                accessor.Save(pluginManifest);
        }

        #endregion

        private static PluginManifest GetPluginManifest(IConfigService configService, TokenSet tokenSet, string directory)
        {
            PluginManifest? pluginManifest = new();

            string pluginFilePath = Path.Combine(directory, "PluginManifest.json");

            if (PathUtilities.ExistsOsAware(pluginFilePath))
            {
                if (configService.IsConfigRegistered(typeof(PluginManifest)))
                    configService.UnregisterConfig(typeof(PluginManifest), tokenSet);

                configService.RegisterConfig(
                    new FileConfigServiceParams(pluginFilePath, Owner: tokenSet.Owner),
                    out IConfigAccessorFor<PluginManifest>? accessor);

                pluginManifest = accessor?.Get() ?? new PluginManifest();

                configService.UnregisterConfig(typeof(PluginManifest), tokenSet);
            }

            return pluginManifest;
        }

        #region Bootstrapper: Bootstrap Plugins

        public static PluginManifest SynchronizePluginConfig(ILogger<IPluginRegistrar> logger, PluginManifest pluginManifest, IEnumerable<PluginReference> plugins)
        {
            ArgumentNullException.ThrowIfNull(pluginManifest);

            plugins ??= [];
            pluginManifest.InterfaceStates ??= [];

            logger.LogInformation("Synchronizing plugin config. Current entries: {EntryCount}", pluginManifest.InterfaceStates.Count);

            HashSet<(string AssemblyName, string ImplementationName, string InterfaceName)> discoveredSet =
                new(plugins.SelectMany(plugin =>
                        plugin.Interfaces.Select(pluginInterface =>
                            (pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName))),
                EqualityComparer<(string, string, string)>.Default);

            bool newEntriesAdded = AddNewPluginEntries(logger, pluginManifest, plugins);
            bool staleEntriesRemoved = RemoveStalePluginEntries(logger, pluginManifest, discoveredSet, ignoreDefault: true);

            if (newEntriesAdded || staleEntriesRemoved)
                logger.LogInformation("Plugin config changes detected and saved.");
            else
                logger.LogInformation("No plugin config changes detected.");

            return pluginManifest;
        }
        public static IEnumerable<PluginReference> GetEnabledInterfaces(ILogger<IPluginRegistrar> logger, PluginManifest pluginData, IEnumerable<PluginReference> plugins)
        {
            List<PluginReference> result = [];

            foreach (PluginReference plugin in plugins)
            {
                logger.LogDebug("Processing plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                List<PluginInterface> enabledImplementations = FindEnabledImplementations(logger, pluginData, plugin);

                bool hasEnabledImplementations = enabledImplementations.Count > 0;

                if (hasEnabledImplementations)
                {
                    logger.LogInformation("Plugin {AssemblyName}:{TypeName} has {EnabledCount} enabled implementations.", plugin.AssemblyName, plugin.TypeName, enabledImplementations.Count);

                    result.Add(new PluginReference(plugin.Assembly, plugin.Type, plugin.Metadata, enabledImplementations));
                }
            }

            logger.LogInformation("Found {PluginCount} plugins with enabled implementations.", result.Count);

            return result;
        }
        public static void RegisterInjectors(ILogger<IPluginRegistrar> logger, IPluginService pluginService, IPluginResolver pluginSorter, IServiceCollection serviceCollection, IEnumerable<PluginReference> enabledPlugins)
        {
            Dictionary<Type, List<PluginInjectorDescriptor>> descriptorsByInterface = [];

            foreach (PluginReference plugin in enabledPlugins)
            {
                logger.LogInformation("Registering plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                foreach (PluginInterface implementation in plugin.Interfaces)
                {
                    bool isInjector = implementation.InterfaceType == typeof(IPluginDependencyInjection);

                    if (!isInjector) continue;

                    IPluginDependencyInjection? injector = pluginService.GetLoadedInterface<IPluginDependencyInjection>(implementation);

                    if (injector == null)
                    {
                        logger.LogWarning("Failed to create injector instance for {TypeName} in {AssemblyName}.", implementation.ImplementationName, implementation.AssemblyName);

                        continue;
                    }

                    foreach (PluginInjectorDescriptor descriptor in injector.GetInjectionDescriptors())
                    {
                        if (!descriptorsByInterface.TryGetValue(descriptor.InterfaceType, out List<PluginInjectorDescriptor>? list))
                            descriptorsByInterface[descriptor.InterfaceType] = list = [];

                        list.Add(descriptor);
                    }
                }
            }

            foreach (KeyValuePair<Type, List<PluginInjectorDescriptor>> kvp in descriptorsByInterface)
            {
                Type interfaceType = kvp.Key;
                List<PluginInjectorDescriptor> descriptors = kvp.Value;
                List<PluginInjectorDescriptor> reverseSorted = [.. pluginSorter.ResolveDescriptors(descriptors).Reverse()];

                foreach (PluginInjectorDescriptor descriptor in reverseSorted)
                {
                    bool hasImplementationType = descriptor.ImplementationType != null;
                    bool hasBothTypes = hasImplementationType && descriptor.Instance != null;
                    bool hasNeitherType = !hasImplementationType && descriptor.Instance == null;

                    if (hasNeitherType)
                    {
                        logger.LogWarning("Descriptor for {InterfaceType} must specify either ImplementationType or Instance; skipping malformed registration.", descriptor.InterfaceType.Name);

                        continue;
                    }
                    else if (hasBothTypes)
                    {
                        logger.LogWarning("Descriptor for {InterfaceType} specifies both ImplementationType and Instance; must specify only one; skipping ambiguous registration.", descriptor.InterfaceType.Name);

                        continue;
                    }
                    else if (descriptor.Instance != null)
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.Instance));
                    else
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.ImplementationType!, descriptor.Lifetime));
                }
            }
        }
        public static void RegisterPlugins(ILogger<IPluginRegistrar> logger, IServiceCollection serviceCollection, IEnumerable<PluginReference> enabledPlugins)
        {
            foreach (PluginReference plugin in enabledPlugins)
            {
                logger.LogInformation("Registering general plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                foreach (PluginInterface implementation in plugin.Interfaces)
                {
                    logger.LogInformation("Registered {Interface} directly via {TypeName}.", implementation.InterfaceName, implementation.ImplementationName);

                    serviceCollection.AddSingleton(implementation.InterfaceType, implementation.ImplementationType);
                }
            }
        }

        #endregion

        #region Bootstrapper: Bootstrap Helper Methods

        private static bool AddNewPluginEntries(ILogger<IPluginRegistrar> logger, PluginManifest pluginData, IEnumerable<PluginReference> plugins)
        {
            bool configChanged = false;

            foreach (PluginReference plugin in plugins)
            {
                foreach (PluginInterface pluginInterface in plugin.Interfaces)
                {
                    bool entryExists = pluginData.InterfaceStates.Any(
                        loadState =>
                            loadState.PluginId == plugin.Metadata.PluginID &&
                            loadState.AssemblyName == pluginInterface.AssemblyName &&
                            loadState.ImplementationName == pluginInterface.ImplementationName &&
                            loadState.InterfaceName == pluginInterface.InterfaceName
                    );

                    if (!entryExists)
                    {
                        PluginLoadState newEntry = new(
                            plugin.Metadata.PluginID,
                            pluginInterface.AssemblyName,
                            pluginInterface.ImplementationName,
                            pluginInterface.InterfaceName,
                            false,
                            false,
                            int.MaxValue);

                        pluginData.InterfaceStates.Add(newEntry);

                        configChanged = true;

                        logger.LogInformation("Added new config entry for {AssemblyName}:{TypeName}:{ImplementationName}", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
                    }
                }
            }

            return configChanged;
        }
        private static bool RemoveStalePluginEntries(ILogger<IPluginRegistrar> logger, PluginManifest pluginManifest, HashSet<(string AssemblyName, string ImplementationName, string InterfaceName)> discoveredSet, bool ignoreDefault)
        {
            bool removedAny = false;

            for (int i = pluginManifest.InterfaceStates.Count - 1; i >= 0; i--)
            {
                PluginLoadState state = pluginManifest.InterfaceStates[i];

                if (ignoreDefault && state.System)
                    continue;

                (string AssemblyName, string ImplementationName, string InterfaceName) key =
                    (state.AssemblyName, state.ImplementationName, state.InterfaceName);

                if (!discoveredSet.Contains(key))
                {
                    logger.LogInformation("Removing stale plugin entry {InterfaceName}", state.InterfaceName);

                    pluginManifest.InterfaceStates.RemoveAt(i);

                    removedAny = true;
                }
            }
            return removedAny;
        }

        private static List<PluginInterface> FindEnabledImplementations(ILogger<IPluginRegistrar> logger, PluginManifest pluginData, PluginReference plugin)
        {
            List<PluginInterface> enabledImplementations = [];

            foreach (PluginInterface iface in plugin.Interfaces)
            {
                foreach (PluginLoadState configEntry in pluginData.InterfaceStates)
                {
                    bool assemblyMatch = string.Equals(configEntry.AssemblyName, iface.AssemblyName, StringComparison.OrdinalIgnoreCase);
                    bool implementationMatch = string.Equals(configEntry.ImplementationName, iface.ImplementationName, StringComparison.OrdinalIgnoreCase);
                    bool interfaceMatch = string.Equals(configEntry.InterfaceName, iface.InterfaceName, StringComparison.OrdinalIgnoreCase);

                    if (assemblyMatch && implementationMatch && interfaceMatch && configEntry.Enabled)
                    {
                        enabledImplementations.Add(iface);

                        logger.LogDebug("Enabled implementation {AssemblyName}:{TypeName}:{ImplementationName}", iface.AssemblyName, iface.ImplementationName, iface.InterfaceName);

                        break;
                    }
                }
            }

            return enabledImplementations;
        }

        #endregion
    }
}