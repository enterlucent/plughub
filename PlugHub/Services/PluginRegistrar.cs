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


    public class PluginRegistrar(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> pluginManifest) : IPluginRegistrar
    {
        protected readonly ILogger<IPluginRegistrar> Logger = logger
            ?? throw new ArgumentNullException();
        protected readonly IConfigAccessorFor<PluginManifest> PluginManifest = pluginManifest
            ?? throw new ArgumentNullException();

        #region PluginRegistrar: Enabled Getters & Mutators

        protected bool GetEnableState(Guid pluginId)
        {
            PluginManifest pluginManifest;

            try
            {
                pluginManifest = this.PluginManifest.Get();
            }
            catch (NullReferenceException)
            {
                throw new InvalidOperationException("Plugin manifest is not available");
            }

            if (pluginManifest == null || pluginManifest.InterfaceStates == null)
            {
                this.Logger.LogWarning("Attempted to call GetEnableState for PluginId {pluginId}, but PluginManifest or InterfaceStates was null. Returning false.", pluginId);
                return false;
            }

            foreach (PluginLoadState loadState in pluginManifest.InterfaceStates)
            {
                if (loadState.PluginId == pluginId && loadState.Enabled)
                    return true;
            }

            return false;
        }
        public bool GetEnabled(Plugin plugin)
            => this.GetEnableState(plugin.Metadata.PluginID);

        protected bool GetEnableState(PluginInterface pluginInterface)
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);

            PluginManifest pluginManifest;

            try
            {
                pluginManifest = this.PluginManifest.Get();
            }
            catch (NullReferenceException)
            {
                throw new InvalidOperationException("Plugin manifest is not available");
            }

            if (pluginManifest == null || pluginManifest.InterfaceStates == null)
            {
                this.Logger.LogWarning("Attempted to call GetEnableState for PluginInterface {InterfaceName}, but PluginManifest or InterfaceStates was null. Returning false.", pluginInterface.InterfaceName);

                return false;
            }

            foreach (PluginLoadState loadState in pluginManifest.InterfaceStates)
            {
                bool assemblyMatch = string.Equals(loadState.AssemblyName, pluginInterface.AssemblyName, StringComparison.OrdinalIgnoreCase);
                bool implementationMatch = string.Equals(loadState.ImplementationName, pluginInterface.ImplementationName, StringComparison.OrdinalIgnoreCase);
                bool interfaceMatch = string.Equals(loadState.InterfaceName, pluginInterface.InterfaceName, StringComparison.OrdinalIgnoreCase);

                if (assemblyMatch && implementationMatch && interfaceMatch)
                    return loadState.Enabled;
            }
            return false;
        }
        public bool GetEnabled(PluginInterface pluginInterface)
            => this.GetEnableState(pluginInterface);


        protected void SetEnableState(Guid pluginId, bool enabled)
        {
            PluginManifest pluginManifest;

            try
            {
                pluginManifest = this.PluginManifest.Get();
            }
            catch (NullReferenceException)
            {
                throw new InvalidOperationException("Plugin manifest is not available");
            }

            if (pluginManifest == null || pluginManifest.InterfaceStates == null)
            {
                this.Logger.LogWarning("Attempted to call SetEnableState for PluginId={PluginId}, but PluginManifest or InterfaceStates was null. Operation aborted.", pluginId);

                return;
            }

            bool foundAny = false;
            bool changeMade = false;

            foreach (PluginLoadState loadState in pluginManifest.InterfaceStates)
            {
                if (loadState.PluginId == pluginId)
                {
                    foundAny = true;
                    if (loadState.Enabled != enabled)
                    {
                        this.Logger.LogInformation("Setting Enabled={Enabled} for {PluginId} - {AssemblyName}:{ImplementationName}:{InterfaceName}", enabled, loadState.PluginId, loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName);

                        loadState.Enabled = enabled;

                        changeMade = true;
                    }
                    else
                    {
                        this.Logger.LogInformation("No change needed: {PluginId} - {AssemblyName}:{ImplementationName}:{InterfaceName} already has Enabled={Enabled}.", loadState.PluginId, loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName, enabled);
                    }
                }
            }

            if (!foundAny)
            {
                this.Logger.LogWarning("No PluginLoadState entries found for PluginId={PluginId}. No changes made.", pluginId);
            }

            if (changeMade)
            {
                this.PluginManifest.Save(pluginManifest);

                this.Logger.LogInformation("Plugin configuration changes detected and saved for PluginId={PluginId}.", pluginId);
            }
            else if (foundAny)
            {
                this.Logger.LogInformation("No plugin configuration changes detected for PluginId={PluginId}.", pluginId);
            }
        }
        public void SetEnabled(Plugin plugin)
            => this.SetEnableState(plugin.Metadata.PluginID, true);
        public void SetDisabled(Plugin plugin)
            => this.SetEnableState(plugin.Metadata.PluginID, false);

        protected void SetEnableState(PluginInterface pluginInterface, bool enabled)
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);

            PluginManifest pluginManifest;

            try
            {
                pluginManifest = this.PluginManifest.Get();
            }
            catch (NullReferenceException)
            {
                throw new InvalidOperationException("Plugin manifest is not available");
            }

            if (pluginManifest == null || pluginManifest.InterfaceStates == null)
            {
                this.Logger.LogWarning("Attempted to call SetEnableState for PluginInterface={PluginId}, but PluginManifest or InterfaceStates was null. Operation aborted.", pluginInterface.InterfaceName);

                return;
            }

            bool changeMade = false;
            bool foundMatch = false;

            foreach (PluginLoadState loadState in pluginManifest.InterfaceStates)
            {
                bool assemblyMatch = string.Equals(loadState.AssemblyName, pluginInterface.AssemblyName, StringComparison.OrdinalIgnoreCase);
                bool implementationMatch = string.Equals(loadState.ImplementationName, pluginInterface.ImplementationName, StringComparison.OrdinalIgnoreCase);
                bool interfaceMatch = string.Equals(loadState.InterfaceName, pluginInterface.InterfaceName, StringComparison.OrdinalIgnoreCase);

                if (assemblyMatch && implementationMatch && interfaceMatch)
                {
                    foundMatch = true;

                    if (loadState.Enabled != enabled)
                    {
                        this.Logger.LogInformation("Setting Enabled={Enabled} for plugin interface {AssemblyName}:{ImplementationName}:{InterfaceName}.", enabled, loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName);

                        loadState.Enabled = enabled;

                        changeMade = true;
                    }
                    else
                    {
                        this.Logger.LogInformation("No change: plugin interface {AssemblyName}:{ImplementationName}:{InterfaceName} already has Enabled={Enabled}.", loadState.AssemblyName, loadState.ImplementationName, loadState.InterfaceName, enabled);
                    }
                }
            }

            if (!foundMatch)
            {
                this.Logger.LogWarning("No PluginLoadState found matching {AssemblyName}:{ImplementationName}:{InterfaceName}. No changes made.", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
            }

            if (changeMade)
            {
                this.PluginManifest.Save(pluginManifest);

                this.Logger.LogInformation("Plugin configuration changes detected and saved for {AssemblyName}:{ImplementationName}:{InterfaceName}.", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
            }
            else if (foundMatch)
            {
                this.Logger.LogInformation("No plugin configuration changes detected for {AssemblyName}:{ImplementationName}:{InterfaceName}.", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
            }
        }
        public void SetEnabled(PluginInterface pluginInterface)
            => this.SetEnableState(pluginInterface, true);
        public void SetDisabled(PluginInterface pluginInterface)
            => this.SetEnableState(pluginInterface, false);

        #endregion

        #region PluginRegistrar: Plugin Boostrapping

        public static void SynchronizePluginConfig(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> pluginManifest, IEnumerable<Plugin> plugins)
        {
            ArgumentNullException.ThrowIfNull(pluginManifest);
            plugins ??= [];

            PluginManifest pluginData = pluginManifest.Get();
            pluginData.InterfaceStates ??= [];

            logger.LogInformation("Synchronizing plugin config. Current entries: {EntryCount}", pluginData.InterfaceStates.Count);

            HashSet<(string AssemblyName, string ImplementationName, string InterfaceName)> discoveredSet = new(
                plugins.SelectMany(plugin =>
                    plugin.Interfaces.Select(pluginInterface =>
                        (pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName))),

                EqualityComparer<(string, string, string)>.Default);

            bool configChanged = false;

            foreach (Plugin plugin in plugins)
            {
                foreach (PluginInterface pluginInterface in plugin.Interfaces)
                {
                    bool exists = pluginData.InterfaceStates
                        .Any(c =>
                            c.PluginId == plugin.Metadata.PluginID &&
                            string.Equals(c.AssemblyName, pluginInterface.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(c.ImplementationName, pluginInterface.ImplementationName, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(c.InterfaceName, pluginInterface.InterfaceName, StringComparison.OrdinalIgnoreCase)
                        );

                    if (!exists)
                    {
                        pluginData.InterfaceStates.Add(
                            new PluginLoadState(
                                plugin.Metadata.PluginID,
                                pluginInterface.AssemblyName,
                                pluginInterface.ImplementationName,
                                pluginInterface.InterfaceName,
                                false,
                                int.MaxValue));

                        configChanged = true;

                        logger.LogInformation("Added new config entry for {AssemblyName}:{TypeName}:{ImplementationName}", pluginInterface.AssemblyName, pluginInterface.ImplementationName, pluginInterface.InterfaceName);
                    }
                }
            }

            List<PluginLoadState> toRemove =
                [.. pluginData.InterfaceStates
                    .Where(c => !discoveredSet.Contains((c.AssemblyName, c.ImplementationName, c.InterfaceName)) && !c.Enabled)];

            foreach (PluginLoadState entry in toRemove)
            {
                pluginData.InterfaceStates.Remove(entry);

                configChanged = true;

                logger.LogInformation("Removed stale config entry for {AssemblyName}:{TypeName}:{ImplementationName}", entry.AssemblyName, entry.ImplementationName, entry.InterfaceName);
            }

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
        public static IEnumerable<Plugin> GetEnabledInterfaces(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> pluginManifest, IEnumerable<Plugin> plugins)
        {
            PluginManifest pluginData = pluginManifest.Get();
            pluginData.InterfaceStates ??= [];

            logger.LogInformation("Checking enabled plugins against {EntryCount} config entries.", pluginData.InterfaceStates.Count);

            List<Plugin> result = [];

            foreach (Plugin plugin in plugins)
            {
                logger.LogDebug("Processing plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                List<PluginInterface> enabledImplementations = [];

                foreach (PluginInterface iface in plugin.Interfaces)
                {
                    bool isEnabled = false;

                    foreach (PluginLoadState configEntry in pluginData.InterfaceStates)
                    {
                        bool assemblyMatch = string.Equals(configEntry.AssemblyName, iface.AssemblyName, StringComparison.OrdinalIgnoreCase);
                        bool typeMatch = string.Equals(configEntry.ImplementationName, iface.ImplementationName, StringComparison.OrdinalIgnoreCase);
                        bool interfaceMatch = string.Equals(configEntry.InterfaceName, iface.InterfaceName, StringComparison.OrdinalIgnoreCase);

                        isEnabled = configEntry.Enabled;

                        if (assemblyMatch && typeMatch && interfaceMatch && isEnabled)
                        {
                            enabledImplementations.Add(iface);

                            logger.LogDebug("Enabled implementation {AssemblyName}:{TypeName}:{ImplementationName}", iface.AssemblyName, iface.ImplementationName, iface.InterfaceName);

                            break;
                        }
                    }
                }

                if (enabledImplementations.Count > 0)
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
            Dictionary<Type, List<PluginInjectorDescriptor>> descriptorsByInterface = [];

            foreach (Plugin plugin in enabledPlugins)
            {
                logger.LogInformation("Registering plugin {AssemblyName}:{TypeName}", plugin.AssemblyName, plugin.TypeName);

                foreach (PluginInterface implementation in plugin.Interfaces)
                {
                    if (implementation.InterfaceType == typeof(IPluginDependencyInjector))
                    {
                        IPluginDependencyInjector? injector =
                            pluginService.GetLoadedInterface<IPluginDependencyInjector>(implementation);

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
            }

            foreach (KeyValuePair<Type, List<PluginInjectorDescriptor>> kvp in descriptorsByInterface)
            {
                Type interfaceType = kvp.Key;

                List<PluginInjectorDescriptor> descriptors = kvp.Value;
                List<PluginInjectorDescriptor> reverseSorted = [.. pluginSorter.ResolveDescriptors(descriptors).Reverse()];

                foreach (PluginInjectorDescriptor? descriptor in reverseSorted)
                {
                    if (descriptor.ImplementationType == null && descriptor.Instance == null)
                    {
                        logger.LogWarning("Descriptor for {InterfaceType} must specify either ImplementationType or Instance; skipping malformed registration.", descriptor.InterfaceType.Name);

                        continue;
                    }

                    if (descriptor.ImplementationType != null && descriptor.Instance != null)
                    {
                        logger.LogWarning("Descriptor for {InterfaceType} specifies both ImplementationType and Instance; must specify only one; skipping ambiguous registration.", descriptor.InterfaceType.Name);

                        continue;
                    }

                    if (descriptor.Instance == null)
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.ImplementationType!, descriptor.Lifetime));
                    else
                        serviceCollection.Add(new ServiceDescriptor(interfaceType, descriptor.Instance));
                }
            }
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
    }
}