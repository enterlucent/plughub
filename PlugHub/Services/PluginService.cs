using Microsoft.Extensions.Logging;
using PlugHub.Shared;
using PlugHub.Shared.Interfaces;
using PlugHub.Shared.Interfaces.Services;
using PlugHub.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace PlugHub.Services
{
    public class PluginService(ILogger<IPluginService> logger) : IPluginService
    {
        private readonly ILogger<IPluginService> logger = logger
            ?? throw new ArgumentNullException();

        private readonly SemaphoreSlim iolock = new(1, 1);

        #region PlugHub.Services.PluginService Plugin Instances

        public TPlugin? GetLoadedPlugin<TPlugin>(PluginInterface pluginInterface) where TPlugin : PluginBase
        {
            ArgumentNullException.ThrowIfNull(nameof(pluginInterface));

            this.iolock.Wait();

            try
            {
                if (!File.Exists(pluginInterface.AssemblyLocation))
                {
                    this.logger?.LogWarning("Plugin assembly does not exist: {AssemblyPath}", pluginInterface.AssemblyLocation);

                    return null;
                }

                Assembly assembly = Assembly.LoadFrom(pluginInterface.AssemblyLocation);

                Type? type = assembly.GetType(pluginInterface.ImplementationName);

                if (type == null)
                {
                    this.logger?.LogWarning("Type {TypeName} not found in assembly {AssemblyName}.", pluginInterface.ImplementationName, pluginInterface.AssemblyName);

                    return null;
                }

                if (!typeof(TPlugin).IsAssignableFrom(type) || type.IsAbstract)
                {
                    this.logger?.LogWarning("Type {TypeName} in assembly {AssemblyName} does not implement {TPlugin} or is abstract.", pluginInterface.ImplementationName, pluginInterface.AssemblyName, typeof(TPlugin).Name);

                    return null;
                }

                if (Activator.CreateInstance(type) is TPlugin pluginInstance)
                {
                    this.logger?.LogInformation("Loaded plugin: {PluginName} ({TypeName})", pluginInterface.AssemblyName, pluginInterface.ImplementationName);

                    return pluginInstance;
                }
                else
                {
                    this.logger?.LogWarning("Failed to create instance of type {TypeName} from assembly {AssemblyName}.", pluginInterface.ImplementationName, pluginInterface.AssemblyName);

                    return null;
                }
            }
            catch (Exception ex)
            {
                this.logger?.LogError("Failed to load plugin {TypeName} from {AssemblyPath}. Exception: {ExceptionMessage}", pluginInterface.ImplementationName, pluginInterface.AssemblyLocation, ex.Message);

                return null;
            }
            finally { this.iolock.Release(); }
        }
        public TInterface? GetLoadedInterface<TInterface>(PluginInterface pluginInterface)
            where TInterface : class
        {
            ArgumentNullException.ThrowIfNull(nameof(pluginInterface));

            PluginBase? instance = this.GetLoadedPlugin<PluginBase>(pluginInterface);

            return instance as TInterface;
        }

        #endregion

        #region PlugHub.Services.PluginService: Discovery

        public IEnumerable<Plugin> Discover(string pluginDirectory)
        {
            List<Assembly> assemblies = [];

            this.iolock.Wait();

            try
            {
                foreach (string assemblyPath in Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories))
                {
                    try
                    {
                        assemblies.Add(Assembly.LoadFrom(assemblyPath));
                    }
                    catch (Exception ex)
                    {
                        this.logger?.LogError("Failed to load assembly {AssemblyPath}: {ExceptionMessage}", assemblyPath, ex.Message);
                    }
                }
            }
            finally { this.iolock.Release(); }

            Dictionary<Assembly, Type[]> assemblyTypes = [];

            foreach (Assembly assembly in assemblies)
            {
                try
                {
                    assemblyTypes[assembly] = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    foreach (Exception? loaderEx in rtle.LoaderExceptions)
                        this.logger?.LogError("Loader exception in assembly {AssemblyName}: {ExceptionMessage}", assembly.FullName, loaderEx?.Message);

                    assemblyTypes[assembly] = rtle.Types.Where(t => t != null).ToArray()!;
                }
                catch (Exception ex)
                {
                    this.logger?.LogError("Failed to get types from assembly {AssemblyName}: {ExceptionMessage}", assembly.FullName, ex.Message);

                    assemblyTypes[assembly] = [];
                }
            }

            List<Type> pluginTypes =
                [.. assemblyTypes
                    .SelectMany(kvp => kvp.Value)
                    .Where(t => t != null && typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract)];

            IEnumerable<IGrouping<PluginMetadata, Type>> pluginGroups =
                pluginTypes.GroupBy(t => PluginMetadata.FromPlugin(t));

            List<Plugin> plugins = [];

            foreach (IGrouping<PluginMetadata, Type> group in pluginGroups)
            {
                PluginMetadata pluginMetadata = group.Key;

                List<PluginInterface> implementations = [];

                foreach (Type? concreteType in group)
                {
                    Assembly pluginAssembly = concreteType.Assembly;

                    List<Type> pluginInterfaces =
                        [.. concreteType
                            .GetInterfaces()
                            .Where(i => typeof(IPlugin).IsAssignableFrom(i) && i != typeof(IPlugin))];

                    foreach (Type? interfaceType in pluginInterfaces)
                        implementations.Add(new PluginInterface(pluginAssembly, concreteType, interfaceType));
                }

                Type? primaryType = group.FirstOrDefault();
                if (primaryType == null)
                {
                    this.logger?.LogError("Plugin discovery failed: No plugin types found for metadata {PluginName} (ID={PluginID}).", pluginMetadata.Name, pluginMetadata.PluginID);

                    throw new InvalidOperationException($"No plugin types found in group for metadata: {pluginMetadata.Name} ({pluginMetadata.PluginID})");
                }

                Assembly? primaryAssembly = primaryType.Assembly;
                if (primaryAssembly == null)
                {
                    this.logger?.LogError("Plugin discovery failed: No primary assembly found for plugin type {PluginType}.", primaryType.FullName);

                    throw new InvalidOperationException($"No primary assembly found for plugin type: {primaryType.FullName}");
                }

                plugins.Add(new Plugin(primaryAssembly!, primaryType!, pluginMetadata, implementations));
            }

            return plugins;
        }

        #endregion
    }
}