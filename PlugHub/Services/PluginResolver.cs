using Microsoft.Extensions.Logging;
using PlugHub.Shared;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;


namespace PlugHub.Services
{
    public class PluginResolver : IPluginResolver
    {
        private class ResolutionContext<TDescriptor>(List<TDescriptor> descriptors) where TDescriptor : PluginDescriptor
        {
            public Dictionary<Guid, TDescriptor> IdToDescriptor { get; } =
                descriptors.ToDictionary(d => d.InterfaceID, d => d);

            public HashSet<TDescriptor> InvalidDescriptors { get; } = [];

            public Dictionary<TDescriptor, HashSet<TDescriptor>> Graph { get; } =
                descriptors.ToDictionary(d => d, _ => new HashSet<TDescriptor>());
        }

        private readonly ILogger<PluginResolver> logger;

        public PluginResolver(ILogger<PluginResolver> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            this.logger = logger;
        }

        #region PluginResolver: Resolution

        public IEnumerable<TDescriptor> ResolveDescriptors<TDescriptor>(IEnumerable<TDescriptor> descriptors) where TDescriptor : PluginDescriptor
        {
            ArgumentNullException.ThrowIfNull(descriptors);

            List<TDescriptor> descriptorsList = [.. descriptors];
            ResolutionContext<TDescriptor> context = new(descriptorsList);

            this.ProcessAllDescriptorRelationships(descriptorsList, context);

            RemoveInvalidDescriptors(context);

            return context.Graph.Keys.TopologicalSort(d => context.Graph[d]);
        }

        private void ProcessAllDescriptorRelationships<TDescriptor>(List<TDescriptor> descriptorsList, ResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (TDescriptor descriptor in descriptorsList)
            {
                this.ProcessDependencies(descriptor, context);
                this.ProcessConflicts(descriptor, context);
                this.ProcessLoadOrder(descriptor, context);
            }
        }
        private void ProcessDependencies<TDescriptor>(TDescriptor descriptor, ResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginInterfaceReference dep in descriptor.DependsOn ?? [])
            {
                bool hasDep = context.IdToDescriptor.TryGetValue(dep.InterfaceID, out TDescriptor? depDesc);

                if (!hasDep)
                {
                    this.logger.LogWarning("Interface {InterfaceID} depends on {DependencyID} (version in [{Min}, {Max}]), but it is missing or version mismatch (found: missing).", descriptor.InterfaceID, dep.InterfaceID, dep.MinVersion, dep.MaxVersion);

                    context.InvalidDescriptors.Add(descriptor);
                }
                else if (depDesc != null)
                {
                    bool matchesDep = dep.Matches(depDesc.PluginID, depDesc.InterfaceID, depDesc.Version);

                    if (!matchesDep)
                    {
                        this.logger.LogWarning("Interface {InterfaceID} depends on {DependencyID} (version in [{Min}, {Max}]), but it is missing or version mismatch (found: {FoundVersion}).", descriptor.InterfaceID, dep.InterfaceID, dep.MinVersion, dep.MaxVersion, depDesc.Version);

                        context.InvalidDescriptors.Add(descriptor);
                    }
                }
                else
                {
                    this.logger.LogError("Critical error: TryGetValue returned true but descriptor is null for {InterfaceID}. This indicates a serious backend data integrity issue.", dep.InterfaceID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for interface {dep.InterfaceID} despite successful lookup");
                }
            }
        }
        private void ProcessConflicts<TDescriptor>(TDescriptor descriptor, ResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (PluginInterfaceReference conflict in descriptor.ConflictsWith ?? [])
            {
                bool hasConflict = context.IdToDescriptor.TryGetValue(conflict.InterfaceID, out TDescriptor? conflictDesc);

                if (!hasConflict)
                {
                    continue;
                }
                else if (conflictDesc != null)
                {
                    bool matchesConflict = conflict.Matches(conflictDesc.PluginID, conflictDesc.InterfaceID, conflictDesc.Version);

                    if (matchesConflict)
                    {
                        this.logger.LogWarning("Interface {InterfaceID} conflicts with {ConflictID} (version in [{Min}, {Max}]), but both are enabled (found: {FoundVersion}).", descriptor.InterfaceID, conflict.InterfaceID, conflict.MinVersion, conflict.MaxVersion, conflictDesc.Version);

                        context.InvalidDescriptors.Add(descriptor);
                    }
                }
                else
                {
                    this.logger.LogError("Critical error: TryGetValue returned true but descriptor is null for {InterfaceID}. This indicates a serious backend data integrity issue.", conflict.InterfaceID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for interface {conflict.InterfaceID} despite successful lookup");
                }
            }
        }

        private void ProcessLoadOrder<TDescriptor>(TDescriptor descriptor, ResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            this.ProcessLoadBefore(descriptor, context);
            this.ProcessLoadAfter(descriptor, context);
        }
        private void ProcessLoadBefore<TDescriptor>(TDescriptor descriptor, ResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
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
                    bool matchesBefore = before.Matches(beforeDesc.PluginID, beforeDesc.InterfaceID, beforeDesc.Version);
                    bool notInvalid = !context.InvalidDescriptors.Contains(beforeDesc);

                    if (matchesBefore && notInvalid)
                    {
                        context.Graph[beforeDesc].Add(descriptor);
                    }
                }
                else
                {
                    this.logger.LogError("Critical error: TryGetValue returned true but descriptor is null for {InterfaceID}. This indicates a serious backend data integrity issue.", before.InterfaceID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for interface {before.InterfaceID} despite successful lookup");
                }
            }
        }
        private void ProcessLoadAfter<TDescriptor>(TDescriptor descriptor, ResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
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
                    bool matchesAfter = after.Matches(afterDesc.PluginID, afterDesc.InterfaceID, afterDesc.Version);
                    bool notInvalid = !context.InvalidDescriptors.Contains(afterDesc);

                    if (matchesAfter && notInvalid)
                    {
                        context.Graph[descriptor].Add(afterDesc);
                    }
                }
                else
                {
                    this.logger.LogError("Critical error: TryGetValue returned true but descriptor is null for {InterfaceID}. This indicates a serious backend data integrity issue.", after.InterfaceID);

                    throw new InvalidOperationException($"Descriptor lookup returned null for interface {after.InterfaceID} despite successful lookup");
                }
            }
        }

        private static void RemoveInvalidDescriptors<TDescriptor>(ResolutionContext<TDescriptor> context) where TDescriptor : PluginDescriptor
        {
            foreach (TDescriptor invalid in context.InvalidDescriptors)
            {
                context.Graph.Remove(invalid);
            }
        }

        #endregion
    }
}