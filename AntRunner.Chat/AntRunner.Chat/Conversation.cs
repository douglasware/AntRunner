
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

        public MessageAddedEventArgs(string role, string newMessage)
        {
            Message = newMessage;
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
        /// The deployment Id of the model, e.g. 03-mini
        /// </summary>
        public string? Model { get { return _chatConfiguration?.DeploymentId; } set { if (_chatConfiguration != null) { _chatConfiguration.DeploymentId = value; } } }

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
        /// <param name="useAssistantDefinitionModel">If true, will set the conversation to use the assistant definitions model, overiding whatever was set when the conversation was created</param>
        public async Task ChangeAssistant(string assistantName, bool useAssistantDefinitionModel = false)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_chatConfiguration == null) throw new Exception("_chatConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");

            if (assistantName == AssistantDefinition.Name) return;
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(assistantName) ?? throw new Exception($"Can't find assistant definition for {assistantName}");

            var newAssistantMessages = AssistantMessages[AssistantDefinition.Name!].Where(m => m.Role == Role.User || m.Role == Role.Assistant && m.ToolCalls == null).ToList();

            newAssistantMessages.Add(new Message(Role.System, "The previous messages are a conversation between the user and a different assistant. Use them to understand the context of the conversation. New instructions follow."));
            newAssistantMessages.Add(new Message(Role.System, assistantDef.Instructions));
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
        /// Undoes the last user message in the conversation.
        /// </summary>
        public void Undo()
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_chatConfiguration == null) throw new Exception("_chatConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_serviceConfiguration == null) throw new Exception("_serviceConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");

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
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_chatConfiguration == null) throw new Exception("_chatConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");
            if (_serviceConfiguration == null) throw new Exception("_serviceConfiguration is null. Use the default public constructor exists to allow serialization, but you should nt use it directly.");

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

        /// <summary>
        /// Overloaded method to create a conversation from a JSON file using the specified chat and service configurations.
        /// </summary>
        /// <param name="filePath">The path to the file from which the instance will be loaded.</param>
        /// <param name="chatConfiguration">The configuration options for the chat.</param>
        /// <param name="serviceConfiguration">The configuration options for the service.</param>
        /// <returns>A task representing the loaded conversation.</returns>
        public static Conversation Create(string filePath, AzureOpenAiConfig serviceConfiguration, HttpClient? httpClient = null)
        {
            var jsonString = File.ReadAllText(filePath);
            var conversation = JsonSerializer.Deserialize<Conversation>(jsonString) ?? throw new Exception("Failed to deserialize the conversation.");

            conversation._chatConfiguration = new ChatRunOptions { AssistantName = conversation.AssistantDefinition!.Name!, DeploymentId = conversation.AssistantDefinition.Model };
            conversation._chatConfiguration = new() { AssistantName = conversation.AssistantDefinition!.Name!, DeploymentId = conversation.AssistantDefinition.Model };
            conversation._serviceConfiguration = serviceConfiguration;
            conversation._httpClient = httpClient ?? conversation._httpClient;
            return conversation;
        }

        static readonly JsonSerializerOptions WriteIndentedOptions = new() { WriteIndented = true };

        /// <summary>
        /// Serializes the current instance to a JSON file.
        /// </summary>
        /// <param name="filePath">The path to the file where the instance will be saved.</param>
        public void Save(string filePath)
        {
            var jsonString = JsonSerializer.Serialize(this, WriteIndentedOptions);
            File.WriteAllText(filePath, jsonString);
        }

        /// <summary>
        /// Asynchronously adds an image file message to the assistant's messages.
        /// This method validates the file extension to ensure it is a supported image format (.jpg, .jpeg, .png, .gif, .bmp, .tiff).
        /// It reads the image file asynchronously, converts its content to a base64 string,
        /// and constructs a data URL that is added to the assistant's messages.
        /// </summary>
        /// <param name="filePath">The path to the image file to be added.</param>
        /// <exception cref="Exception">Thrown when AssistantDefinition is null, indicating a missing assistant configuration.</exception>
        /// <exception cref="ArgumentException">Thrown when the file extension is not recognized as a valid image type.</exception>
        public async Task AddImageFileMessage(string filePath)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null adding message");

            // Validate imageUrl is an image file
            var validImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff" };
            if (!validImageExtensions.Contains(Path.GetExtension(filePath).ToLower()))
            {
                throw new ArgumentException("File is not a valid image type");
            }

            // Read it asynchronously and create a base64 image url
            byte[] imageBytes;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                imageBytes = new byte[fileStream.Length];
                await fileStream.ReadAsync(imageBytes, 0, (int)fileStream.Length);
            }

            string base64Image = Convert.ToBase64String(imageBytes);
            string url = $"data:image/{Path.GetExtension(filePath).TrimStart('.').ToLower()};base64,{base64Image}";

            var imageContent = new Content(new ImageUrl(url));
            AssistantMessages[AssistantDefinition.Name!].Add(new Message(Role.User, new List<Content> { imageContent }));
        }

        /// <summary>
        /// Adds an image url message to the assistant's messages. The Url must be publicly accessible
        /// </summary>
        /// <param name="imageUrl">The public url to the image file to be added.</param>
        /// <exception cref="Exception">Thrown when AssistantDefinition is null, indicating a missing assistant configuration.</exception>
        /// <exception cref="ArgumentException">Thrown when the file extension is not recognized as a valid image type.</exception>
        public void AddImageUrlMessage(string imageUrl)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null adding message");
            AssistantMessages[AssistantDefinition.Name!].Add(new Message(Role.User, new List<Content> { new Content(new ImageUrl(imageUrl)) }));
        }

        /// <summary>
        /// Asynchronously adds an audio file message to the assistant's messages.
        /// This method validates the file extension to ensure it is a supported audio format (.wav, .mp3).
        /// It determines the audio format based on the file extension and reads the audio file asynchronously.
        /// The file's content is converted to a base64 string, and a data URL is constructed to be added to the assistant's messages.
        /// Note: This method does not work. The current API implementation requires the audio file to be accessible via a public URL.
        /// </summary>
        /// <param name="filePath">The path to the audio file to be added.</param>
        /// <exception cref="Exception">Thrown when AssistantDefinition is null, indicating a missing assistant configuration.</exception>
        /// <exception cref="ArgumentException">Thrown when the file extension is not recognized as a valid audio type.</exception>
        public async Task AddAudioFileMessage(string filePath)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null adding message");

            // Validate imageUrl is an audio file
            var validAudioExtensions = new[] { ".wav", ".mp3" };
            var fileExtension = Path.GetExtension(filePath).ToLower();

            if (!validAudioExtensions.Contains(fileExtension))
            {
                throw new ArgumentException("File is not a valid audio type");
            }

            // Determine the format based on the file extension
            InputAudioFormat format = fileExtension == ".wav" ? InputAudioFormat.Wav : InputAudioFormat.Mp3;

            // Read it asynchronously and create a base64 audio url
            byte[] audioBytes;
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                audioBytes = new byte[fileStream.Length];
                await fileStream.ReadAsync(audioBytes, 0, (int)fileStream.Length);
            }

            string base64Audio = Convert.ToBase64String(audioBytes);
            string url = $"data:audio/{format.ToString().ToLower()};base64,{base64Audio}";

            var audioContent = new Content(new InputAudio(url, format));
            AssistantMessages[AssistantDefinition.Name!].Add(new Message(Role.User, new List<Content> { audioContent }));
        }

        /// <summary>
        /// Asynchronously adds a text file message to the assistant's messages.
        /// This method reads the entire content of the specified text file asynchronously.
        /// It formats the content with a header that includes the file name and its contents.
        /// The formatted message is then added to the assistant's messages.
        /// </summary>
        /// <param name="filePath">The path to the text file to be added.</param>
        /// <exception cref="Exception">Thrown when AssistantDefinition is null, indicating a missing assistant configuration.</exception>
        public async Task AddTextFileMessage(string filePath)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null adding message");

            // Read the file asynchronously
            string fileContent;
            using (var reader = new StreamReader(filePath))
            {
                fileContent = await reader.ReadToEndAsync();
            }

            // Format the message
            string fileName = Path.GetFileName(filePath);
            string messageContent = $"The {fileName} file contains:\n\n---\n{fileContent}";

            var textContent = new Content(messageContent);
            AssistantMessages[AssistantDefinition.Name!].Add(new Message(Role.User, new List<Content> { textContent }));
        }

        /// <summary>
        /// Adds a sandbox file message to the assistant's messages using environment variable paths.
        /// This method checks the existence of the file and validates its location within the specified local volume mount path.
        /// It constructs a container path using the local and container volume paths,
        /// and adds a message instructing the assistant to use the contents of the specified file.
        /// </summary>
        /// <param name="filePath">The path to the sandbox file to be added.</param>
        /// <exception cref="Exception">Thrown when AssistantDefinition is null, indicating a missing assistant configuration.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the localVolumeMountPath or containerVolumePath is null,
        /// when the specified file does not exist,
        /// or when the file is not located within the specified local path.
        /// </exception>
        public void AddSandboxFileMessage(string filePath)
        {
            AddSandboxFileMessage(filePath, Environment.GetEnvironmentVariable("CHATRUNNER_SANDBOX_LOCAL_PATH"), Environment.GetEnvironmentVariable("CHATRUNNER_SANDBOX_CONTAINER_PATH"));
        }

        /// <summary>
        /// Adds a sandbox file message to the assistant's messages using specified paths.
        /// This method checks the existence of the file and validates its location within the specified local volume mount path.
        /// It constructs a container path using the provided local and container volume paths,
        /// and adds a message instructing the assistant to use the contents of the specified file.
        /// </summary>
        /// <param name="filePath">The path to the sandbox file to be added.</param>
        /// <param name="localVolumeMountPath">The local path where the file is mounted.</param>
        /// <param name="containerVolumePath">The container path where the file is to be accessed.</param>
        /// <exception cref="Exception">Thrown when AssistantDefinition is null, indicating a missing assistant configuration.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when the localVolumeMountPath or containerVolumePath is null,
        /// when the specified file does not exist,
        /// or when the file is not located within the specified local path.
        /// </exception>
        public void AddSandboxFileMessage(string filePath, string? localVolumeMountPath, string? containerVolumePath)
        {
            if (AssistantDefinition == null) throw new Exception("AssistantDefinition is null adding message");

            if (localVolumeMountPath == null) throw new ArgumentException("localVolumeMountPath is null. Provide it or set the CHATRUNNER_SANDBOX_LOCAL_PATH env variable");
            if (containerVolumePath == null) throw new ArgumentException("containerVolumePath is null. Provide it or set the CHATRUNNER_SANDBOX_CONTAINER_PATH env variable");
            if (!File.Exists(filePath)) throw new ArgumentException($"{filePath} does not exist");
            var fullLocalPath = Path.GetFullPath(localVolumeMountPath);
            var fullFilePath = Path.GetFullPath(filePath);
            if (!fullFilePath.StartsWith(fullLocalPath)) throw new ArgumentException($"{fullFilePath} is not located in {fullLocalPath}");
            var containerPath = fullFilePath.Replace(fullLocalPath, containerVolumePath);
            AssistantMessages[AssistantDefinition.Name!].Add(new Message(Role.User, new List<Content> { new Content($"Use the contents of {containerPath}") }));
        }
    }
}
