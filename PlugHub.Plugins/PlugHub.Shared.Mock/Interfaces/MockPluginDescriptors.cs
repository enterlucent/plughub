namespace PlugHub.Shared.Mock.Interfaces
{
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
