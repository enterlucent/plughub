using PlugHub.Shared.Interfaces.Plugins;
using PlugHub.Shared.Mock.Interfaces.Services;
using PlugHub.Shared.Models.Plugins;

namespace PlugHub.Shared.Mock.Interfaces.Plugins
{
    /// <summary>
    /// Descriptor providing executable success processing logic to EchoService.
    /// Bridges IEchoSuccessHandler interface contracts with actual Action delegates.
    /// </summary>
    public record EchoSuccessDescriptor(
        Guid PluginID,
        Guid InterfaceID,
        string Version,
        Action<MessageReceivedEventArgs>? ProcessSuccess = null,
        IEnumerable<PluginInterfaceReference>? LoadBefore = null,
        IEnumerable<PluginInterfaceReference>? LoadAfter = null,
        IEnumerable<PluginInterfaceReference>? DependsOn = null,
        IEnumerable<PluginInterfaceReference>? ConflictsWith = null
    ) : PluginDescriptor(PluginID, InterfaceID, Version, LoadBefore, LoadAfter, DependsOn, ConflictsWith);

    /// <summary>
    /// Plugin-level handler invoked after a successful echo.
    /// Multiple success handlers can be registered, forming an open-ended
    /// pipeline that other plugins can extend indefinitely.
    /// </summary>
    public interface IEchoSuccessHandler : IPlugin
    {
        /// <summary>
        /// Returns the post-success actions this handler contributes.
        /// Handlers from different plugins are aggregated, enabling true recursive extension.
        /// </summary>
        List<EchoSuccessDescriptor> GetEchoSuccessDescriptors();
    }
}
