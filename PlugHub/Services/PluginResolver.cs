using Microsoft.Extensions.Logging;
using PlugHub.Shared;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlugHub.Services
{
    public class PluginResolver(ILogger<PluginResolver> logger) : IPluginResolver
    {
        private readonly ILogger<PluginResolver> logger = logger
            ?? throw new ArgumentNullException();

        public IEnumerable<TDescriptor> ResolveDescriptors<TDescriptor>(IEnumerable<TDescriptor> descriptors) where TDescriptor : PluginDescriptor
        {
            List<TDescriptor> descriptorsList = [.. descriptors];

            Dictionary<Guid, TDescriptor> idToDescriptor =
                descriptorsList.ToDictionary(d => d.InterfaceID, d => d);

            Dictionary<TDescriptor, HashSet<TDescriptor>> graph =
                descriptorsList.ToDictionary(d => d, _ => new HashSet<TDescriptor>());

            HashSet<TDescriptor> invalidDescriptors = [];

            foreach (TDescriptor descriptor in descriptorsList)
            {
                Guid descriptorId = descriptor.InterfaceID;

                foreach (PluginInterfaceReference dep in descriptor.DependsOn ?? [])
                {
                    if (!idToDescriptor.TryGetValue(dep.InterfaceID, out TDescriptor? depDesc) ||
                        !dep.Matches(depDesc.PluginID, depDesc.InterfaceID, depDesc.Version))
                    {
                        this.logger.LogWarning("Interface {InterfaceID} depends on {DependencyID} (version in [{Min}, {Max}]), but it is missing or version mismatch (found: {FoundVersion}).", descriptorId, dep.InterfaceID, dep.MinVersion, dep.MaxVersion, depDesc?.Version ?? "missing");

                        invalidDescriptors.Add(descriptor);
                    }
                }

                foreach (PluginInterfaceReference conflict in descriptor.ConflictsWith ?? [])
                {
                    if (idToDescriptor.TryGetValue(conflict.InterfaceID, out TDescriptor? conflictDesc) &&
                        conflict.Matches(conflictDesc.PluginID, conflictDesc.InterfaceID, conflictDesc.Version))
                    {
                        this.logger.LogWarning("Interface {InterfaceID} conflicts with {ConflictID} (version in [{Min}, {Max}]), but both are enabled (found: {FoundVersion}).", descriptorId, conflict.InterfaceID, conflict.MinVersion, conflict.MaxVersion, conflictDesc.Version);

                        invalidDescriptors.Add(descriptor);
                    }
                }

                foreach (PluginInterfaceReference before in descriptor.LoadBefore ?? [])
                {
                    if (idToDescriptor.TryGetValue(before.InterfaceID, out TDescriptor? beforeDesc) &&
                        before.Matches(before.PluginID, beforeDesc.InterfaceID, beforeDesc.Version) &&
                        !invalidDescriptors.Contains(beforeDesc))
                    {
                        graph[beforeDesc].Add(descriptor);
                    }
                }

                foreach (PluginInterfaceReference after in descriptor.LoadAfter ?? [])
                {
                    if (idToDescriptor.TryGetValue(after.InterfaceID, out TDescriptor? afterDesc) &&
                        after.Matches(after.PluginID, afterDesc.InterfaceID, afterDesc.Version) &&
                        !invalidDescriptors.Contains(afterDesc))
                    {
                        graph[descriptor].Add(afterDesc);
                    }
                }
            }

            foreach (TDescriptor invalid in invalidDescriptors)
                graph.Remove(invalid);

            IList<TDescriptor> value = graph.Keys.TopologicalSort(d => graph[d]);
            return value;
        }
    }
}