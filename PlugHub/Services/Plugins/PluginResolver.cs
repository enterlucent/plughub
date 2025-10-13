using Microsoft.Extensions.Logging;
using PlugHub.Shared.Attributes;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PlugHub.Services.Plugins
{
    public class PluginResolver : IPluginResolver
    {
        private readonly ILogger<IPluginResolver> logger;

        public PluginResolver(ILogger<IPluginResolver> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
        }

        #region PluginResolver: Resolution

        public PluginResolutionContext<TDescriptor> ResolveContext<TDescriptor>(IEnumerable<TDescriptor> descriptors) where TDescriptor : PluginDescriptor
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            HashSet<TDescriptor> duplicates = FilterDuplicates(descriptors, out List<TDescriptor> descriptorsList);

            foreach (TDescriptor duplicate in duplicates)
                this.logger.LogError(
                    "[PluginResolver] Duplicate DescriptorID {DescriptorID} for plugin {PluginID}, version {Version} detected and excluded.",
                    duplicate.DescriptorID,
                    duplicate.PluginID,
                    duplicate.Version);

            PluginResolutionContext<TDescriptor> context = new(descriptorsList, duplicates);

            foreach (TDescriptor descriptor in descriptorsList)
            {
                this.ProcessDependencies(descriptor, context);
                this.ProcessConflicts(descriptor, context);
                this.ProcessLoadBefore(descriptor, context);
                this.ProcessLoadAfter(descriptor, context);
            }

            RemoveInvalidDescriptors(context);

            return context;
        }
        public IEnumerable<TDescriptor> ResolveDescriptors<TDescriptor>(IEnumerable<TDescriptor> descriptors) where TDescriptor : PluginDescriptor
        {
            PluginResolutionContext<TDescriptor> context = this.ResolveContext(descriptors);

            return context.GetSorted();
        }
        public IReadOnlyList<TDescriptor> ResolveAndOrder<TInterface, TDescriptor>(IEnumerable<TInterface> plugins) where TInterface : class where TDescriptor : PluginDescriptor
        {
            ArgumentNullException.ThrowIfNull(plugins);

            Type ifaceType = typeof(TInterface);
            DescriptorProviderAttribute attr =
                ifaceType.GetCustomAttribute<DescriptorProviderAttribute>(inherit: false)
                    ?? throw new InvalidOperationException($"Interface {ifaceType.Name} is missing DescriptorProviderAttribute");

            MethodInfo accessor =
                ifaceType.GetMethod(attr.DescriptorAccessorName)
                    ?? throw new InvalidOperationException($"Descriptor accessor {attr.DescriptorAccessorName} not found on {ifaceType.Name}");

            List<TDescriptor> allDescriptors = [];

            foreach (TInterface plugin in plugins)
                if (accessor.Invoke(plugin, null) is IEnumerable<TDescriptor> descriptors)
                    allDescriptors.AddRange(descriptors);

            PluginResolutionContext<TDescriptor> context = this.ResolveContext(allDescriptors);
            IReadOnlyList<TDescriptor> sorted = context.GetSorted();

            return attr.SortContext switch
            {
                DescriptorSortContext.Forward => sorted,
                DescriptorSortContext.Reverse => [.. sorted.Reverse()],
                _ => sorted
            };
        }

        private static HashSet<TDescriptor> FilterDuplicates<TDescriptor>(IEnumerable<TDescriptor> descriptors, out List<TDescriptor> cleanDescriptors) where TDescriptor : PluginDescriptor
        {
            cleanDescriptors = [];
            HashSet<TDescriptor> duplicates = [];
            HashSet<Guid> seenIDs = [];

            foreach (TDescriptor descriptor in descriptors)
            {
                if (seenIDs.Add(descriptor.DescriptorID))
                    cleanDescriptors.Add(descriptor);
                else
                    duplicates.Add(descriptor);
            }
            return duplicates;
        }
        private void ProcessDependencies<TDescriptor>(TDescriptor descriptor, PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginDescriptorReference dep in descriptor.DependsOn ?? [])
            {
                bool hasDep = context.IdToDescriptor.TryGetValue(dep.DescriptorID, out TDescriptor? depDesc);

                if (!hasDep)
                {
                    this.logger.LogWarning("[PluginResolver] Descriptor {DescriptorID} depends on {DependencyID} (version in [{Min}, {Max}]), but it is missing or version mismatch (found: missing).", descriptor.DescriptorID, dep.DescriptorID, dep.MinVersion, dep.MaxVersion);

                    context.DependencyDisabled.Add(descriptor);
                }
                else if (depDesc != null)
                {
                    bool matchesDep = dep.Matches(depDesc.PluginID, depDesc.DescriptorID, depDesc.Version);

                    if (!matchesDep)
                    {
                        this.logger.LogWarning("[PluginResolver] Descriptor {DescriptorID} depends on {DependencyID} (version in [{Min}, {Max}]), but it is missing or version mismatch (found: {FoundVersion}).", descriptor.DescriptorID, dep.DescriptorID, dep.MinVersion, dep.MaxVersion, depDesc.Version);

                        context.DependencyDisabled.Add(descriptor);
                    }
                }
                else
                {
                    this.logger.LogCritical("[PluginResolver] Critical error: TryGetValue returned true but descriptor is null for {DescriptorID}. This indicates a serious backend data integrity issue.", dep.DescriptorID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for descriptor {dep.DescriptorID} despite successful lookup");
                }
            }
        }
        private void ProcessConflicts<TDescriptor>(TDescriptor descriptor, PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            if (descriptor.ConflictsWith == null) return;

            foreach (TDescriptor otherDescriptor in context.Graph.Keys)
            {
                if (otherDescriptor == descriptor)
                    continue;

                foreach (PluginDescriptorReference conflict in descriptor.ConflictsWith)
                {
                    bool matchesConflict = conflict.Matches(otherDescriptor.PluginID, otherDescriptor.DescriptorID, otherDescriptor.Version);

                    if (matchesConflict)
                    {
                        this.logger.LogWarning("[PluginResolver] Descriptor {DescriptorID} conflicts with {ConflictID} (version in [{Min}, {Max}]), but both are enabled (found: {FoundVersion}).", descriptor.DescriptorID, conflict.DescriptorID, conflict.MinVersion, conflict.MaxVersion, otherDescriptor.Version);

                        context.ConflictDisabled.Add(descriptor);

                        return;
                    }
                }
            }
        }
        private void ProcessLoadBefore<TDescriptor>(TDescriptor descriptor, PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginDescriptorReference before in descriptor.LoadBefore ?? [])
            {
                bool hasBefore = context.IdToDescriptor.TryGetValue(before.DescriptorID, out TDescriptor? beforeDesc);

                if (!hasBefore)
                {
                    continue;
                }
                else if (beforeDesc != null)
                {
                    bool matchesBefore = before.Matches(beforeDesc.PluginID, beforeDesc.DescriptorID, beforeDesc.Version);
                    bool notInvalid = !context.ConflictDisabled.Contains(beforeDesc) && !context.DependencyDisabled.Contains(beforeDesc);

                    if (matchesBefore && notInvalid)
                    {
                        context.Graph[beforeDesc].Add(descriptor);
                    }
                }
                else
                {
                    this.logger.LogCritical("[PluginResolver] Critical error: TryGetValue returned true but descriptor is null for {DescriptorID}. This indicates a serious backend data integrity issue.", before.DescriptorID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for descriptor {before.DescriptorID} despite successful lookup");
                }
            }
        }
        private void ProcessLoadAfter<TDescriptor>(TDescriptor descriptor, PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginDescriptorReference after in descriptor.LoadAfter ?? [])
            {
                bool hasAfter = context.IdToDescriptor.TryGetValue(after.DescriptorID, out TDescriptor? afterDesc);

                if (!hasAfter)
                {
                    continue;
                }
                else if (afterDesc != null)
                {
                    bool matchesAfter = after.Matches(afterDesc.PluginID, afterDesc.DescriptorID, afterDesc.Version);
                    bool notInvalid = !context.ConflictDisabled.Contains(afterDesc) && !context.DependencyDisabled.Contains(afterDesc);

                    if (matchesAfter && notInvalid)
                    {
                        context.Graph[descriptor].Add(afterDesc);
                    }
                }
                else
                {
                    this.logger.LogCritical("[PluginResolver] Critical error: TryGetValue returned true but descriptor is null for {DescriptorID}. This indicates a serious backend data integrity issue.", after.DescriptorID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for descriptor {after.DescriptorID} despite successful lookup");
                }
            }
        }

        private static void RemoveInvalidDescriptors<TDescriptor>(PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            IEnumerable<TDescriptor> allDisabled = context.ConflictDisabled
                    .Concat(context.DependencyDisabled)
                    .Distinct();

            foreach (TDescriptor invalid in allDisabled)
                context.Graph.Remove(invalid);
        }

        #endregion
    }
}