using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Mock.Interfaces.Services;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Mock.Interfaces.Plugins
{
    /// <summary>
    /// Descriptor providing executable error processing logic to EchoService.
    /// Bridges IEchoErrorHandler interface contracts with actual Action delegates.
    /// </summary>
    public record EchoErrorDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        Action<MessageErrorEventArgs>? ProcessError = null,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null
    ) : PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Plugin-level handler invoked when an echo operation fails.
    /// New error handlers can be introduced by additional plugins, extending the
    /// processing chain without any changes to the core service.
    /// </summary>
    public interface IEchoErrorHandler : IPlugin
    {
        /// <summary>
        /// Returns the error-handling actions this handler contributes.
        /// Aggregation across plugins creates a recursive error-processing pipeline.
        /// </summary>
        List<EchoErrorDescriptor> GetEchoErrorDescriptors();
    }
}
