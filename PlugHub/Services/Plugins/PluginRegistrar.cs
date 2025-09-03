using Microsoft.Extensions.Logging;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Interfaces.Accessors;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PlugHub.Services.Plugins
{
    public class PluginRegistrar(ILogger<IPluginRegistrar> logger, IConfigAccessorFor<PluginManifest> manifestAccessor, IPluginService pluginService, IPluginCache pluginCache) : IPluginRegistrar
    {
        private readonly ILogger<IPluginRegistrar> logger = logger ??
            throw new ArgumentNullException(nameof(logger));

        private readonly IConfigAccessorFor<PluginManifest> manifestAccessor = manifestAccessor ??
            throw new ArgumentNullException(nameof(manifestAccessor));

        private readonly IPluginService pluginService = pluginService ??
            throw new ArgumentNullException(nameof(pluginService));

        private readonly IPluginCache pluginCache = pluginCache ??
            throw new ArgumentNullException(nameof(pluginCache));

        public bool IsEnabled(Guid pluginId, Type interfaceType) =>
            this.GetManifest().InterfaceStates.Any(s =>
                s.PluginId == pluginId &&
                s.InterfaceName == interfaceType.FullName &&
                s.Enabled);

        public void SetEnabled(Guid pluginId, Type interfaceType, bool enabled = true)
        {
            PluginManifest manifest = this.GetManifest();
            PluginLoadState? state = manifest.InterfaceStates
                .FirstOrDefault(s => s.PluginId == pluginId && s.InterfaceName == interfaceType.FullName);

            if (state == null || state.System == true) return;
            if (state.Enabled == enabled) return;

            state.Enabled = enabled;
            this.manifestAccessor.Save(manifest);
            this.logger.LogInformation("{Action} interface {Interface} of plugin {PluginId}.",
                enabled ? "Enabled" : "Disabled", interfaceType.Name, pluginId);
        }
        public void SetAllEnabled(Guid pluginId, bool enabled = true)
        {
            PluginManifest manifest = this.GetManifest();
            bool changed = false;

            foreach (PluginLoadState? state in manifest.InterfaceStates.Where(s => s.PluginId == pluginId && s.System != true))
            {
                if (state.Enabled != enabled)
                {
                    state.Enabled = enabled;
                    changed = true;
                }
            }

            if (changed)
            {
                this.manifestAccessor.Save(manifest);
                this.logger.LogInformation("{Action} all interfaces of plugin {PluginId}.",
                    enabled ? "Enabled" : "Disabled", pluginId);
            }
        }

        public PluginManifest GetManifest()
        {
            PluginManifest manifest = this.manifestAccessor.Get();
            if (manifest?.InterfaceStates == null)
                throw new InvalidOperationException("Plugin manifest is unavailable or invalid.");
            return manifest;
        }
        public void SaveManifest(PluginManifest manifest)
        {
            if (manifest?.InterfaceStates == null)
                throw new ArgumentException("Manifest is invalid.", nameof(manifest));

            this.manifestAccessor.Save(manifest);
            this.logger.LogInformation("Plugin manifest saved with {Count} interface states.", manifest.InterfaceStates.Count);
        }

        public List<PluginDescriptor> GetDescriptorsForInterface(Type pluginInterfaceType)
        {
            ArgumentNullException.ThrowIfNull(pluginInterfaceType);

            List<PluginDescriptor> allDescriptors = [];

            foreach (PluginReference pluginReference in this.pluginCache.Plugins)
            {
                IEnumerable<PluginInterface> matchingInterfaces = pluginReference.Interfaces
                    .Where(pi => pluginInterfaceType.IsAssignableFrom(pi.InterfaceType));

                foreach (PluginInterface? pluginInterface in matchingInterfaces)
                {
                    object? pluginInstance = this.pluginService.GetLoadedInterface<object>(pluginInterface);

                    if (pluginInstance == null)
                    {
                        this.logger.LogWarning("Failed to instantiate plugin interface {Interface} from plugin {Plugin}.", pluginInterface.InterfaceType.FullName, pluginReference.AssemblyName);

                        continue;
                    }

                    DescriptorProviderAttribute? attr = null;
                    Type[] allInterfaces = [pluginInterface.InterfaceType, .. pluginInterface.InterfaceType.GetInterfaces()];

                    foreach (Type it in allInterfaces)
                    {
                        attr = it.GetCustomAttribute<DescriptorProviderAttribute>(inherit: false);
                        if (attr != null)
                            break;
                    }

                    if (attr == null)
                    {
                        this.logger.LogWarning("Plugin interface {Interface} missing ProvidesDescriptor attribute.", pluginInterface.InterfaceType.FullName);

                        continue;
                    }

                    string descriptorMethodName = attr.DescriptorAccessorName;
                    MethodInfo? descriptorMethod = pluginInterface.InterfaceType.GetMethod(descriptorMethodName, BindingFlags.Public | BindingFlags.Instance);

                    if (descriptorMethod == null)
                    {
                        this.logger.LogWarning("Descriptor method {Method} not found on plugin interface {Interface}.", descriptorMethodName, pluginInterface.InterfaceType.FullName);

                        continue;
                    }

                    object? descriptorsObj = descriptorMethod.Invoke(pluginInstance, null);

                    if (descriptorsObj is IEnumerable<PluginDescriptor> descriptors)
                    {
                        allDescriptors.AddRange(descriptors);
                    }
                    else
                    {
                        this.logger.LogWarning("Descriptor method {Method} on plugin interface {Interface} did not return expected descriptors.", descriptorMethodName, pluginInterface.InterfaceType.FullName);
                    }
                }
            }

            return allDescriptors;
        }
    }
}