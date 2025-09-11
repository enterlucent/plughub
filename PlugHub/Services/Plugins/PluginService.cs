using Microsoft.Extensions.Logging;
using PlugHub.Shared.Attributes;
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

        #region PluginService: Plugin Instances

        public TPlugin? GetLoadedPlugin<TPlugin>(PluginInterface pluginInterface) where TPlugin : PluginBase
        {
            ArgumentNullException.ThrowIfNull(pluginInterface);

            this.iolock.Wait();

            try
            {
                if (!File.Exists(pluginInterface.AssemblyLocation))
                {
                    this.logger.LogWarning("[IPluginService] Plugin assembly does not exist: {AssemblyPath}", pluginInterface.AssemblyLocation);

                    return null;
                }

                Assembly assembly = Assembly.LoadFrom(pluginInterface.AssemblyLocation);
                Type? type = assembly.GetType(pluginInterface.ImplementationName);

                if (type == null)
                {
                    this.logger.LogWarning("[IPluginService] Type {TypeName} not found in assembly {AssemblyName}.", pluginInterface.ImplementationName, pluginInterface.AssemblyName);

                    return null;
                }

                bool isSubclass = type.IsSubclassOf(typeof(PluginBase));
                bool isNotAbstract = !type.IsAbstract;

                if (isSubclass && isNotAbstract)
                {
                    object? instance = Activator.CreateInstance(type);

                    bool instanceCreated = instance != null;
                    bool instanceIsCorrectType = instance is TPlugin;

                    if (instanceCreated && instanceIsCorrectType)
                    {
                        this.logger.LogInformation("[IPluginService] Loaded plugin: {PluginName} ({TypeName})", pluginInterface.AssemblyName, pluginInterface.ImplementationName);

                        return (TPlugin)instance!;
                    }
                    else
                    {
                        this.logger.LogError("[IPluginService] Failed to create instance of type {TypeName} from assembly {AssemblyName}.", pluginInterface.ImplementationName, pluginInterface.AssemblyName);

                        return null;
                    }
                }
                else return null;
            }
            catch (Exception ex)
            {
                this.logger.LogError("[IPluginService] Failed to load plugin {TypeName} from {AssemblyPath}. Exception: {ExceptionMessage}", pluginInterface.ImplementationName, pluginInterface.AssemblyLocation, ex.Message);

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

        #region PluginService: Plugin Interface Data

        public DescriptorProviderAttribute? GetDescriptorProviderAttribute(string interfaceFullName)
        {
            if (string.IsNullOrWhiteSpace(interfaceFullName))
                throw new ArgumentNullException(nameof(interfaceFullName));

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? interfaceType = null;

                try
                {
                    interfaceType = assembly.GetType(interfaceFullName, throwOnError: false, ignoreCase: false);
                }
                catch { /* nothing to see here */ }

                if (interfaceType != null && interfaceType.IsInterface)
                {
                    DescriptorProviderAttribute? attr = 
                        interfaceType.GetCustomAttribute<DescriptorProviderAttribute>(inherit: false);

                    if (attr != null) return attr;
                }
            }

            return null;
        }

        #endregion

        #region PluginService: Discovery

        public IEnumerable<PluginReference> Discover(string pluginDirectory)
        {
            ArgumentNullException.ThrowIfNull(pluginDirectory);

            List<Assembly> assemblies = this.LoadAssembliesFromDirectory(pluginDirectory);

            Dictionary<Assembly, Type[]> assemblyTypes = this.ExtractTypesFromAssemblies(assemblies);

            List<Type> filteredpluginTypes = FilterPluginTypes(assemblyTypes);
            List<Type> pluginTypes = FilterInterfaceTypes(filteredpluginTypes);

            IEnumerable<IGrouping<PluginMetadata, Type>> pluginGroups = pluginTypes.GroupBy(t => PluginMetadata.FromPlugin(t));

            HashSet<Guid> seenPluginIds = [];

            List<IGrouping<PluginMetadata, Type>> uniquePluginGroups = [];

            foreach (IGrouping<PluginMetadata, Type> group in pluginGroups)
            {
                Guid pluginId = group.Key.PluginID;

                if (seenPluginIds.Contains(pluginId))
                {
                    this.logger.LogError("[IPluginService] Duplicate plugin ID detected and ignored: {PluginID}", pluginId);

                    continue;
                }

                seenPluginIds.Add(pluginId);

                uniquePluginGroups.Add(group);
            }

            return this.BuildPluginsFromGroups(uniquePluginGroups);
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
                        this.logger.LogError("[IPluginService] Failed to load assembly {AssemblyPath}: {ExceptionMessage}", assemblyPath, ex.Message);
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
                            this.logger.LogError("[IPluginService] Loader exception in assembly {AssemblyName}: {ExceptionMessage}", assembly.FullName, loaderEx.Message);
                        }
                    }

                    Type[] validTypes = rtle.Types.Where(t => t != null).ToArray()!;

                    assemblyTypes[assembly] = validTypes;
                }
                catch (Exception ex)
                {
                    this.logger.LogError("[IPluginService] Failed to get types from assembly {AssemblyName}: {ExceptionMessage}", assembly.FullName, ex.Message);

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

                    if (!(derivesFromPluginBase && isNotAbstract))
                        continue;

                    pluginTypes.Add(type);
                }
            }
            return pluginTypes;
        }
        private static List<Type> FilterInterfaceTypes(List<Type> pluginInterfaceTypes)
        {
            List<Type> validInterfaces = [];

            foreach (Type interfaceType in pluginInterfaceTypes)
            {
                DescriptorProviderAttribute? attr = null;

                Type[] allInterfaces = [interfaceType, .. interfaceType.GetInterfaces()];

                foreach (Type it in allInterfaces)
                {
                    attr = it.GetCustomAttribute<DescriptorProviderAttribute>(inherit: false);

                    if (attr != null) break;
                }

                if (attr == null) continue;

                string methodName = attr.DescriptorAccessorName;

                MethodInfo? methodInfo = interfaceType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

                if (methodInfo == null) continue;

                validInterfaces.Add(interfaceType);
            }

            return validInterfaces;
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

                if (implementsPlugin && isNotBasePlugin)
                {
                    PluginInterface pluginInterface = new(pluginAssembly, concreteType, interfaceType);

                    implementations.Add(pluginInterface);
                }
            }

            return implementations;
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
                    this.logger.LogError("[IPluginService] Plugin discovery failed: No plugin types found for metadata {PluginName} (ID={PluginID}).", pluginMetadata.Name, pluginMetadata.PluginID);

                    throw new InvalidOperationException($"No plugin types found in group for metadata: {pluginMetadata.Name} ({pluginMetadata.PluginID})");
                }
            }

            return plugins;
        }

        #endregion
    }
}
