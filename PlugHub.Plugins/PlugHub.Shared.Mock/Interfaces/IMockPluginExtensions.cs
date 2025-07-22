using PlugHub.Shared.Interfaces;

namespace PlugHub.Shared.Mock.Interfaces
{
    public interface IEchoSuccessHandler : IPlugin
    {
        List<EchoSuccessDescriptor> GetEchoSuccessDescriptors();
    }

    public interface IEchoErrorHandler : IPlugin
    {
        List<EchoErrorDescriptor> GetEchoErrorDescriptors();
    }
}
