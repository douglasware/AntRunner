namespace AntRunnerLib
{
    /// <summary>
    /// Represents the output of a thread run within the assistant orchestrator.
    /// This class contains the status, output, and conversation messages of the thread run.
    /// </summary>
    public class ThreadRunOutput
    {
        /// <summary>
        /// Gets or sets the output of the thread run.
        /// </summary>
        public string LastMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status of the thread run.
        /// </summary>
        public string? Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of conversation messages that occurred during the thread run.
        /// </summary>
        public List<ThreadConversationMessage> ConversationMessages { get; set; } = new();

        /// <summary>
        /// Generates a dialog string from the conversation messages.
        /// </summary>
        /// <returns>A formatted dialog string representing the conversation.</returns>
        public string Dialog
        {
            get
            {
                var dialog = string.Empty;
                var lastMessageType = string.Empty;
                foreach (var message in ConversationMessages)
                {
                    dialog += (lastMessageType == message.MessageType.ToString() ? "\n" : $"\n{message.MessageType.ToString()}: ") + message.Message + "\n";
                    lastMessageType = message.MessageType.ToString();
                }
                return dialog;
            }
        }

        /// <summary>
        /// Gets or sets the ID of the thread associated with the assistant.
        /// </summary>
        public string ThreadId { get; set; } = string.Empty;

        /// <summary>
        /// File search and code interpreter annotations
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<MessageAnnotation>? Annotations { get; set; }

        /// <summary>
        /// Gets or sets the usage of the thread run.
        /// </summary>
        public UsageResponse? Usage { get; set; }
    }

    /// <summary>
    /// Enum representing the type of conversation message.
    /// </summary>
    public enum ThreadConversationMessageType
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        User,
        Assistant
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }

    /// <summary>
    /// Represents a message in the thread conversation.
    /// This record holds the message type and content.
    /// </summary>
    public record ThreadConversationMessage
    {
        /// <summary>
        /// Gets or sets the type of the message.
        /// </summary>
        public ThreadConversationMessageType MessageType { get; set; }

        /// <summary>
        /// Gets or sets the content of the message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}