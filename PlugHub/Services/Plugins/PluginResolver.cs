using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Models.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;


namespace PlugHub.Services.Plugins
{
    public class PluginResolver : IPluginResolver
    {
        private readonly ILogger<PluginResolver> logger;

        public PluginResolver(ILogger<PluginResolver> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
        }

        #region PluginResolver: Resolution

        public PluginResolutionContext<TDescriptor> ResolveContext<TDescriptor>(IEnumerable<TDescriptor> descriptors) where TDescriptor : PluginDescriptor
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            List<TDescriptor> descriptorsList = [.. descriptors];
            PluginResolutionContext<TDescriptor> context = new(descriptorsList);

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

        private void ProcessDependencies<TDescriptor>(TDescriptor descriptor, PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginInterfaceReference dep in descriptor.DependsOn ?? [])
            {
                bool hasDep = context.IdToDescriptor.TryGetValue(dep.InterfaceID, out TDescriptor? depDesc);

                if (!hasDep)
                {
                    this.logger.LogWarning("[PluginResolver] Interface {InterfaceID} depends on {DependencyID} (version in [{Min}, {Max}]), but it is missing or version mismatch (found: missing).", descriptor.DescriptorID, dep.InterfaceID, dep.MinVersion, dep.MaxVersion);

                    context.DependencyDisabled.Add(descriptor);
                }
                else if (depDesc != null)
                {
                    bool matchesDep = dep.Matches(depDesc.PluginID, depDesc.DescriptorID, depDesc.Version);

                    if (!matchesDep)
                    {
                        this.logger.LogWarning("[PluginResolver] Interface {InterfaceID} depends on {DependencyID} (version in [{Min}, {Max}]), but it is missing or version mismatch (found: {FoundVersion}).", descriptor.DescriptorID, dep.InterfaceID, dep.MinVersion, dep.MaxVersion, depDesc.Version);

                        context.DependencyDisabled.Add(descriptor);
                    }
                }
                else
                {
                    this.logger.LogCritical("[PluginResolver] Critical error: TryGetValue returned true but descriptor is null for {InterfaceID}. This indicates a serious backend data integrity issue.", dep.InterfaceID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for interface {dep.InterfaceID} despite successful lookup");
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

                foreach (PluginInterfaceReference conflict in descriptor.ConflictsWith)
                {
                    bool matchesConflict = conflict.Matches(otherDescriptor.PluginID, otherDescriptor.DescriptorID, otherDescriptor.Version);

                    if (matchesConflict)
                    {
                        this.logger.LogWarning("[PluginResolver] Interface {InterfaceID} conflicts with {ConflictID} (version in [{Min}, {Max}]), but both are enabled (found: {FoundVersion}).", descriptor.DescriptorID, conflict.InterfaceID, conflict.MinVersion, conflict.MaxVersion, otherDescriptor.Version);

                        context.ConflictDisabled.Add(descriptor);

                        return;
                    }
                }
            }
        }
        private void ProcessLoadBefore<TDescriptor>(TDescriptor descriptor, PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginInterfaceReference before in descriptor.LoadBefore ?? [])
            {
                bool hasBefore = context.IdToDescriptor.TryGetValue(before.InterfaceID, out TDescriptor? beforeDesc);

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
                    this.logger.LogCritical("[PluginResolver] Critical error: TryGetValue returned true but descriptor is null for {InterfaceID}. This indicates a serious backend data integrity issue.", before.InterfaceID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for interface {before.InterfaceID} despite successful lookup");
                }
            }
        }
        private void ProcessLoadAfter<TDescriptor>(TDescriptor descriptor, PluginResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginInterfaceReference after in descriptor.LoadAfter ?? [])
            {
                bool hasAfter = context.IdToDescriptor.TryGetValue(after.InterfaceID, out TDescriptor? afterDesc);

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
                    this.logger.LogCritical("[PluginResolver] Critical error: TryGetValue returned true but descriptor is null for {InterfaceID}. This indicates a serious backend data integrity issue.", after.InterfaceID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for interface {after.InterfaceID} despite successful lookup");
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