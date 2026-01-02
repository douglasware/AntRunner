using AntRunner.ToolCalling.AssistantDefinitions;
using OpenAI;
using OpenAI.Chat;

namespace AntRunner.Chat
{
    public delegate void MessageAddedEventHandler(object? sender, MessageAddedEventArgs e);

    public class MessageAddedEventArgs : EventArgs
    {
        public string Message { get; }
        public string Role { get; }
        public string? ToolCallId { get; }
        public string? FunctionName { get; }
        public string? ToolCallsJson { get; }

        public MessageAddedEventArgs(string role, string newMessage, string? toolCallId = null, string? functionName = null, string? toolCallsJson = null)
        {
            Message = newMessage;
            Role = role;
            ToolCallId = toolCallId;
            FunctionName = functionName;
            ToolCallsJson = toolCallsJson;
        }
    }

    public delegate void StreamingMessageProgressEventHandler(object? sender, StreamingMessageProgressEventArgs e);

    public delegate void ExternalToolCallEventHandler(object? sender, ExternalToolCallEventArgs e);

    public class ExternalToolCallEventArgs : EventArgs
    {
        public string ToolCallsJson { get; }
        public ExternalToolCallEventArgs(string toolCallsJson)
        {
            ToolCallsJson = toolCallsJson;
        }
    }

    public class StreamingMessageProgressEventArgs : EventArgs
    {
        public string ContentDelta { get; }
        public string Role { get; }

        public StreamingMessageProgressEventArgs(string role, string contentDelta)
        {
            ContentDelta = contentDelta;
            Role = role;
        }
    }

    /// <summary>
    /// Represents a conversation with an AI assistant, managing the interaction and message history.
    /// </summary>
    public class Conversation
    {
        private ChatRunOptions? _chatConfiguration;
        private AzureOpenAiConfig? _serviceConfiguration;
        private HttpClient _httpClient = HttpClientUtility.Get();

        /// <summary>
        /// Gets or sets the messages exchanged with the assistant.
        /// </summary>
        public Dictionary<string, List<Message>> AssistantMessages { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of turns in the conversation.
        /// </summary>
        public List<Turn> Turns { get; set; } = [];

        /// <summary>
        /// Event stream as messages are added to the conversation
        /// </summary>
        public event MessageAddedEventHandler? MessageAdded;

        /// <summary>
        /// Initializes a new instance of the <see cref="Conversation"/> class.
        /// </summary>
        public Conversation()
        {
            // Parameterless constructor for deserialization
        }

        private Conversation(ChatRunOptions chatConfiguration, AzureOpenAiConfig serviceConfiguration, AssistantDefinition assistantDef)
        {
            _chatConfiguration = chatConfiguration;
            _serviceConfiguration = serviceConfiguration;
            AssistantDefinition = assistantDef;
            AssistantMessages[AssistantDefinition.Name!] = [];
        }

        /// <summary>
        /// Gets the definition of the assistant being used in the conversation.
        /// </summary>
        public AssistantDefinition? AssistantDefinition { get; set; }

        /// <summary>
        /// Changes the assistant being used in the conversation to the specified assistant name.
        /// </summary>
        /// <param name="assistantName">The name of the new assistant to use.</param>
        /// <param name="useAssistantDefinitionModel">If true, will set the conversation to use the assistant definitions model, overiding whatever was set when the conversation was created</param>
        public async Task ChangeAssistant(string assistantName, bool useAssistantDefinitionModel = false)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_chatConfiguration == null) throw new Exception("_chatConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");

            if (assistantName == AssistantDefinition.Name) return;
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(assistantName) ?? throw new Exception($"Can't find assistant definition for {assistantName}");

            var existingMessages = AssistantMessages[AssistantDefinition.Name!].Where(m => m.Role == Role.User || m.Role == Role.Assistant && m.ToolCalls == null).ToList();

            // Create new message list with system prompts first, then history, then handoff message
            var newAssistantMessages = new List<Message>();
            
            // Add system prompts at the beginning
            newAssistantMessages.Add(new Message(Role.System, assistantDef.Instructions));
            
            // Add filtered existing messages
            newAssistantMessages.AddRange(existingMessages);
            
            // Add handoff message AFTER the history (so "above" makes sense)
            newAssistantMessages.Add(new Message(Role.System, "The previous messages between the user and assistant above are from a conversation with a different assistant. Use them to understand the conversation context, but follow the system messages that were provided at the start of this message sequence."));

            AssistantDefinition = assistantDef;
            AssistantMessages[assistantName] = newAssistantMessages;

            _chatConfiguration.AssistantName = assistantName;
            if (useAssistantDefinitionModel)
            {
                _chatConfiguration.DeploymentId = assistantDef.Model;
            }
        }

        /// <summary>
        /// Initiates a chat with the assistant using the specified instructions.
        /// </summary>
        /// <param name="instructions">The instructions for the chat session.</param>
        /// <returns>A task representing the output of the chat run.</returns>
        public async Task<ChatRunOutput> Chat(string instructions)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_chatConfiguration == null) throw new Exception("_chatConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_serviceConfiguration == null) throw new Exception("_serviceConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");

            _chatConfiguration.Instructions = instructions;

            Turn turn = new() { AssistantName = _chatConfiguration.AssistantName, Instructions = instructions };
            Turns.Add(turn);

            var oldMessages = AssistantMessages[AssistantDefinition.Name!];

            var runnerOutput = await ChatRunner.RunThread(_chatConfiguration, _serviceConfiguration, oldMessages, _httpClient, MessageAdded);

            if (runnerOutput != null && runnerOutput.Messages != null)
            {
                AssistantMessages[AssistantDefinition.Name!] = runnerOutput.Messages;
                turn.ChatRunOutput = runnerOutput;
                return runnerOutput;
            }
            else if (runnerOutput != null)
            {
                runnerOutput.Messages = [(new Message(Role.System, "Unknown error"))];
                return runnerOutput;
            }
            return new() { Messages = [(new Message(Role.System, "Unknown error"))] };
        }

        /// <summary>
        /// Creates a new conversation asynchronously using the specified chat configuration.
        /// </summary>
        /// <param name="chatConfiguration">The configuration options for the chat.</param>
        /// <returns>A task representing the newly created conversation.</returns>
        public static async Task<Conversation> Create(ChatRunOptions chatConfiguration, HttpClient? httpClient = null)
        {
            var serviceConfiguration = AzureOpenAiConfigFactory.Get();
            return await Create(chatConfiguration, serviceConfiguration, httpClient);
        }

        /// <summary>
        /// Creates a new conversation asynchronously using the specified chat and service configurations.
        /// </summary>
        /// <param name="chatConfiguration">The configuration options for the chat.</param>
        /// <param name="serviceConfiguration">The configuration options for the service.</param>
        /// <returns>A task representing the newly created conversation.</returns>
        public static async Task<Conversation> Create(ChatRunOptions chatConfiguration, AzureOpenAiConfig serviceConfiguration, HttpClient? httpClient = null)
        {
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(chatConfiguration.AssistantName) ?? throw new Exception($"Can't find assistant definition for {chatConfiguration.AssistantName}");
            var conversation = new Conversation(chatConfiguration, serviceConfiguration, assistantDef);
            conversation._httpClient = httpClient ?? conversation._httpClient;
            return conversation;
        }
    }
}
