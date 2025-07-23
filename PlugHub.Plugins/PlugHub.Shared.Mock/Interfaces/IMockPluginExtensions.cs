using PlugHub.Shared.Interfaces;

namespace PlugHub.Shared.Mock.Interfaces
{
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
