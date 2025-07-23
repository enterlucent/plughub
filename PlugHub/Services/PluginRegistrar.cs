using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PlugHub.Shared;
using PlugHub.Shared.Interfaces;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;


namespace PlugHub.Services
{
    public class PluginLoadState(Guid pluginId = default, string assemblyName = "Unknown", string implementationName = "Unknown", string interfaceName = "Unknown", bool enabled = false, int loadOrder = int.MaxValue)
    {
        public PluginLoadState()
            : this(default, "Unknown", "Unknown", "Unknown", false, int.MaxValue) { }

        public Guid PluginId { get; set; } = pluginId;
        public string AssemblyName { get; set; } = assemblyName;
        public string ImplementationName { get; set; } = implementationName;
        public string InterfaceName { get; set; } = interfaceName;
        public bool Enabled { get; set; } = enabled;
        public int LoadOrder { get; set; } = loadOrder;
    }

    public class PluginManifest
    {
        public List<PluginLoadState> InterfaceStates { get; set; } = [];
    }

    public class PluginRegistrar : IPluginRegistrar
    {
        private class EnableStateChangeResult
        {
            public bool FoundAny { get; set; }
            public bool ChangeMade { get; set; }
            public List<PluginLoadState> ModifiedStates { get; set; } = [];
        }

        protected readonly ILogger<IPluginRegistrar> Logger;
        protected readonly IConfigAccessorFor<PluginManifest> PluginManifest;

        public PluginRegistrar(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> pluginManifest)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(pluginManifest);

            this.Logger = logger;
            this.PluginManifest = pluginManifest;
        }

        #region PluginRegistrar: Enabled Getters & Mutators

        protected bool GetEnableState(Guid pluginId)
        {
            return this.GetEnableState(pluginId, (state, id) => state.PluginId == id && state.Enabled);
        }
        public bool GetEnabled(Plugin plugin)
            => this.GetEnableState(plugin.Metadata.PluginID);

        protected bool GetEnableState(PluginInterface pluginInterface)
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);

            return this.GetEnableState(pluginInterface, (state, iface) => DoesLoadStateMatch(state, iface) && state.Enabled);
        }
        public bool GetEnabled(PluginInterface pluginInterface)
            => this.GetEnableState(pluginInterface);

        protected void SetEnableState(Guid pluginId, bool enabled)
        {
            try
            {
                PluginManifest pluginManifest = this.GetValidatedManifest($"SetEnableState for PluginId {pluginId}");

                EnableStateChangeResult result =
                    this.UpdateEnableState(
                        pluginManifest,
                        pluginId,
                        enabled,
                        (state, id) => state.PluginId == id,
                        "PluginId");

                string identifier = GetIdentifierString(pluginId);

                this.HandleEnableStateChangeResult(result, identifier, "PluginId", enabled);
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }
        public void SetEnabled(Plugin plugin)
            => this.SetEnableState(plugin.Metadata.PluginID, true);
        public void SetDisabled(Plugin plugin)
            => this.SetEnableState(plugin.Metadata.PluginID, false);

        protected void SetEnableState(PluginInterface pluginInterface, bool enabled)
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);

            try
            {
                PluginManifest pluginManifest = this.GetValidatedManifest($"SetEnableState for PluginInterface {pluginInterface.InterfaceName}");

                EnableStateChangeResult result =
                    this.UpdateEnableState(
                        pluginManifest,
                        pluginInterface,
                        enabled,
                        (state, iface) => DoesLoadStateMatch(state, iface),
                        "Interface");

                string identifier = GetIdentifierString(pluginInterface);

                this.HandleEnableStateChangeResult(result, identifier, "interface", enabled);
            }
            catch (InvalidOperationException)
            {
                return;
            }
        }
        public void SetEnabled(PluginInterface pluginInterface)
            => this.SetEnableState(pluginInterface, true);
        public void SetDisabled(PluginInterface pluginInterface)
            => this.SetEnableState(pluginInterface, false);

        #endregion

        #region PluginRegistrar: Core Helper Methods

        private bool GetEnableState<T>(T target, Func<PluginLoadState, T, bool> matchPredicate)
        {
            try
            {
                string operationContext = $"GetEnableState for {typeof(T).Name}";
                PluginManifest pluginManifest = this.GetValidatedManifest(operationContext);

                foreach (PluginLoadState loadState in pluginManifest.InterfaceStates)
                {
                    bool isMatch = matchPredicate(loadState, target);

                    if (isMatch)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
        private EnableStateChangeResult UpdateEnableState<T>(PluginManifest pluginManifest, T target, bool enabled, Func<PluginLoadState, T, bool> matchPredicate, string logContext)
        {
            EnableStateChangeResult result = new();

            foreach (PluginLoadState loadState in pluginManifest.InterfaceStates)
            {
                bool isMatch = matchPredicate(loadState, target);

                if (!isMatch) continue;

                result.FoundAny = true;

                bool needsUpdate = loadState.Enabled != enabled;
                bool alreadyCorrectState = loadState.Enabled == enabled;

                if (needsUpdate)
                {
                    if (logContext == "PluginId")
                    {
                        this.Logger.LogInformation("Setting Enabled={Enabled} for {PluginId} - {AssemblyName}:{ImplementationName}:{InterfaceName}", enabled, loadState.PluginId, loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName);
                    }
                    else
                    {
                        this.Logger.LogInformation("Setting Enabled={Enabled} for plugin interface {AssemblyName}:{ImplementationName}:{InterfaceName}.", enabled, loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName);
                    }

                    loadState.Enabled = enabled;
                    result.ChangeMade = true;
                    result.ModifiedStates.Add(loadState);
                }
                else if (alreadyCorrectState)
                {
                    if (logContext == "PluginId")
                    {
                        this.Logger.LogInformation("No change needed: {PluginId} - {AssemblyName}:{ImplementationName}:{InterfaceName} already has Enabled={Enabled}.", loadState.PluginId, loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName, enabled);
                    }
                    else
                    {
                        this.Logger.LogInformation("No change: plugin interface {AssemblyName}:{ImplementationName}:{InterfaceName} already has Enabled={Enabled}.", loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName, enabled);
                    }
                }
            }

            return result;
        }
        private void HandleEnableStateChangeResult(EnableStateChangeResult result, string identifier, string identifierType, bool enabled)
        {
            bool foundAny = result.FoundAny;
            bool changeMade = result.ChangeMade;

            if (!foundAny)
            {
                if (identifierType == "PluginId")
                {
                    this.Logger.LogWarning("No PluginLoadState entries found for PluginId={PluginId} when trying to set Enabled={Enabled}. No changes made.", identifier, enabled);
                }
                else
                {
                    this.Logger.LogWarning("No PluginLoadState found matching {Interface} when trying to set Enabled={Enabled}. No changes made.", identifier, enabled);
                }
                return;
            }

            if (changeMade)
            {
                this.SaveChangesAndLog(identifier, identifierType);
            }
            else
            {
                if (identifierType == "PluginId")
                {
                    this.Logger.LogInformation("No plugin configuration changes detected for PluginId={PluginId}. Already set to Enabled={Enabled}.", identifier, enabled);
                }
                else
                {
                    this.Logger.LogInformation("No plugin configuration changes detected for {Interface}. Already set to Enabled={Enabled}.", identifier, enabled);
                }
            }
        }

        private static string GetIdentifierString(Guid pluginId)
            => pluginId.ToString();
        private static string GetIdentifierString(PluginInterface pluginInterface)
            => $"{pluginInterface.AssemblyName}:{pluginInterface.ImplementationName}:{pluginInterface.InterfaceName}";

        private PluginManifest GetValidatedManifest(string operationContext)
        {
            PluginManifest manifest;

            try
            {
                manifest = this.PluginManifest.Get();
            }
            catch (NullReferenceException)
            {
                throw new InvalidOperationException("Plugin manifest is not available");
            }

            bool manifestExists = manifest != null;
            bool interfaceStatesExists = manifest?.InterfaceStates != null;
            bool isValidManifest = manifestExists && interfaceStatesExists;

            if (!isValidManifest)
            {
                this.Logger.LogWarning("Plugin manifest validation failed for operation: {Operation}. Manifest null: {ManifestNull}, InterfaceStates null: {StatesNull}",
                    operationContext, !manifestExists, !interfaceStatesExists);

                throw new InvalidOperationException($"Invalid plugin manifest state for operation: {operationContext}");
            }

            return manifest!;
        }

        private void SaveChangesAndLog(string identifier, string identifierType)
        {
            try
            {
                PluginManifest manifest = this.GetValidatedManifest($"Save changes for {identifierType} {identifier}");
                this.PluginManifest.Save(manifest);

                this.Logger.LogInformation("Plugin configuration changes detected and saved for {IdentifierType}={Identifier}.", identifierType, identifier);
            }
            catch (InvalidOperationException ex)
            {
                this.Logger.LogError("Failed to save plugin configuration changes for {IdentifierType}={Identifier}. Error: {Error}", identifierType, identifier, ex.Message);

                throw;
            }
        }

        #endregion

        #region PluginRegistrar: Static Matching Helpers

        private static bool DoesLoadStateMatch(PluginLoadState loadState, PluginInterface pluginInterface)
        {
            bool assemblyMatch = string.Equals(loadState.AssemblyName, pluginInterface.AssemblyName, StringComparison.OrdinalIgnoreCase);
            bool implementationMatch = string.Equals(loadState.ImplementationName, pluginInterface.ImplementationName, StringComparison.OrdinalIgnoreCase);
            bool interfaceMatch = string.Equals(loadState.InterfaceName, pluginInterface.InterfaceName, StringComparison.OrdinalIgnoreCase);

            return assemblyMatch && implementationMatch && interfaceMatch;
        }
        private static bool DoesLoadStateMatch(PluginLoadState loadState, Plugin plugin, PluginInterface pluginInterface)
        {
            bool pluginIdMatch = loadState.PluginId == plugin.Metadata.PluginID;
            bool interfaceMatch = DoesLoadStateMatch(loadState, pluginInterface);

            return pluginIdMatch && interfaceMatch;
        }

        #endregion

        #region PluginRegistrar: Plugin Bootstrapping

        public static void SynchronizePluginConfig(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> pluginManifest, IEnumerable<Plugin> plugins)
        {
            ArgumentNullException.ThrowIfNull(pluginManifest);
            plugins ??= [];

            PluginManifest pluginData =
                PreparePluginManifestCore(
                    pluginManifest,
                    count => logger.LogInformation("Synchronizing plugin config. Current entries: {EntryCount}",
                    count));

            HashSet<(string, string, string)> discoveredSet = BuildDiscoveredInterfaceSet(plugins);

            bool newEntriesAdded = AddNewPluginEntries(logger, pluginData, plugins);
            bool staleEntriesRemoved = RemoveStalePluginEntries(logger, pluginData, discoveredSet);

            SaveConfigurationIfNeeded(logger, pluginManifest, pluginData, newEntriesAdded || staleEntriesRemoved);
        }
        public static IEnumerable<Plugin> GetEnabledInterfaces(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> pluginManifest, IEnumerable<Plugin> plugins)
        {
            PluginManifest pluginData =
                PreparePluginManifestCore(
                    pluginManifest,
                    count => logger.LogInformation("Checking enabled plugins against {EntryCount} config entries.",
                    count));

            List<Plugin> result = [];

            foreach (Plugin plugin in plugins)
            {
                logger.LogDebug("Processing plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                List<PluginInterface> enabledImplementations = FindEnabledImplementations(logger, pluginData, plugin);

                bool hasEnabledImplementations = enabledImplementations.Count > 0;

                if (hasEnabledImplementations)
                {
                    logger.LogInformation("Plugin {AssemblyName}:{TypeName} has {EnabledCount} enabled implementations.", plugin.AssemblyName, plugin.TypeName, enabledImplementations.Count);
                    result.Add(new Plugin(plugin.Assembly, plugin.Type, plugin.Metadata, enabledImplementations));
                }
            }

            logger.LogInformation("Found {PluginCount} plugins with enabled implementations.", result.Count);

            return result;
        }
        public static void RegisterInjectors(ILogger<IPluginRegistrar> logger, IPluginService pluginService, IPluginResolver pluginSorter, IServiceCollection serviceCollection, IEnumerable<Plugin> enabledPlugins)
        {
            Dictionary<Type, List<PluginInjectorDescriptor>> descriptorsByInterface = CollectInjectorDescriptors(logger, pluginService, enabledPlugins);

            ProcessDescriptorRegistrations(logger, pluginSorter, serviceCollection, descriptorsByInterface);
        }
        public static void RegisterPlugins(ILogger<IPluginRegistrar> logger, IServiceCollection serviceCollection, IEnumerable<Plugin> enabledPlugins)
        {
            foreach (Plugin plugin in enabledPlugins)
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

        #region PluginRegistrar: Bootstrap Helper Methods

        private static PluginManifest PreparePluginManifestCore(IConfigAccessorFor<PluginManifest> pluginManifest, Action<int> logAction)
        {
            PluginManifest pluginData = pluginManifest.Get();
            pluginData.InterfaceStates ??= [];

            logAction(pluginData.InterfaceStates.Count);

            return pluginData;
        }
        private static HashSet<(string, string, string)> BuildDiscoveredInterfaceSet(IEnumerable<Plugin> plugins)
        {
            return new HashSet<(string, string, string)>(
                plugins.SelectMany(plugin =>
                    plugin.Interfaces.Select(pluginInterface =>
                        (pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName))),
                EqualityComparer<(string, string, string)>.Default);
        }
        private static PluginLoadState CreateNewPluginLoadState(Plugin plugin, PluginInterface pluginInterface)
        {
            return new PluginLoadState(
                plugin.Metadata.PluginID,
                pluginInterface.AssemblyName,
                pluginInterface.ImplementationName,
                pluginInterface.InterfaceName,
                false,
                int.MaxValue);
        }
        private static List<PluginLoadState> FindStaleEntries(PluginManifest pluginData, HashSet<(string, string, string)> discoveredSet)
        {
            List<PluginLoadState> staleEntries = [];

            foreach (PluginLoadState loadState in pluginData.InterfaceStates)
            {
                bool isDiscovered = discoveredSet.Contains((loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName));
                bool isEnabled = loadState.Enabled;
                bool isStaleEntry = !isDiscovered && !isEnabled;

                if (isStaleEntry)
                {
                    staleEntries.Add(loadState);
                }
            }

            return staleEntries;
        }

        private static bool AddNewPluginEntries(ILogger<IPluginRegistrar> logger, PluginManifest pluginData, IEnumerable<Plugin> plugins)
        {
            bool configChanged = false;

            foreach (Plugin plugin in plugins)
            {
                foreach (PluginInterface pluginInterface in plugin.Interfaces)
                {
                    bool entryExists = pluginData.InterfaceStates.Any(c => DoesLoadStateMatch(c, plugin, pluginInterface));

                    if (!entryExists)
                    {
                        PluginLoadState newEntry = CreateNewPluginLoadState(plugin, pluginInterface);
                        pluginData.InterfaceStates.Add(newEntry);

                        configChanged = true;

                        logger.LogInformation("Added new config entry for {AssemblyName}:{TypeName}:{ImplementationName}", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
                    }
                }
            }

            return configChanged;
        }
        private static bool RemoveStalePluginEntries(ILogger<IPluginRegistrar> logger, PluginManifest pluginData, HashSet<(string, string, string)> discoveredSet)
        {
            bool configChanged = false;

            List<PluginLoadState> entriesToRemove = FindStaleEntries(pluginData, discoveredSet);

            foreach (PluginLoadState entry in entriesToRemove)
            {
                bool removalSuccessful = pluginData.InterfaceStates.Remove(entry);

                if (removalSuccessful)
                {
                    configChanged = true;

                    logger.LogInformation("Removed stale config entry for {AssemblyName}:{TypeName}:{ImplementationName}", entry.AssemblyName, entry.ImplementationName, entry.InterfaceName);
                }
            }

            return configChanged;
        }
        private static void SaveConfigurationIfNeeded(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> pluginManifest, PluginManifest pluginData, bool configChanged)
        {
            if (configChanged)
            {
                pluginManifest.Save(pluginData);

                logger.LogInformation("Plugin config changes detected and saved.");
            }
            else
            {
                logger.LogInformation("No plugin config changes detected.");
            }
        }

        private static List<PluginInterface> FindEnabledImplementations(ILogger<IPluginRegistrar> logger, PluginManifest pluginData, Plugin plugin)
        {
            List<PluginInterface> enabledImplementations = [];

            foreach (PluginInterface iface in plugin.Interfaces)
            {
                foreach (PluginLoadState configEntry in pluginData.InterfaceStates)
                {
                    bool isMatch = DoesLoadStateMatch(configEntry, iface);
                    bool isEnabled = configEntry.Enabled;
                    bool isEnabledMatch = isMatch && isEnabled;

                    if (isEnabledMatch)
                    {
                        enabledImplementations.Add(iface);

                        logger.LogDebug("Enabled implementation {AssemblyName}:{TypeName}:{ImplementationName}", iface.AssemblyName, iface.ImplementationName, iface.InterfaceName);

                        break;
                    }
                }
            }

            return enabledImplementations;
        }
        private static Dictionary<Type, List<PluginInjectorDescriptor>> CollectInjectorDescriptors(ILogger<IPluginRegistrar> logger, IPluginService pluginService, IEnumerable<Plugin> enabledPlugins)
        {
            Dictionary<Type, List<PluginInjectorDescriptor>> descriptorsByInterface = [];

            foreach (Plugin plugin in enabledPlugins)
            {
                logger.LogInformation("Registering plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                foreach (PluginInterface implementation in plugin.Interfaces)
                {
                    bool isInjector = implementation.InterfaceType == typeof(IPluginDependencyInjector);

                    if (!isInjector) continue;

                    IPluginDependencyInjector? injector = pluginService.GetLoadedInterface<IPluginDependencyInjector>(implementation);

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

            return descriptorsByInterface;
        }
        private static void ProcessDescriptorRegistrations(ILogger<IPluginRegistrar> logger, IPluginResolver pluginSorter, IServiceCollection serviceCollection, Dictionary<Type, List<PluginInjectorDescriptor>> descriptorsByInterface)
        {
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
                    {
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.Instance));
                    }
                    else
                    {
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.ImplementationType!, descriptor.Lifetime));
                    }
                }
            }
        }

        #endregion
    }
}
