using PlugHub.Shared.Models;

namespace PlugHub.Shared.Mock.Interfaces
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
}
