using AntRunner.ToolCalling.AssistantDefinitions;
using OpenAI;
using OpenAI.Chat;

namespace AntRunner.Chat
{
    /// <summary>
    /// Represents a conversation with an AI assistant, managing the interaction and message history.
    /// </summary>
    public class Conversation
    {
        private ChatRunOptions _chatConfiguration;
        private AzureOpenAiConfig _serviceConfiguration;

        /// <summary>
        /// Gets or sets the messages exchanged with the assistant.
        /// </summary>
        public Dictionary<string, List<Message>> AssistantMessages { get; set; } = [];

        /// <summary>
        /// Gets or sets the list of turns in the conversation.
        /// </summary>
        public List<Turn> Turns { get; set; } = [];

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
        public AssistantDefinition AssistantDefinition { get; set; }

        /// <summary>
        /// Gets the usage statistics of the conversation.
        /// </summary>
        public UsageResponse Usage
        {
            get
            {
                UsageResponse totalUsage = new() { CachedPromptTokens = 0, CompletionTokens = 0, PromptTokens = 0, TotalTokens = 0 };
                foreach (var turn in Turns)
                {
                    if (turn.ChatRunOutput != null)
                    {
                        totalUsage.PromptTokens += turn.ChatRunOutput.Usage?.PromptTokens ?? 0;
                        totalUsage.CompletionTokens += turn.ChatRunOutput.Usage?.CompletionTokens ?? 0;
                        totalUsage.CachedPromptTokens += turn.ChatRunOutput.Usage?.CachedPromptTokens ?? 0;
                        totalUsage.TotalTokens += turn.ChatRunOutput.Usage?.TotalTokens ?? 0;
                    }
                }
                return totalUsage;
            }
        }

        /// <summary>
        /// Gets the last response from the assistant.
        /// </summary>
        public ChatRunOutput? LastResponse { get { return Turns.LastOrDefault()?.ChatRunOutput; } }

        /// <summary>
        /// Changes the assistant being used in the conversation to the specified assistant name.
        /// </summary>
        /// <param name="assistantName">The name of the new assistant to use.</param>
        public async Task ChangeAssistant(string assistantName)
        {
            if (assistantName == AssistantDefinition.Name) return;
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(assistantName) ?? throw new Exception($"Can't find assistant definition for {assistantName}");

            var newAssistantMessages = AssistantMessages[AssistantDefinition.Name!].Where(m => m.Role == Role.User || m.Role == Role.Assistant && m.ToolCalls == null).ToList();

            newAssistantMessages.Add(new Message(Role.System, "The previous messages are a conversation between the user and a different assistant. Use them to understand the context of the conversation. New instructions follow."));
            newAssistantMessages.Add(new Message(Role.System, assistantDef.Instructions));
            AssistantDefinition = assistantDef;
            AssistantMessages[assistantName] = newAssistantMessages;

            _chatConfiguration.AssistantName = assistantName;
            _chatConfiguration.DeploymentId = assistantDef.Model;
        }

        /// <summary>
        /// Initiates a chat with the assistant using the specified instructions.
        /// </summary>
        /// <param name="instructions">The instructions for the chat session.</param>
        /// <returns>A task representing the output of the chat run.</returns>
        public async Task<ChatRunOutput> Chat(string instructions)
        {
            _chatConfiguration.Instructions = instructions;

            Turn turn = new() { AssistantName = _chatConfiguration.AssistantName, Instructions = instructions };
            Turns.Add(turn);

            var runnerOutput = await ChatRunner.RunThread(_chatConfiguration, _serviceConfiguration, AssistantMessages[AssistantDefinition.Name!]);

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
        /// Undoes the last user message in the conversation.
        /// </summary>
        public void Undo()
        {
            var lastIndex = AssistantMessages[AssistantDefinition.Name!].FindLastIndex(m => m.Role == Role.User);
            if (lastIndex != -1)
            {
                if (Turns.Count > 0)
                {
                    Turns.RemoveAt(Turns.Count - 1);
                }
                else
                {
                    TraceWarning("Something is wrong. There are no turns to undo.");
                }
                AssistantMessages[AssistantDefinition.Name!].RemoveRange(lastIndex, AssistantMessages[AssistantDefinition.Name!].Count - lastIndex);
            }
        }

        /// <summary>
        /// Undoes the last user message in the conversation that matches the specified text.
        /// </summary>
        /// <param name="messageText">The text of the user message to undo.</param>
        public void Undo(string messageText)
        {
            var lastIndex = AssistantMessages[AssistantDefinition.Name!].FindLastIndex(m => m.Role == Role.User && m.Content == (dynamic)messageText);
            if (lastIndex != -1)
            {
                AssistantMessages[AssistantDefinition.Name!].RemoveRange(lastIndex, AssistantMessages[AssistantDefinition.Name!].Count - lastIndex);
            }
            var lastTurn = Turns.LastOrDefault(t => t.Instructions == messageText);
            if (lastTurn != null) Turns.Remove(lastTurn);
        }

        /// <summary>
        /// Creates a new conversation asynchronously using the specified chat configuration.
        /// </summary>
        /// <param name="chatConfiguration">The configuration options for the chat.</param>
        /// <returns>A task representing the newly created conversation.</returns>
        public static async Task<Conversation> Create(ChatRunOptions chatConfiguration)
        {
            var serviceConfiguration = AzureOpenAiConfigFactory.Get();
            return await Create(chatConfiguration, serviceConfiguration);
        }

        /// <summary>
        /// Creates a new conversation asynchronously using the specified chat and service configurations.
        /// </summary>
        /// <param name="chatConfiguration">The configuration options for the chat.</param>
        /// <param name="serviceConfiguration">The configuration options for the service.</param>
        /// <returns>A task representing the newly created conversation.</returns>
        public static async Task<Conversation> Create(ChatRunOptions chatConfiguration, AzureOpenAiConfig serviceConfiguration)
        {
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(chatConfiguration.AssistantName) ?? throw new Exception($"Can't find assistant definition for {chatConfiguration.AssistantName}");
            var conversation = new Conversation(chatConfiguration, serviceConfiguration, assistantDef);
            return conversation;
        }

        /// <summary>
        /// Overloaded method to create a conversation from a JSON file using the specified chat and service configurations.
        /// </summary>
        /// <param name="filePath">The path to the file from which the instance will be loaded.</param>
        /// <param name="chatConfiguration">The configuration options for the chat.</param>
        /// <param name="serviceConfiguration">The configuration options for the service.</param>
        /// <returns>A task representing the loaded conversation.</returns>
        public static Conversation Create(string filePath, AzureOpenAiConfig serviceConfiguration)
        {
            var jsonString = File.ReadAllText(filePath);
            var conversation = JsonSerializer.Deserialize<Conversation>(jsonString) ?? throw new Exception("Failed to deserialize the conversation.");

            // Apply the provided configurations to the deserialized conversation
            conversation._chatConfiguration = new() { AssistantName = conversation.AssistantDefinition.Name!, DeploymentId = conversation.AssistantDefinition.Model };
            conversation._serviceConfiguration = serviceConfiguration;

            return conversation;
        }

        /// <summary>
        /// Serializes the current instance to a JSON file.
        /// </summary>
        /// <param name="filePath">The path to the file where the instance will be saved.</param>
        public void Save(string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, jsonString);
        }
    }
}