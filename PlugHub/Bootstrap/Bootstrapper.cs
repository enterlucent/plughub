using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Models;
using PlugHub.Services.Configuration;
using PlugHub.Services.Plugins;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Interfaces.Services.Configuration;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models;
using PlugHub.Shared.Models.Configuration.Parameters;
using PlugHub.Shared.Models.Plugins;
using PlugHub.Shared.Utility;
using PlugHub.Shared.ViewModels;
using PlugHub.ViewModels;
using PlugHub.ViewModels.Pages;
using PlugHub.Views;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlugHub.Bootstrap
{
    internal class Bootstrapper
    {
        public static IServiceProvider BuildEnv(IServiceCollection services, IConfigService baseConfigService, TokenSet tokenSet, AppConfig baseConfig, AppEnv baseEnv)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(baseConfigService);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(baseConfig);
            ArgumentNullException.ThrowIfNull(baseEnv);

            PluginManifest baseManifest;
            AppConfig sysAppConfig;
            AppEnv sysAppEnv;
            PluginManifest pluginManifest;
            AppEnv userAppEnv;

            CollectServices(services, tokenSet);
            CollectViewModels(services);

            // Initialize an empty plugin reference collection
            IEnumerable<PluginReference> plugins = [];

            // Build a temporary DI provider (to resolve things like logging, services, etc.)
            using (ServiceProvider tempProvider = services.BuildServiceProvider())
            {
                // Load the *base PluginManifest* (system-level plugin config) from disk if it exists in AppContext.BaseDirectory
                baseManifest = GetPluginManifest(baseConfigService, tokenSet, AppContext.BaseDirectory);

                // If the base AppConfig specifies a plugin folder, discover & register those plugins
                if (!string.IsNullOrWhiteSpace(baseConfig.PluginDirectory))
                    plugins = RegisterPlugins(tempProvider, services, baseManifest, baseConfig.PluginDirectory, plugins);
            }

            // Build a temporary DI provider including system plugins
            using (ServiceProvider tempProvider = services.BuildServiceProvider())
            {
                // Apply system-plugin-provided AppConfig mutations 
                sysAppConfig = PluginsAppConfig(tempProvider, baseConfig);

                // Apply system-plugin-provided AppEnv mutations 
                sysAppEnv = PluginsAppEnv(tempProvider, baseEnv);

                // Merge user manifest with the base manifest and discovered plugin states
                pluginManifest = ResolvePluginManifest(tempProvider, baseConfigService, tokenSet, sysAppConfig, baseManifest);

                if (!string.IsNullOrWhiteSpace(sysAppConfig.PluginDirectory))
                    plugins = RegisterPlugins(tempProvider, services, pluginManifest, sysAppConfig.PluginDirectory, plugins);

                // Apply user-plugin-provided AppEnv mutations 
                userAppEnv = PluginsAppEnv(tempProvider, sysAppEnv);

                // Persist a cache of the loaded plugin references
                services.AddSingleton<IPluginCache>(new PluginCache(plugins));

                SaveAppConfig(baseConfigService, tokenSet, sysAppConfig);
                SaveAppEnv(baseConfigService, tokenSet, sysAppConfig, userAppEnv);
                SavePluginManifest(baseConfigService, tokenSet, sysAppConfig, pluginManifest);

                // Register a ConfigService instance bound to the plugin AppConfig
                services.AddSingleton(ConfigService.GetInstance(services, sysAppConfig));
            }

            // Build the *final* DI provider including plugins and configs
            IServiceProvider provider = services.BuildServiceProvider();

            PluginsConfigs(provider);
            PluginsStyleInclude(provider);
            PluginsPages(provider);
            PluginsSettingPages(provider);

            PluginAppServices(provider);

            return provider;
        }

        #region Bootstrapper: Service Registration

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
                    new ConfigFileParams(Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                    out IConfigAccessorFor<PluginManifest> accessor);

                return new PluginRegistrar(logger, accessor, pluginService, pluginCache);
            });

            Log.Information("[Bootstrapper] Plugin Services Added");
        }
        private static void CollectViewModels(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<SettingsPluginsView>();
            services.AddSingleton<SettingsPluginsViewModel>();

            Log.Information("[Bootstrapper] Plugin Static UI Added");
        }

        #endregion

        #region Bootstrapper: Config Management

        private static PluginManifest GetPluginManifest(IConfigService configService, TokenSet tokenSet, string directory)
        {
            PluginManifest? pluginManifest = new();

            string pluginFilePath = Path.Combine(directory, "PluginManifest.json");

            if (PlatformPath.Exists(pluginFilePath))
            {
                Log.Information("[Bootstrapper] Plugin manifest found at {PluginManifestPath}, loading.", pluginFilePath);

                configService.RegisterConfig(
                    new ConfigFileParams(pluginFilePath, Owner: tokenSet.Owner),
                    out IConfigAccessorFor<PluginManifest>? accessor);

                pluginManifest = accessor?.Get() ?? new PluginManifest();

                configService.UnregisterConfig(typeof(PluginManifest), tokenSet);
            }

            return pluginManifest;
        }

        private static PluginManifest ResolvePluginManifest(IServiceProvider provider, IConfigService configService, TokenSet tokenSet, AppConfig appConfig, PluginManifest baseManifest)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(appConfig);
            ArgumentNullException.ThrowIfNull(baseManifest);

            IPluginService pluginService = provider.GetRequiredService<IPluginService>();

            PluginManifest userManifest = !string.IsNullOrWhiteSpace(appConfig.ConfigDirectory)
                ? GetPluginManifest(configService, tokenSet, appConfig.ConfigDirectory)
                : new PluginManifest();

            IEnumerable<PluginReference> discoveredPlugins = !string.IsNullOrWhiteSpace(appConfig.PluginDirectory)
                ? pluginService.Discover(appConfig.PluginDirectory)
                : [];

            List<PluginLoadState> mergedStates = MergePluginManifest(baseManifest, userManifest);

            userManifest.InterfaceStates = mergedStates;

            PluginManifest syncedManifest = SynchronizePluginConfig(userManifest, discoveredPlugins);

            NormalizePluginManifest(syncedManifest, baseManifest, pluginService);

            Log.Information("[Bootstrapper] Resolved and synchronized plugin manifest with {PluginCount} interface states and {DiscoveredCount} discovered plugins.", syncedManifest.InterfaceStates?.Count ?? 0, discoveredPlugins.Count());

            return syncedManifest;
        }
        private static List<PluginLoadState> MergePluginManifest(PluginManifest baseManifest, PluginManifest userManifest)
        {
            List<PluginLoadState> mergedStates = [];
            Dictionary<(Guid PluginId, string InterfaceName), PluginLoadState> userStatesByKey = userManifest.InterfaceStates
                .ToDictionary(s => (s.PluginId, s.InterfaceName), s => s);

            HashSet<(Guid, string)> mergedKeys = [];

            foreach (PluginLoadState systemState in baseManifest.InterfaceStates)
            {
                (Guid PluginId, string InterfaceName) key = (systemState.PluginId, systemState.InterfaceName);

                if (userStatesByKey.TryGetValue(key, out PluginLoadState? userState))
                {
                    userState.AssemblyName = systemState.AssemblyName;
                    userState.ClassName = systemState.ClassName;
                    userState.Enabled = systemState.Enabled;
                    userState.LoadOrder = systemState.LoadOrder;
                    userState.System = true;
                    mergedStates.Add(userState);
                }
                else
                {
                    mergedStates.Add(new PluginLoadState(
                        systemState.PluginId,
                        systemState.AssemblyName,
                        systemState.ClassName,
                        systemState.InterfaceName,
                        true,
                        systemState.Enabled,
                        systemState.LoadOrder));
                    Log.Information("[Bootstrapper] Added missing default plugin state for {InterfaceName}", systemState.InterfaceName);
                }

                mergedKeys.Add(key);
            }

            foreach (KeyValuePair<(Guid PluginId, string InterfaceName), PluginLoadState> kvp in userStatesByKey)
            {
                if (!mergedKeys.Contains(kvp.Key))
                    mergedStates.Add(kvp.Value);
            }

            return mergedStates;
        }
        private static void NormalizePluginManifest(PluginManifest syncedManifest, PluginManifest baseManifest, IPluginService pluginService)
        {
            Dictionary<string, DescriptorProviderAttribute?> attributeCache = [];

            foreach (PluginLoadState state in syncedManifest.InterfaceStates)
            {
                if (!attributeCache.TryGetValue(state.InterfaceName, out DescriptorProviderAttribute? dpa))
                {
                    dpa = pluginService.GetDescriptorProviderAttribute(state.InterfaceName);
                    attributeCache[state.InterfaceName] = dpa;
                }

                if (dpa != null)
                {
                    bool isInBaseManifest = baseManifest.InterfaceStates.Any(
                        m => m.PluginId == state.PluginId && m.InterfaceName == state.InterfaceName);

                    if (!isInBaseManifest && dpa.DescriptorIsSystemOnly)
                    {
                        state.Enabled = false;
                        state.System = true;
                    }
                }
            }
        }
        private static PluginManifest SynchronizePluginConfig(PluginManifest pluginManifest, IEnumerable<PluginReference> plugins)
        {
            ArgumentNullException.ThrowIfNull(pluginManifest);

            plugins ??= [];
            pluginManifest.InterfaceStates ??= [];

            Log.Information("[Bootstrapper] Synchronizing plugin config. Current entries: {EntryCount}", pluginManifest.InterfaceStates.Count);

            HashSet<(string AssemblyName, string ImplementationName, string InterfaceName)> discoveredSet =
                new(plugins.SelectMany(plugin =>
                        plugin.Interfaces.Select(pluginInterface =>
                            (pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName))),
                EqualityComparer<(string, string, string)>.Default);

            bool newEntriesAdded = AddNewPluginEntries(pluginManifest, plugins);
            bool staleEntriesRemoved = RemoveStalePluginEntries(pluginManifest, discoveredSet, ignoreDefault: true);

            if (newEntriesAdded || staleEntriesRemoved)
                Log.Information("[Bootstrapper] Plugin config changes detected and saved.");
            else
                Log.Information("[Bootstrapper] No plugin config changes detected.");

            return pluginManifest;
        }

        private static void SaveAppConfig(IConfigService configService, TokenSet tokenSet, AppConfig appConfig)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(appConfig);
            ArgumentNullException.ThrowIfNull(tokenSet);

            JsonSerializerOptions options = new() { WriteIndented = false };

            string configPath = Path.Combine(appConfig.ConfigDirectory ?? AppContext.BaseDirectory, "AppConfig.json");

            configService.RegisterConfig(
                new ConfigFileParams(configPath, Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                out IConfigAccessorFor<AppConfig>? accessor);

            AppConfig persistAppConfig = accessor.Get();

            string appLocalJson = JsonSerializer.Serialize(appConfig, options);
            string appPersistJson = JsonSerializer.Serialize(persistAppConfig, options);

            try
            {
                if (appLocalJson != appPersistJson)
                    Task.Run(() => accessor.SaveAsync(appConfig)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Bootstrapper] Failed to save AppConfig to {ConfigPath}", configPath);
            }
        }
        private static void SaveAppEnv(IConfigService configService, TokenSet tokenSet, AppConfig appConfig, AppEnv appEnv)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(appEnv);
            ArgumentNullException.ThrowIfNull(tokenSet);

            JsonSerializerOptions options = new() { WriteIndented = false };

            string configPath = Path.Combine(appConfig.ConfigDirectory ?? AppContext.BaseDirectory, "AppEnv.json");

            configService.RegisterConfig(
                new ConfigFileParams(configPath, Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                out IConfigAccessorFor<AppEnv>? accessor);

            AppEnv persistAppEnv = accessor.Get();

            string appLocalJson = JsonSerializer.Serialize(appEnv, options);
            string appPersistJson = JsonSerializer.Serialize(persistAppEnv, options);

            try
            {
                if (appLocalJson != appPersistJson)
                    Task.Run(() => accessor.SaveAsync(appEnv)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Bootstrapper] Failed to save AppEnv to {ConfigPath}", configPath);
            }
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
                new ConfigFileParams(configPath, Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
                out IConfigAccessorFor<PluginManifest>? accessor);

            PluginManifest persistManifest = accessor.Get();

            string manifestLocalJson = JsonSerializer.Serialize(pluginManifest, options);
            string manifestPersistJson = JsonSerializer.Serialize(persistManifest, options);

            try
            {
                if (manifestLocalJson != manifestPersistJson)
                    Task.Run(() => accessor.SaveAsync(pluginManifest)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[Bootstrapper] Failed to save PluginManifest to {ConfigPath}", configPath);
            }
        }

        private static bool AddNewPluginEntries(PluginManifest pluginData, IEnumerable<PluginReference> plugins)
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
                            loadState.ClassName == pluginInterface.ImplementationName &&
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

                        Log.Debug("[Bootstrapper] Added new config entry for {AssemblyName}:{TypeName}:{ImplementationName}", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
                    }
                }
            }

            return configChanged;
        }
        private static bool RemoveStalePluginEntries(PluginManifest pluginManifest, HashSet<(string AssemblyName, string ImplementationName, string InterfaceName)> discoveredSet, bool ignoreDefault)
        {
            bool removedAny = false;

            for (int i = pluginManifest.InterfaceStates.Count - 1; i >= 0; i--)
            {
                PluginLoadState state = pluginManifest.InterfaceStates[i];

                if (ignoreDefault && state.System)
                    continue;

                (string AssemblyName, string ImplementationName, string InterfaceName) key =
                    (state.AssemblyName, state.ClassName, state.InterfaceName);

                if (!discoveredSet.Contains(key))
                {
                    Log.Information("[Bootstrapper] Removing stale plugin entry {InterfaceName}", state.InterfaceName);

                    pluginManifest.InterfaceStates.RemoveAt(i);

                    removedAny = true;
                }
            }
            return removedAny;
        }

        #endregion

        #region Bootstrapper: Plugin Registration

        private static IEnumerable<PluginReference> RegisterPlugins(IServiceProvider provider, IServiceCollection services, PluginManifest? pluginManifest, string pluginFolderPath, IEnumerable<PluginReference> pluginReferences)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pluginFolderPath);

            IEnumerable<PluginReference> enabled = [];

            IPluginService pluginService = provider.GetRequiredService<IPluginService>();
            IPluginResolver pluginSorter = provider.GetRequiredService<IPluginResolver>();

            if (!pluginReferences.Any())
                pluginReferences = pluginService.Discover(pluginFolderPath);

            if (pluginManifest != null)
                enabled = GetEnabledInterfaces(pluginManifest, pluginReferences);

            RegisterInjectors(pluginService, pluginSorter, services, enabled);
            RegisterPlugins(services, enabled);

            return pluginReferences;
        }
        private static List<PluginReference> GetEnabledInterfaces(PluginManifest pluginData, IEnumerable<PluginReference> plugins)
        {
            List<PluginReference> result = [];

            foreach (PluginReference plugin in plugins)
            {
                Log.Debug("[Bootstrapper] Processing plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                List<PluginInterface> enabledImplementations = FindEnabledImplementations(pluginData, plugin);

                bool hasEnabledImplementations = enabledImplementations.Count > 0;

                if (hasEnabledImplementations)
                {
                    Log.Information("[Bootstrapper] Plugin {AssemblyName}:{TypeName} has {EnabledCount} enabled implementations.", plugin.AssemblyName, plugin.TypeName, enabledImplementations.Count);

                    result.Add(new PluginReference(plugin.Assembly, plugin.Type, plugin.Metadata, enabledImplementations));
                }
            }

            if (result.Count > 0)
                Log.Information("[Bootstrapper] Found {PluginCount} plugins with enabled implementations.", result.Count);

            return result;
        }
        private static List<PluginInterface> FindEnabledImplementations(PluginManifest pluginData, PluginReference plugin)
        {
            List<PluginInterface> enabledImplementations = [];

            foreach (PluginInterface iface in plugin.Interfaces)
            {
                foreach (PluginLoadState configEntry in pluginData.InterfaceStates)
                {
                    bool assemblyMatch = string.Equals(configEntry.AssemblyName, iface.AssemblyName, StringComparison.OrdinalIgnoreCase);
                    bool implementationMatch = string.Equals(configEntry.ClassName, iface.ImplementationName, StringComparison.OrdinalIgnoreCase);
                    bool interfaceMatch = string.Equals(configEntry.InterfaceName, iface.InterfaceName, StringComparison.OrdinalIgnoreCase);

                    if (assemblyMatch && implementationMatch && interfaceMatch && configEntry.Enabled)
                    {
                        enabledImplementations.Add(iface);

                        Log.Debug("[Bootstrapper] Enabled implementation {AssemblyName}:{TypeName}:{ImplementationName}", iface.AssemblyName, iface.ImplementationName, iface.InterfaceName);

                        break;
                    }
                }
            }

            return enabledImplementations;
        }

        private static void RegisterInjectors(IPluginService pluginService, IPluginResolver pluginSorter, IServiceCollection serviceCollection, IEnumerable<PluginReference> enabledPlugins)
        {
            Dictionary<Type, List<PluginInjectorDescriptor>> descriptorsByInterface = [];

            foreach (PluginReference plugin in enabledPlugins)
            {
                foreach (PluginInterface implementation in plugin.Interfaces)
                {
                    if (implementation.InterfaceType != typeof(IPluginDependencyInjection))
                        continue;

                    IPluginDependencyInjection? injector = pluginService.GetLoadedInterface<IPluginDependencyInjection>(implementation);

                    if (injector == null)
                    {
                        Log.Error("[Bootstrapper] Failed to create injector instance for {TypeName} in {AssemblyName}.", implementation.ImplementationName, implementation.AssemblyName);

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
                    if (descriptor == null)
                        continue;

                    bool hasImplementationType = descriptor.ImplementationType != null;
                    bool hasImplementationFactory = descriptor.ImplementationFactory != null;

                    if (!hasImplementationType && !hasImplementationFactory)
                    {
                        Log.Warning("[Bootstrapper] Descriptor for {InterfaceType} must specify either ImplementationType or Factory; skipping malformed registration.", descriptor.InterfaceType.Name);

                        continue;
                    }
                    else if (hasImplementationType && hasImplementationFactory)
                    {
                        Log.Information("[Bootstrapper] Descriptor for {InterfaceType} specifies both ImplementationType and Factory; must specify only one; skipping ambiguous registration.", descriptor.InterfaceType.Name);

                        continue;
                    }
                    else if (hasImplementationFactory)
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, provider => descriptor.ImplementationFactory!(provider)!, descriptor.Lifetime));
                    else
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.ImplementationType!, descriptor.Lifetime));
                }
            }

            Log.Information("[Bootstrapper] Plugin DI injection complete: processed {Count} interface contracts across enabled plugins.", descriptorsByInterface.Count);
        }
        private static void RegisterPlugins(IServiceCollection serviceCollection, IEnumerable<PluginReference> enabledPlugins)
        {
            foreach (PluginReference plugin in enabledPlugins)
            {
                Log.Debug("[Bootstrapper] Registering general plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                foreach (PluginInterface iface in plugin.Interfaces)
                {
                    Log.Debug("[Bootstrapper] Registered {AssemblyName}:{TypeName}:{Interface}.", iface.AssemblyName, iface.ImplementationName, iface.InterfaceName);

                    serviceCollection.AddSingleton(iface.InterfaceType, iface.ImplementationType);
                }
            }
        }

        #endregion

        #region Bootstrapper: Plugin Integration

        private static AppConfig PluginsAppConfig(IServiceProvider provider, AppConfig appConfig)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(appConfig);

            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginAppConfig> appConfigPlugins = provider.GetServices<IPluginAppConfig>();

            List<PluginAppConfigDescriptor> allDescriptors = [];

            foreach (IPluginAppConfig appConfigPlugin in appConfigPlugins)
            {
                IEnumerable<PluginAppConfigDescriptor> descriptors = appConfigPlugin.GetAppConfigDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            PluginAppConfigDescriptor[] reverseSortedDescriptors = [.. pluginResolver.ResolveDescriptors(allDescriptors).Reverse()];

            foreach (PluginAppConfigDescriptor descriptor in reverseSortedDescriptors)
            {
                try
                {
                    descriptor.AppConfig?.Invoke(appConfig);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[Bootstrapper] Failed to apply configuration mutation for plugin {PluginID}", descriptor.PluginID);
                }
            }

            Log.Information("[Bootstrapper] PluginsAppConfig completed: applied {PluginCount} config mutation descriptors.", allDescriptors.Count);

            return appConfig;
        }
        private static AppEnv PluginsAppEnv(IServiceProvider provider, AppEnv appEnv)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(appEnv);

            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginAppEnv> appConfigEnvPlugins = provider.GetServices<IPluginAppEnv>();

            List<PluginAppEnvDescriptor> allDescriptors = [];

            foreach (IPluginAppEnv appEnvPlugin in appConfigEnvPlugins)
            {
                IEnumerable<PluginAppEnvDescriptor> descriptors = appEnvPlugin.GetAppEnvDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            PluginAppEnvDescriptor[] reverseSortedDescriptors = [.. pluginResolver.ResolveDescriptors(allDescriptors).Reverse()];

            foreach (PluginAppEnvDescriptor descriptor in reverseSortedDescriptors)
            {
                try
                {
                    descriptor.AppEnv?.Invoke(appEnv);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[Bootstrapper] Failed to apply configuration mutation for plugin {PluginID}", descriptor.PluginID);
                }
            }

            Log.Information("[Bootstrapper] PluginsAppConfig completed: applied {PluginCount} config mutation descriptors.", allDescriptors.Count);

            return appEnv;
        }

        private static void PluginsConfigs(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            IConfigService configService = provider.GetRequiredService<IConfigService>();
            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            ITokenService tokenService = provider.GetRequiredService<ITokenService>();
            IEnumerable<IPluginConfiguration> configurationPlugins = provider.GetServices<IPluginConfiguration>();

            List<PluginConfigurationDescriptor> allDescriptors = [];

            foreach (IPluginConfiguration configurationPlugin in configurationPlugins)
            {
                IEnumerable<PluginConfigurationDescriptor> descriptors = configurationPlugin.GetConfigurationDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            IEnumerable<PluginConfigurationDescriptor> sortedDescriptors = pluginResolver.ResolveDescriptors(allDescriptors);

            foreach (PluginConfigurationDescriptor descriptor in sortedDescriptors)
                configService.RegisterConfig(descriptor.ConfigType, descriptor.ConfigServiceParams(tokenService));

            Log.Information("[Bootstrapper] PluginsConfigs completed: Added {ConfigCount} configuration descriptors from plugins.", allDescriptors.Count);
        }
        private static void PluginsStyleInclude(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            IEnumerable<IPluginStyleInclusion> styleIncludeProviders = provider.GetServices<IPluginStyleInclusion>();
            ILogger<Bootstrapper> logger = provider.GetRequiredService<ILogger<Bootstrapper>>();
            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();

            List<PluginStyleIncludeDescriptor> allDescriptors = [];
            HashSet<string> loadedResourceDictionaries = [];

            foreach (IPluginStyleInclusion providerInstance in styleIncludeProviders)
            {
                IEnumerable<PluginStyleIncludeDescriptor> descriptors = providerInstance.GetStyleIncludeDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            IEnumerable<PluginStyleIncludeDescriptor> sortedDescriptors = pluginResolver.ResolveDescriptors(allDescriptors);

            foreach (PluginStyleIncludeDescriptor descriptor in sortedDescriptors)
            {
                string resource = descriptor.ResourceUri;
                if (Application.Current != null && Application.Current.Styles != null)
                {
                    if (loadedResourceDictionaries.Add(resource))
                    {
                        try
                        {
                            Uri baseUri = string.IsNullOrEmpty(descriptor.BaseUri)
                                ? new Uri("avares://PlugHub/")
                                : new Uri(descriptor.BaseUri);

                            StyleInclude styleInclude = new(baseUri)
                            {
                                Source = new Uri(resource)
                            };

                            Application.Current.Styles.Add(styleInclude);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[Bootstrapper] Failed to load resource dictionary at {Resource}", resource);
                        }
                    }
                }
            }

            logger.LogInformation("[Bootstrapper] PluginsStyleIncludes completed: Added {StyleCount} unique plugin style resource dictionaries.", loadedResourceDictionaries.Count);
        }
        private static void PluginsPages(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            MainViewModel mainViewModel = provider.GetRequiredService<MainViewModel>();
            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginPages> pageProviders = provider.GetServices<IPluginPages>();

            List<PluginPageDescriptor> allDescriptors = [];

            foreach (IPluginPages providerInstance in pageProviders)
                allDescriptors.AddRange(providerInstance.GetPageDescriptors());

            IEnumerable<PluginPageDescriptor> sortedDescriptors = pluginResolver.ResolveDescriptors(allDescriptors);

            foreach (PluginPageDescriptor descriptor in sortedDescriptors)
            {
                UserControl? view;
                BaseViewModel? viewModel;

                #region PluginsPages: Resolve View

                if (descriptor.ViewFactory != null)
                {
                    view = descriptor.ViewFactory(provider);
                }
                else if (descriptor.ViewType != null)
                {
                    view = provider.GetService(descriptor.ViewType) as UserControl;

                    if (view is null)
                    {
                        Log.Error("[Bootstrapper] Could not resolve view type {ViewType} for plugin page {PageName}, skipping.", descriptor.ViewType.FullName, descriptor.Name);

                        continue;
                    }
                }
                else
                {
                    Log.Error("[Bootstrapper] No view factory or view type provided for plugin page {PageName}, skipping.", descriptor.Name);

                    continue;
                }

                #endregion

                #region PluginPages: Resolve viewmodel

                if (descriptor.ViewModelFactory != null)
                {
                    viewModel = descriptor.ViewModelFactory(provider);
                }
                else if (descriptor.ViewModelType != null)
                {
                    viewModel = provider.GetService(descriptor.ViewModelType) as BaseViewModel;

                    if (viewModel is null)
                    {
                        Log.Error("[Bootstrapper] Could not resolve viewmodel type {ViewModelType} for plugin page {PageName}, skipping.", descriptor.ViewModelType.FullName, descriptor.Name);

                        continue;
                    }
                }
                else
                {
                    Log.Error("[Bootstrapper] No viewmodel factory or viewmodel type provided for plugin page {PageName}, skipping.", descriptor.Name);

                    continue;
                }

                #endregion

                ContentItemViewModel page = new(descriptor.ViewType!, descriptor.ViewModelType!, descriptor.Name, descriptor.IconSource)
                {
                    Control = view,
                    ViewModel = viewModel
                };
                view.DataContext = viewModel;

                mainViewModel.AddMainPageItem(page);
            }

            Log.Information("[Bootstrapper] PluginsPages completed: added {PageCount} plugin-provided UI pages into main navigation.", allDescriptors.Count);
        }
        private static void PluginsSettingPages(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            SettingsViewModel settingsViewModel = provider.GetRequiredService<SettingsViewModel>();
            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginSettingsPages> settingsProviders = provider.GetServices<IPluginSettingsPages>();

            List<SettingsPageDescriptor> allDescriptors = [];

            foreach (IPluginSettingsPages providerInstance in settingsProviders)
                allDescriptors.AddRange(providerInstance.GetSettingsPageDescriptors());

            IEnumerable<SettingsPageDescriptor> sortedDescriptors = pluginResolver.ResolveDescriptors(allDescriptors);

            foreach (SettingsPageDescriptor descriptor in sortedDescriptors)
            {
                UserControl? view;
                BaseViewModel? viewModel;

                #region PluginsSettingPages: Resolve View

                if (descriptor.ViewFactory != null)
                {
                    view = descriptor.ViewFactory(provider);
                }
                else if (descriptor.ViewType != null)
                {
                    view = provider.GetService(descriptor.ViewType) as UserControl;

                    if (view == null)
                    {
                        Log.Error("[Bootstrapper] Could not resolve view type {ViewType} for settings page {PageName}, skipping.", descriptor.ViewType.FullName, descriptor.Name);

                        continue;
                    }
                }
                else
                {
                    Log.Error("[Bootstrapper] No view factory or view type provided for settings page {PageName}, skipping.", descriptor.Name);

                    continue;
                }

                #endregion

                #region PluginsSettingPages: Resolve ViewModel

                if (descriptor.ViewModelFactory != null)
                {
                    viewModel = descriptor.ViewModelFactory(provider);
                }
                else if (descriptor.ViewModelType != null)
                {
                    viewModel = provider.GetService(descriptor.ViewModelType) as BaseViewModel;

                    if (viewModel == null)
                    {
                        Log.Error("[Bootstrapper] Could not resolve viewmodel type {ViewModelType} for settings page {PageName}, skipping.", descriptor.ViewModelType.FullName, descriptor.Name);

                        continue;
                    }
                }
                else
                {
                    Log.Error("[Bootstrapper] No viewmodel factory or viewmodel type provided for settings page {PageName}, skipping.", descriptor.Name);

                    continue;
                }

                #endregion

                ContentItemViewModel page = new(descriptor.ViewType!, descriptor.ViewModelType!, descriptor.Name, descriptor.IconSource)
                {
                    Control = view,
                    ViewModel = viewModel
                };
                view.DataContext = viewModel;

                settingsViewModel.AddSettingsPage(descriptor.Group, page);
            }

            Log.Information("[Bootstrapper] PluginsSettingPages completed: added {SettingsPageCount} plugin-provided settings pages, grouped appropriately.", allDescriptors.Count);
        }

        private static void PluginAppServices(IServiceProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            IPluginResolver pluginResolver = provider.GetRequiredService<IPluginResolver>();
            IEnumerable<IPluginAppSetup> appSetupPlugins = provider.GetServices<IPluginAppSetup>();

            List<PluginAppSetupDescriptor> allDescriptors = [];

            foreach (IPluginAppSetup appConfigPlugin in appSetupPlugins)
            {
                IEnumerable<PluginAppSetupDescriptor> descriptors = appConfigPlugin.GetAppSetupDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            IEnumerable<PluginAppSetupDescriptor> reverseSortedDescriptors = pluginResolver.ResolveDescriptors(allDescriptors).Reverse();

            foreach (PluginAppSetupDescriptor descriptor in reverseSortedDescriptors)
            {
                try
                {
                    descriptor.AppSetup?.Invoke(provider);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[Bootstrapper] Failed to apply service setup for plugin {PluginID}", descriptor.PluginID);
                }
            }

            Log.Information("[Bootstrapper] PluginAppServices completed: applied {SetupCount} service mutation descriptors.", allDescriptors.Count);
        }

        #endregion
    }
}