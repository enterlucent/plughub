using Microsoft.Extensions.Logging;
using PlugHub.Shared.Mock.Interfaces;


namespace PlugHub.Plugin.Mock.Services
{
    public class EchoService(
        ILogger<IEchoService> logger,
        IEnumerable<IEchoSuccessHandler> successHandlers,
        IEnumerable<IEchoErrorHandler> errorHandlers) : IEchoService
    {
        protected readonly ILogger<IEchoService> Logger = logger;

        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<MessageErrorEventArgs>? MessageError;

        protected IEnumerable<IEchoSuccessHandler> SuccessHandler = successHandlers ?? [];
        protected IEnumerable<IEchoErrorHandler> ErrorHandlers = errorHandlers ?? [];

        public string Echo(string message)
        {
            ArgumentNullException.ThrowIfNull(message, nameof(message));

            if (string.IsNullOrWhiteSpace(message))
                this.MessageError?.Invoke(this, new MessageErrorEventArgs(message, "The message provided was emtpy."));

            this.Logger?.LogInformation("Message Received: {message}", message);

            this.MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));

            return message;
        }
    }
}