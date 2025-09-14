using Microsoft.Extensions.Logging;
using PlugHub.Shared.Interfaces.Services.Plugins;
using PlugHub.Shared.Mock.Interfaces.Plugins;
using PlugHub.Shared.Mock.Interfaces.Services;

namespace PlugHub.Plugin.Mock.Services
{
    /// <summary>
    /// Extensible echo service that accepts handler plugins through constructor injection.
    /// Other plugins implementing IEchoSuccessHandler or IEchoErrorHandler are automatically injected.
    /// </summary>
    public class EchoService : IEchoService
    {
        protected readonly ILogger<IEchoService> Logger;
        protected readonly IEnumerable<EchoResultDescriptor> EchoDescriptors;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<MessageErrorEventArgs>? MessageError;

        public EchoService(ILogger<IEchoService> logger, IPluginResolver resolver, IEnumerable<IEchoResultHandler> resultHandlers)
        {
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(resolver);
            ArgumentNullException.ThrowIfNull(resultHandlers);

            this.Logger = logger;

            this.EchoDescriptors = PluginsConfigs(resolver, resultHandlers);
        }

        /// <summary>
        /// Echoes the provided message while triggering events that registered handlers can process.
        /// </summary>
        public string Echo(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                MessageErrorEventArgs errorArgs = new(message, "The message provided was emtpy.");

                this.Logger?.LogInformation("[EchoService] Message Error!");

                foreach (EchoResultDescriptor descriptor in this.EchoDescriptors)
                    if (descriptor != null && descriptor.ProcessError != null)
                        descriptor.ProcessError(errorArgs, this);

                this.MessageError?.Invoke(this, errorArgs);
            }

            MessageReceivedEventArgs successArgs = new(message);

            this.Logger?.LogInformation("[EchoService] Message Received: {message}", message);

            foreach (EchoResultDescriptor descriptor in this.EchoDescriptors)
                if (descriptor != null && descriptor.ProcessSuccess != null)
                    descriptor.ProcessSuccess(successArgs, this);

            this.MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));

            return message;
        }


        private static IEnumerable<EchoResultDescriptor> PluginsConfigs(IPluginResolver resolver, IEnumerable<IEchoResultHandler> resultHandlers)
        {
            ArgumentNullException.ThrowIfNull(resultHandlers);

            List<EchoResultDescriptor> allDescriptors = [];

            foreach (IEchoResultHandler resultHandler in resultHandlers)
            {
                IEnumerable<EchoResultDescriptor> descriptors = resultHandler.GetEchoResultDescriptors();

                allDescriptors.AddRange(descriptors);
            }

            return resolver.ResolveDescriptors(allDescriptors);
        }
    }
}
