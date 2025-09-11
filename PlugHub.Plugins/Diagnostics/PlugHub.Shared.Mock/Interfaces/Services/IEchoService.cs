namespace PlugHub.Shared.Mock.Interfaces.Services
{
    /// <summary>
    /// Event arguments for the <see cref="IEchoService.MessageReceived"/> event.
    /// Carries the message that was echoed by the service.
    /// </summary>
    /// <param name="message">The echoed message string.</param>
    /// <remarks>Initializes a new instance of the <see cref="MessageReceivedEventArgs"/> class with the specified message.</remarks>
    public class MessageReceivedEventArgs(string message) : EventArgs()
    {
        /// <summary>
        /// Gets the message received by the echo service.
        /// </summary>
        public string Message { get; } = message;
    }

    /// <summary>
    /// Carries event data for a message-processing error within the plugin infrastructure.
    /// </summary>
    /// <param name="message">The original message associated with the error event.</param>
    /// <param name="error">A descriptive error string explaining what went wrong.</param>
    /// <remarks>
    /// This class extends <see cref="EventArgs"/> and is intended for use with events that 
    /// must communicate both the message content that triggered the event and any resulting error information.
    /// </remarks>
    public class MessageErrorEventArgs(string message, string error) : EventArgs()
    {
        /// <summary>
        /// Gets the original message that triggered the error event.
        /// </summary>
        public string Message { get; } = message;

        /// <summary>
        /// Gets a description of the error that occurred during message processing.
        /// </summary>
        public string Error { get; } = error;
    }


    /// <summary>
    /// Defines an echo service interface for plugins.
    /// Provides an Echo method and an event triggered when a message is echoed.
    /// </summary>
    public interface IEchoService
    {
        /// <summary>
        /// Event raised each time a message is echoed via the <see cref="Echo"/> method.
        /// </summary>
        event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        /// <summary>
        /// Event raised each time a message errors via the <see cref="Echo"/> method.
        /// </summary>
        event EventHandler<MessageErrorEventArgs>? MessageError;

        /// <summary>
        /// Echoes the provided message.
        /// Raises the <see cref="MessageReceived"/> event after successful echo.
        /// </summary>
        /// <param name="message">The string to echo.</param>
        /// <returns>The original message.</returns>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ArgumentException"/>
        string Echo(string message);
    }
}
