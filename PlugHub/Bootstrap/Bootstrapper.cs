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
            // ✅ Fail fast if arguments are null
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(baseConfigService);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(baseConfig);

            IServiceProvider baseProvider;

            // STEP 1: Register core framework + plugin infrastructure services (IPluginService, IPluginResolver, etc.)
            CollectServices(services, tokenSet);

            // STEP 2: Register host-specific ViewModels into DI
            CollectViewModels(services);

            // STEP 3: Load the *base PluginManifest* (system-level plugin config) 
            //         from disk if it exists in AppContext.BaseDirectory
            PluginManifest baseManifest = GetPluginManifest(baseConfigService, tokenSet, AppContext.BaseDirectory);

            // Initialize an empty plugin reference collection
            IEnumerable<PluginReference> plugins = [];

            // STEP 4: If the base AppConfig specifies a plugin folder, discover & register those plugins
            if (!string.IsNullOrWhiteSpace(baseConfig.PluginFolderPath))
                plugins = RegisterPlugins(services, baseManifest, baseConfig.PluginFolderPath, plugins);

            // STEP 5: Build a temporary DI provider (to resolve things like logging, services, etc.)
            baseProvider = services.BuildServiceProvider();

            // STEP 6: Apply plugin-provided AppConfig mutations 
            //         (Plugins can override or extend AppConfig here)
            AppConfig userConfig = PluginsAppConfig(baseProvider, baseConfig);

            // STEP 7: Merge user manifest with the base manifest and discovered plugin states
            PluginManifest userManifest =
                ResolvePluginManifest(baseConfigService, baseProvider, tokenSet, userConfig, baseManifest);

            // STEP 8: If the user AppConfig has its own plugin folder, discover and register additional plugins
            if (!string.IsNullOrWhiteSpace(userConfig.PluginFolderPath))
                plugins = RegisterPlugins(services, userManifest, userConfig.PluginFolderPath, plugins);

            // STEP 9: Persist a cache of the loaded plugin references
            services.AddSingleton<IPluginCache>(new PluginCache(plugins));

            // STEP 10: Persist user AppConfig (only write to disk if changes detected)
            SaveAppConfig(baseConfigService, tokenSet, userConfig);

            // STEP 11: Persist user PluginManifest (only write if changes occurred vs disk)
            SavePluginManifest(baseConfigService, tokenSet, userConfig, userManifest);

            // STEP 12: Register a ConfigService instance bound to the user’s AppConfig
            IConfigService configService = ConfigService.GetInstance(services, userConfig);

            services.AddSingleton(configService);

            // STEP 13: Build the *final* DI provider including plugins and configs
            IServiceProvider provider = services.BuildServiceProvider();

            // STEP 14: Apply plugin-provided configurations into ConfigService
            PluginsConfigs(provider, configService);

            // STEP 15: Apply any UI styling declared by plugins (Themes, Resource Dictionaries, etc.)
            PluginsStyleInclude(provider);

            // STEP 16: Register plugin-provided main UI pages into the MainViewModel
            PluginsPages(provider);

            // STEP 17: Register plugin-provided **settings pages** into SettingsViewModel
            PluginsSettingPages(provider);

            // STEP 18: Allow plugins to do additional app-level DI or setup (IPluginAppSetup)
            PluginAppServices(provider);

            // ✅ Return the initialized service provider and the final merged AppConfig
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
        }
        private static void CollectViewModels(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<SettingsPluginsView>();
            services.AddSingleton<SettingsPluginsViewModel>();
        }

        #endregion

        #region Bootstrapper: Config Management

        private static PluginManifest GetPluginManifest(IConfigService configService, TokenSet tokenSet, string directory)
        {
            PluginManifest? pluginManifest = new();

            string pluginFilePath = Path.Combine(directory, "PluginManifest.json");

            if (PlatformPath.Exists(pluginFilePath))
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
        private static PluginManifest ResolvePluginManifest(IConfigService configService, IServiceProvider provider, TokenSet tokenSet, AppConfig appConfig, PluginManifest baseManifest)
        {
            ArgumentNullException.ThrowIfNull(configService);
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(tokenSet);
            ArgumentNullException.ThrowIfNull(appConfig);
            ArgumentNullException.ThrowIfNull(baseManifest);

            ILogger<Bootstrapper> logger = provider.GetRequiredService<ILogger<Bootstrapper>>();
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
        private static PluginManifest SynchronizePluginConfig(ILogger<Bootstrapper> logger, PluginManifest pluginManifest, IEnumerable<PluginReference> plugins)
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
                Task.Run(() => accessor.SaveAsync(appConfig)).GetAwaiter().GetResult();
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
                Task.Run(() => accessor.SaveAsync(pluginManifest)).GetAwaiter().GetResult();
        }

        private static bool AddNewPluginEntries(ILogger<Bootstrapper> logger, PluginManifest pluginData, IEnumerable<PluginReference> plugins)
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

                        logger.LogInformation("Added new config entry for {AssemblyName}:{TypeName}:{ImplementationName}", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
                    }
                }
            }

            return configChanged;
        }
        private static bool RemoveStalePluginEntries(ILogger<Bootstrapper> logger, PluginManifest pluginManifest, HashSet<(string AssemblyName, string ImplementationName, string InterfaceName)> discoveredSet, bool ignoreDefault)
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
                    logger.LogInformation("Removing stale plugin entry {InterfaceName}", state.InterfaceName);

                    pluginManifest.InterfaceStates.RemoveAt(i);

                    removedAny = true;
                }
            }
            return removedAny;
        }

        #endregion

        #region Bootstrapper: Plugin Registration

        private static IEnumerable<PluginReference> RegisterPlugins(IServiceCollection services, PluginManifest? pluginManifest, string pluginFolderPath, IEnumerable<PluginReference> pluginReferences)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(pluginFolderPath);

            IEnumerable<PluginReference> enabled = [];

            using (ServiceProvider tempProvider = services.BuildServiceProvider())
            {
                ILogger<Bootstrapper> logger = tempProvider.GetRequiredService<ILogger<Bootstrapper>>();

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
        private static List<PluginReference> GetEnabledInterfaces(ILogger<Bootstrapper> logger, PluginManifest pluginData, IEnumerable<PluginReference> plugins)
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
        private static List<PluginInterface> FindEnabledImplementations(ILogger<Bootstrapper> logger, PluginManifest pluginData, PluginReference plugin)
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

                        logger.LogDebug("Enabled implementation {AssemblyName}:{TypeName}:{ImplementationName}", iface.AssemblyName, iface.ImplementationName, iface.InterfaceName);

                        break;
                    }
                }
            }

            return enabledImplementations;
        }

        private static void RegisterInjectors(ILogger<Bootstrapper> logger, IPluginService pluginService, IPluginResolver pluginSorter, IServiceCollection serviceCollection, IEnumerable<PluginReference> enabledPlugins)
        {
            Dictionary<Type, List<PluginInjectorDescriptor>> descriptorsByInterface = [];

            foreach (PluginReference plugin in enabledPlugins)
            {
                logger.LogInformation("Registering plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                foreach (PluginInterface implementation in plugin.Interfaces)
                {
                    if (implementation.InterfaceType != typeof(IPluginDependencyInjection))
                        continue;

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
        private static void RegisterPlugins(ILogger<Bootstrapper> logger, IServiceCollection serviceCollection, IEnumerable<PluginReference> enabledPlugins)
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

        #region Bootstrapper: Plugin Integration

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
                    descriptor.AppConfig?.Invoke(appConfig);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to apply configuration mutation for plugin {PluginID}", descriptor.PluginID);
                }
            }

            return appConfig;
        }

        private static void PluginsConfigs(IServiceProvider provider, IConfigService configService)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentNullException.ThrowIfNull(configService);

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
                            logger.LogError(ex, "Failed to load resource dictionary at {Resource}", resource);
                        }
                    }
                }
            }
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
                    Log.Error(ex, "Failed to apply service setup for plugin {PluginID}", descriptor.PluginID);
                }
            }
        }

        #endregion
    }
}