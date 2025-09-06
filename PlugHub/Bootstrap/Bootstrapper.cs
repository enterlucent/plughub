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
        public static IServiceProvider BuildEnv(IServiceCollection services, IConfigService baseConfigService, TokenSet tokenSet, AppConfig baseConfig)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(baseConfigService);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(baseConfig);

            PluginManifest baseManifest;
            AppConfig sysConfig;
            PluginManifest pluginManifest;
            AppConfig pluginConfig;

            CollectServices(services, tokenSet);
            CollectViewModels(services);

            // Initialize an empty plugin reference collection
            IEnumerable<PluginReference> plugins = [];

            // Build a temporary DI provider (to resolve things like logging, services, etc.)
            using (ServiceProvider tempProvider = services.BuildServiceProvider())
            {
                // Load the *base PluginManifest* (system-level plugin config) 
                //         from disk if it exists in AppContext.BaseDirectory
                baseManifest = GetPluginManifest(baseConfigService, tokenSet, AppContext.BaseDirectory);

                // If the base AppConfig specifies a plugin folder, discover & register those plugins
                if (!string.IsNullOrWhiteSpace(baseConfig.PluginFolderPath))
                    plugins = RegisterPlugins(tempProvider, services, baseManifest, baseConfig.PluginFolderPath, plugins);
            }

            // Build a temporary DI provider including system plugins
            using (ServiceProvider tempProvider = services.BuildServiceProvider())
            {
                // Apply system-plugin-provided AppConfig mutations 
                //         (Plugins can override or extend AppConfig here)
                sysConfig = PluginsAppConfig(tempProvider, baseConfig);

                // Merge user manifest with the base manifest and discovered plugin states
                pluginManifest = ResolvePluginManifest(tempProvider, baseConfigService, tokenSet, sysConfig, baseManifest);

                if (!string.IsNullOrWhiteSpace(sysConfig.PluginFolderPath))
                    plugins = RegisterPlugins(tempProvider, services, pluginManifest, sysConfig.PluginFolderPath, plugins);
            }

            // Build a temporary DI provider including user plugins
            using (ServiceProvider tempProvider = services.BuildServiceProvider())
            {
                // Apply plugin-provided AppConfig mutations 
                //         (Plugins can override or extend AppConfig here)
                pluginConfig = PluginsAppConfig(tempProvider, sysConfig);

                // Persist a cache of the loaded plugin references
                services.AddSingleton<IPluginCache>(new PluginCache(plugins));

                SaveAppConfig(baseConfigService, tokenSet, pluginConfig);
                SavePluginManifest(baseConfigService, tokenSet, pluginConfig, pluginManifest);

                // Register a ConfigService instance bound to the plugin AppConfig
                services.AddSingleton(ConfigService.GetInstance(services, pluginConfig));
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
                    new FileConfigServiceParams(Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
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
                    new FileConfigServiceParams(pluginFilePath, Owner: tokenSet.Owner),
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
                    userState.ClassName = systemState.ClassName;
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
                        systemState.ClassName,
                        systemState.InterfaceName,
                        true,
                        systemState.Enabled,
                        systemState.LoadOrder);

                    mergedStates.Add(imposedCopy);

                    Log.Information("[Bootstrapper] Added missing default plugin state for {InterfaceName}", systemState.InterfaceName);
                }

                mergedKeys.Add(key);
            }

            foreach (KeyValuePair<(Guid PluginId, string InterfaceName), PluginLoadState> kvp in userStatesByKey)
                if (!mergedKeys.Contains(kvp.Key))
                    mergedStates.Add(kvp.Value);

            userManifest.InterfaceStates = mergedStates;

            PluginManifest synced = SynchronizePluginConfig(userManifest, discoveredPlugins);

            Log.Information("[Bootstrapper] Resolved and synchronized plugin manifest with {PluginCount} interface states and {DiscoveredCount} discovered plugins.", synced.InterfaceStates?.Count ?? 0, discoveredPlugins.Count());

            return synced;
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
                new FileConfigServiceParams(configPath, Owner: tokenSet.Owner, Read: tokenSet.Read, Write: tokenSet.Write),
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

                        Log.Information("[Bootstrapper] Added new config entry for {AssemblyName}:{TypeName}:{ImplementationName}", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
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
                    bool hasImplementationType = descriptor.ImplementationType != null;
                    bool hasBothTypes = hasImplementationType && descriptor.Instance != null;
                    bool hasNeitherType = !hasImplementationType && descriptor.Instance == null;

                    if (hasNeitherType)
                    {
                        Log.Warning("[Bootstrapper] Descriptor for {InterfaceType} must specify either ImplementationType or Instance; skipping malformed registration.", descriptor.InterfaceType.Name);

                        continue;
                    }
                    else if (hasBothTypes)
                    {
                        Log.Information("[Bootstrapper] Descriptor for {InterfaceType} specifies both ImplementationType and Instance; must specify only one; skipping ambiguous registration.", descriptor.InterfaceType.Name);

                        continue;
                    }
                    else if (descriptor.Instance != null)
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.Instance));
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

            foreach (IPluginAppConfig brandingPlugin in appConfigPlugins)
            {
                IEnumerable<PluginAppConfigDescriptor> descriptors = brandingPlugin.GetAppConfigDescriptors();

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
            {
                configService.RegisterConfig(descriptor.ConfigType, descriptor.ConfigServiceParams(tokenService));
            }

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
            {
                foreach (PluginPageDescriptor descriptor in providerInstance.GetPageDescriptors())
                {
                    allDescriptors.Add(descriptor);
                }
            }

            IEnumerable<PluginPageDescriptor> sortedDescriptors = pluginResolver.ResolveDescriptors(allDescriptors);

            foreach (PluginPageDescriptor descriptor in sortedDescriptors)
            {
                mainViewModel.AddMainPageItem(new(descriptor.ViewType, descriptor.ViewModelType, descriptor.Name, descriptor.IconSource)
                {
                    Control = descriptor.ViewFactory.Invoke(provider),
                    ViewModel = descriptor.ViewModelFactory.Invoke(provider)
                });
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
            {
                foreach (SettingsPageDescriptor descriptor in providerInstance.GetSettingsPageDescriptors())
                {
                    allDescriptors.Add(descriptor);
                }
            }

            IEnumerable<SettingsPageDescriptor> sortedDescriptors = pluginResolver.ResolveDescriptors(allDescriptors);

            foreach (SettingsPageDescriptor descriptor in sortedDescriptors)
            {
                ContentItemViewModel contentItem = new(descriptor.ViewType, descriptor.ViewModelType, descriptor.Name, descriptor.IconSource)
                {
                    Control = descriptor.ViewFactory.Invoke(provider),
                    ViewModel = descriptor.ViewModelFactory.Invoke(provider)
                };
                settingsViewModel.AddSettingsPage(descriptor.Group, contentItem);
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