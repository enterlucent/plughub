using PlugHub.Shared.Attributes;
using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Mock.Interfaces.Services;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Mock.Interfaces.Plugins
{
    /// <summary>
    /// Descriptor providing executable processing logic for both success and error events in EchoService.
    /// Bridges IEchoResultHandler interface contracts with actual Action delegates.
    /// </summary>
    public record EchoResultDescriptor(
        Guid PluginID,
        Guid DescriptorID,
        string Version,
        Action<MessageReceivedEventArgs, IEchoService>? ProcessSuccess = null,
        Action<MessageErrorEventArgs, IEchoService>? ProcessError = null,
        IEnumerable<PluginDescriptorReference>? LoadBefore = null,
        IEnumerable<PluginDescriptorReference>? LoadAfter = null,
        IEnumerable<PluginDescriptorReference>? DependsOn = null,
        IEnumerable<PluginDescriptorReference>? ConflictsWith = null
    ) : PluginDescriptor(PluginID, DescriptorID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Plugin-level handler invoked on both successful echo and echo error.
    /// Supports extensibility of EchoService behavior by contributing a combined success/error processing pipeline.
    /// </summary>
    [DescriptorProvider("GetEchoResultDescriptors")]
    public interface IEchoResultHandler : IPlugin
    {
        /// <summary>
        /// Returns the descriptors containing success and error handling actions contributed by this handler.
        /// Aggregation across plugins creates an extensible processing pipeline for echo results.
        /// </summary>
        List<EchoResultDescriptor> GetEchoResultDescriptors();
    }
}