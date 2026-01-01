using AntRunner.ToolCalling;
using OpenAI.Chat;

namespace AntRunner.Chat
{
    public class ChatConversationException(Exception ex, ChatRunOutput? chatRunOutput) : Exception("An error occured during the run", ex)
    {
        public ChatRunOutput? ChatRunOutput { get; private set; } = chatRunOutput;
    }

    /// <summary>
    /// Responsible for running assistant threads through interaction with various utilities.
    /// </summary>
    public class ChatRunner
    {

        /// <summary>
        /// Executes a chat conversation thread with an AI assistant using Azure OpenAI.
        /// </summary>
        /// <param name="chatRunOptions">Options and settings for running the chat, including assistant name and instructions.</param>
        /// <param name="config">Configuration object for Azure OpenAI, containing API keys and necessary settings.</param>
        /// <param name="previousMessages">Optional list of messages from a previous conversation for context continuity.</param>
        /// <param name="httpClient">Optional HttpClient instance for making HTTP requests, with a default fallback if not provided.</param>
        /// <param name="messageAdded">Optional event handler for when a message is added.</param>
        /// <param name="streamingMessageProgress">Optional event handler for streaming message progress.</param>
        /// <param name="projectId">Optional project ID for notebook context injection.</param>
        /// <param name="notebookId">Optional notebook ID for notebook context injection.</param>
        /// <returns>
        /// Returns a <see cref="ChatRunOutput"/> containing the results of the chat run, including the final state of the conversation.
        /// </returns>
        /// <exception cref="Exception">Thrown when the assistant definition cannot be found.</exception>
        /// <exception cref="ChatConversationException">Thrown when an error occurs during the chat conversation, encapsulating the current run results.</exception>
        public static async Task<ChatRunOutput?> RunThread(ChatRunOptions chatRunOptions, AzureOpenAiConfig config, List<Message>? previousMessages = null, HttpClient? httpClient = null, MessageAddedEventHandler? messageAdded = null, StreamingMessageProgressEventHandler? streamingMessageProgress = null, CancellationToken cancellationToken = default)
        {
            // Delegate to the execution engine
            return await ThreadRun.ExecuteAsync(
                chatRunOptions,
                config,
                previousMessages,
                httpClient,
                messageAdded,
                streamingMessageProgress,
                cancellationToken,
                isAgentInvocation: false);
        }
       
    }
}
