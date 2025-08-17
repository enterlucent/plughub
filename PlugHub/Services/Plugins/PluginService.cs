using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace PlugHub.Services.Plugins
{
    public class PluginService : IPluginService
    {
        private readonly ILogger<IPluginService> logger;
        private readonly SemaphoreSlim iolock = new(1, 1);

        public PluginService(ILogger<IPluginService> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
        }

        #region PlugHub.Services.PluginService Plugin Instances

        public TPlugin? GetLoadedPlugin<TPlugin>(PluginInterface pluginInterface) where TPlugin : PluginBase
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);

            this.iolock.Wait();

            try
            {
                bool fileExists = File.Exists(pluginInterface.AssemblyLocation);

                if (!fileExists)
                {
                    this.logger.LogWarning("Plugin assembly does not exist: {AssemblyPath}", pluginInterface.AssemblyLocation);
                    return null;
                }

                Assembly assembly = Assembly.LoadFrom(pluginInterface.AssemblyLocation);
                Type? type = assembly.GetType(pluginInterface.ImplementationName);

                if (type == null)
                {
                    this.logger.LogWarning("Type {TypeName} not found in assembly {AssemblyName}.", pluginInterface.ImplementationName, pluginInterface.AssemblyName);
                    return null;
                }

                // Validate the type is a subclass of PluginBase and concrete (non-abstract)
                bool isSubclass = type.IsSubclassOf(typeof(PluginBase));
                bool isNotAbstract = !type.IsAbstract;
                bool isValidType = isSubclass && isNotAbstract;

                if (isValidType)
                {
                    object? instance = Activator.CreateInstance(type);
                    bool instanceCreated = instance != null;
                    bool instanceIsCorrectType = instance is TPlugin;

                    if (instanceCreated && instanceIsCorrectType)
                    {
                        this.logger.LogInformation("Loaded plugin: {PluginName} ({TypeName})", pluginInterface.AssemblyName, pluginInterface.ImplementationName);

                        return (TPlugin)instance!;
                    }
                    else
                    {
                        this.logger.LogWarning("Failed to create instance of type {TypeName} from assembly {AssemblyName}.", pluginInterface.ImplementationName, pluginInterface.AssemblyName);
                        return null;
                    }
                }
                else
                {
                    this.logger.LogWarning("Type {TypeName} in assembly {AssemblyName} is not a subclass of {PluginBase} or is abstract.", pluginInterface.ImplementationName, pluginInterface.AssemblyName, nameof(PluginBase));
                    return null;
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError("Failed to load plugin {TypeName} from {AssemblyPath}. Exception: {ExceptionMessage}", pluginInterface.ImplementationName, pluginInterface.AssemblyLocation, ex.Message);
                return null;
            }
            finally
            {
                this.iolock.Release();
            }
        }
        public TInterface? GetLoadedInterface<TInterface>(PluginInterface pluginInterface) where TInterface : class
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);

            PluginBase? instance = this.GetLoadedPlugin<PluginBase>(pluginInterface);

            return instance as TInterface;
        }

        #endregion

        #region PlugHub.Services.PluginService: Discovery

        public IEnumerable<PluginReference> Discover(string pluginDirectory)
        {
            ArgumentNullException.ThrowIfNull(pluginDirectory);

            List<Assembly> assemblies = this.LoadAssembliesFromDirectory(pluginDirectory);
            Dictionary<Assembly, Type[]> assemblyTypes = this.ExtractTypesFromAssemblies(assemblies);
            List<Type> pluginTypes = FilterPluginTypes(assemblyTypes);
            IEnumerable<IGrouping<PluginMetadata, Type>> pluginGroups = pluginTypes.GroupBy(t => PluginMetadata.FromPlugin(t));

            return this.BuildPluginsFromGroups(pluginGroups);
        }
        private List<Assembly> LoadAssembliesFromDirectory(string pluginDirectory)
        {
            List<Assembly> assemblies = [];

            this.iolock.Wait();

            try
            {
                string[] assemblyPaths = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.AllDirectories);

                foreach (string assemblyPath in assemblyPaths)
                {
                    try
                    {
                        Assembly assembly = Assembly.LoadFrom(assemblyPath);
                        assemblies.Add(assembly);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError("Failed to load assembly {AssemblyPath}: {ExceptionMessage}", assemblyPath, ex.Message);
                    }
                }
            }
            finally
            {
                this.iolock.Release();
            }

            return assemblies;
        }
        private Dictionary<Assembly, Type[]> ExtractTypesFromAssemblies(List<Assembly> assemblies)
        {
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
                    {
                        if (loaderEx != null)
                        {
                            this.logger.LogError("Loader exception in assembly {AssemblyName}: {ExceptionMessage}", assembly.FullName, loaderEx.Message);
                        }
                    }

                    Type[] validTypes = rtle.Types.Where(t => t != null).ToArray()!;

                    assemblyTypes[assembly] = validTypes;
                }
                catch (Exception ex)
                {
                    this.logger.LogError("Failed to get types from assembly {AssemblyName}: {ExceptionMessage}", assembly.FullName, ex.Message);

                    assemblyTypes[assembly] = [];
                }
            }

            return assemblyTypes;
        }
        private static List<Type> FilterPluginTypes(Dictionary<Assembly, Type[]> assemblyTypes)
        {
            List<Type> pluginTypes = [];

            foreach (Type? type in assemblyTypes.SelectMany(kvp => kvp.Value))
            {
                if (type != null)
                {
                    bool derivesFromPluginBase = type.IsSubclassOf(typeof(PluginBase));
                    bool isNotAbstract = !type.IsAbstract;
                    bool isValidPluginType = derivesFromPluginBase && isNotAbstract;

                    if (isValidPluginType)
                    {
                        pluginTypes.Add(type);
                    }
                }
            }

            return pluginTypes;
        }
        private List<PluginReference> BuildPluginsFromGroups(IEnumerable<IGrouping<PluginMetadata, Type>> pluginGroups)
        {
            List<PluginReference> plugins = [];

            foreach (IGrouping<PluginMetadata, Type> group in pluginGroups)
            {
                PluginMetadata pluginMetadata = group.Key;
                List<PluginInterface> implementations = [];

                foreach (Type concreteType in group)
                {
                    List<PluginInterface> typeInterfaces = ExtractPluginInterfaces(concreteType);

                    implementations.AddRange(typeInterfaces);
                }

                Type? primaryType = group.FirstOrDefault();

                if (primaryType != null)
                {
                    Assembly primaryAssembly = primaryType.Assembly;
                    PluginReference plugin = new(primaryAssembly, primaryType, pluginMetadata, implementations);

                    plugins.Add(plugin);
                }
                else
                {
                    this.logger.LogError("Plugin discovery failed: No plugin types found for metadata {PluginName} (ID={PluginID}).", pluginMetadata.Name, pluginMetadata.PluginID);

                    throw new InvalidOperationException($"No plugin types found in group for metadata: {pluginMetadata.Name} ({pluginMetadata.PluginID})");
                }
            }

            return plugins;
        }
        private static List<PluginInterface> ExtractPluginInterfaces(Type concreteType)
        {
            List<PluginInterface> implementations = [];
            Assembly pluginAssembly = concreteType.Assembly;

            Type[] allInterfaces = concreteType.GetInterfaces();

            foreach (Type interfaceType in allInterfaces)
            {
                bool implementsPlugin = typeof(IPlugin).IsAssignableFrom(interfaceType);
                bool isNotBasePlugin = interfaceType != typeof(IPlugin);
                bool isValidInterface = implementsPlugin && isNotBasePlugin;

                if (isValidInterface)
                {
                    PluginInterface pluginInterface = new(pluginAssembly, concreteType, interfaceType);
                    implementations.Add(pluginInterface);
                }
            }

            return implementations;
        }

        #endregion
    }
}
