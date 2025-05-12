
using AntRunner.ToolCalling.AssistantDefinitions.Storage;
using AntRunner.ToolCalling.AssistantDefinitions;
using AntRunner.ToolCalling.Functions;
using OpenAI;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json.Nodes;

namespace AntRunner.Chat
{
    public class ChatConversationException : Exception
    {
        public ChatRunOutput? ChatRunOutput { get; private set; }

        public ChatConversationException(Exception ex, ChatRunOutput? chatRunOutput) : base("An error occured during the run", ex)
        {
            ChatRunOutput = chatRunOutput;
        }
    }

    /// <summary>
    /// Responsible for running assistant threads through interaction with various utilities.
    /// </summary>
    public class ChatRunner
    {
        static readonly HttpClient _httpClient = HttpClientUtility.Get();

        private static readonly ConcurrentDictionary<string, Dictionary<string, ToolCaller>> RequestBuilderCache = new();

        /// <summary>
        /// Executes a chat conversation thread with an AI assistant using Azure OpenAI.
        /// </summary>
        /// <param name="chatRunOptions">Options and settings for running the chat, including assistant name and instructions.</param>
        /// <param name="config">Configuration object for Azure OpenAI, containing API keys and necessary settings.</param>
        /// <param name="previousMessages">Optional list of messages from a previous conversation for context continuity.</param>
        /// <param name="httpClient">Optional HttpClient instance for making HTTP requests, with a default fallback if not provided.</param>
        /// <returns>
        /// Returns a <see cref="ChatRunOutput"/> containing the results of the chat run, including the final state of the conversation.
        /// </returns>
        /// <exception cref="Exception">Thrown when the assistant definition cannot be found.</exception>
        /// <exception cref="ChatConversationException">Thrown when an error occurs during the chat conversation, encapsulating the current run results.</exception>
        public static async Task<ChatRunOutput?> RunThread(ChatRunOptions chatRunOptions, AzureOpenAiConfig config, List<Message>? previousMessages = null, HttpClient? httpClient = null, MessageAddedEventHandler? messageAdded = null)
        {
            // Retrieve the assistant ID using the assistant name from the configuration
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(chatRunOptions.AssistantName);
            if (assistantDef == null)
            {
                throw new Exception($"Can't find {assistantDef}");
            }

            // 256000 is the maximum instruction length allowed by the API
            if (chatRunOptions.Instructions.Length >= 256000)
            {
                TraceWarning("Instructions are too long, truncating");
                chatRunOptions.Instructions = chatRunOptions.Instructions[..255999];
            }

            var auth = new OpenAIAuthentication(config.ApiKey);
            var settings = new OpenAIClientSettings(resourceName: config.ResourceName, chatRunOptions.DeploymentId, apiVersion: config.ApiVersion);
            using var api = new OpenAIClient(auth, settings, httpClient ?? _httpClient);

            var messages = new List<Message>();

            if (previousMessages != null && previousMessages.Count > 0)
            {
                foreach (var previousMessage in previousMessages)
                {
                    messages.Add(previousMessage);
                }
                messages.Add(new Message(Role.User, chatRunOptions.Instructions));
                messageAdded?.Invoke(null, new MessageAddedEventArgs(messages.Last().GetText()));
            }
            else
            {
                messages =
                [
                    new Message(Role.System, assistantDef.Instructions),
                    new Message(Role.User, chatRunOptions.Instructions),
                ];
                messageAdded?.Invoke(null, new MessageAddedEventArgs(messages.First().GetText()));
                messageAdded?.Invoke(null, new MessageAddedEventArgs(messages.Last().GetText()));
            }

            var tools = new List<Tool>();

            if (assistantDef.Tools != null)
            {
                foreach (var toolDef in assistantDef.Tools.Where(t => t.Type == "function"))
                {
                    if (toolDef.Function?.AsObject != null)
                    {
                        var function = toolDef.Function.AsObject;
                        var functionParametersJsonNode = JsonNode.Parse(JsonSerializer.Serialize(function.Parameters));

                        var newFunction = new OpenAI.Function(function.Name, function.Description, functionParametersJsonNode, null);
                        var newTool = new Tool(newFunction);
                        tools.Add(newTool);
                    }
                }
            }

            bool continueChat = true;

            Choice? choice = null;
            ChatRunOutput? runResults = null;
            int evaluatorTurnCounter = 0;

            try
            {
                while (continueChat)
                {
                    var chatRequest = new ChatRequest(messages, tools: tools, model: chatRunOptions.DeploymentId, temperature: assistantDef.Temperature, topP: assistantDef.TopP);
                    var response = await api.ChatEndpoint.GetCompletionAsync(chatRequest);
                    messages.Add(response.FirstChoice.Message);
                    messageAdded?.Invoke(null, new MessageAddedEventArgs(messages.Last().GetText()));
                    choice = response.FirstChoice;

                    switch (choice.FinishReason)
                    {
                        case "stop":
                            evaluatorTurnCounter = 0;
                            continueChat = false;
                            break;
                        case "tool_calls":
                            await DoToolCalls(assistantDef, choice.Message.ToolCalls, messages, oAuthUserAccessToken: chatRunOptions.oAuthUserAccessToken, httpClient: httpClient);
                            break;
                        case "length":
                            continueChat = false;
                            break;
                        case "function_call":
                            continueChat = false;
                            break;
                        default:
                            break;
                    }

                    runResults = BuildRunResults(messages, response);

                    if (choice.FinishReason == "stop" && !string.IsNullOrEmpty(chatRunOptions.Evaluator))
                    {
                        while (evaluatorTurnCounter < 2)
                        {
                            evaluatorTurnCounter++;
                            var evaluatorOptions = new ChatRunOptions()
                            {
                                AssistantName = chatRunOptions.Evaluator,
                                Instructions = runResults?.Dialog ?? ""
                            };

                            var evaluatorOutput = (await RunThread(evaluatorOptions, config))?.LastMessage ?? "";
                            if (!evaluatorOutput.Contains("End Conversation", StringComparison.OrdinalIgnoreCase))
                            {
                                messages.Add(new Message(Role.User, evaluatorOutput));
                                continueChat = true;
                            }
                        }
                    }
                }

                return runResults;
            }
            catch(Exception ex)
            {
                throw new ChatConversationException(ex, runResults);
            }
        }

        private static ChatRunOutput? BuildRunResults(List<Message> messages, ChatResponse response)
        {
            ChatRunOutput? runResults = new() { Messages = messages };

            if (messages.Last().Content is JsonElement && messages.Last().Content.ValueKind == JsonValueKind.String)
            {
                runResults.LastMessage = messages.Last().Content.ToString();
            }
            else
            {
                runResults.LastMessage = messages.Last().Content[0].Text;
            }

            var choice = response.FirstChoice;
            runResults.Status = choice?.FinishReason ?? "unknown";

            foreach (var message in messages)
            {
                if (message.Role == Role.System || message.Role == Role.Developer) continue;

                string messageText = message.GetText();

                if (message.Role == Role.User)
                {
                    runResults.ConversationMessages.Add(new() { Message = messageText, MessageType = ThreadConversationMessageType.User });
                }
                else if (message.Role == Role.Assistant)
                {
                    runResults.ConversationMessages.Add(new() { Message = messageText, MessageType = ThreadConversationMessageType.Assistant });
                }
                else if (message.Role == Role.Tool)
                {
                    runResults.ConversationMessages.Add(new() { Message = messageText, MessageType = ThreadConversationMessageType.Tool });
                }
            }

            runResults.Usage = new()
            {
                CompletionTokens = response.Usage.CompletionTokens,
                PromptTokens = response.Usage.PromptTokens ?? 0,
                CachedPromptTokens = response.Usage.PromptTokensDetails.CachedTokens,
                TotalTokens = response.Usage.TotalTokens ?? 0
            };

            return runResults;
        }

        /// <summary>
        /// Executes a chat conversation thread with an AI assistant using a simplified approach.
        /// </summary>
        /// <param name="assistantName">The name of the assistant to run.</param>
        /// <param name="instructions">The instructions for the conversation with the assistant.</param>
        /// <param name="evaluator">Optional evaluator name for further processing or evaluation.</param>
        /// <param name="httpClient">Optional HttpClient instance for making HTTP requests, with a default fallback if not provided.</param>
        /// <returns>
        /// Returns a <see cref="string"/> containing the dialog from the chat run.
        /// If unable to process the request, returns "Unable to process request".
        /// </returns>
        /// <remarks>
        /// This method is designed to provide a simplified interface for running an assistant thread,
        /// allowing the use of a thread run as a tool call via local functions.
        /// </remarks>
        /// <exception cref="ChatConversationException">Thrown when an error occurs during the chat conversation, encapsulating the current run results.</exception>
        public static async Task<string> RunThread(string assistantName, string instructions, string? evaluator = "", HttpClient? httpClient = null)
        {
            TraceInformation($"Running {assistantName}"); //: {instructions}");
            var config = AzureOpenAiConfigFactory.Get();
            var chatRunOptions = new ChatRunOptions()
            {
                AssistantName = assistantName,
                Instructions = instructions,
                Evaluator = evaluator,
                DeploymentId = config.DeploymentId
            };

            var output = await RunThread(chatRunOptions, config!, httpClient: httpClient);
            if (output != null)
            {
                return output.Dialog;
            }

            return "Unable to process request";
        }
        
        private static async Task DoToolCalls(AssistantDefinition assistantDef, IReadOnlyList<OpenAI.ToolCall> toolCalls, List<Message> messages, string? oAuthUserAccessToken = null, HttpClient? httpClient = null)
        {
            var assistantName = assistantDef.Name!;

            await EnsureRequestBuilderCache(assistantName);

            if (!RequestBuilderCache.TryGetValue(assistantName, out var builders)) throw new Exception($"No request builders found for {assistantName}");

            var toolCallTasks = new List<Task<ToolOutput>>();

            foreach (var requiredOutput in toolCalls)
            {
                if (!requiredOutput.IsFunction) continue;
                var toolCallId = requiredOutput.Id;
                var toolName = requiredOutput.Function.Name;
                var parameters = requiredOutput.Function.Arguments;
                if (builders.TryGetValue(toolName, out ToolCaller? tool))
                {
                    var builder = tool.Clone();
                    
                    builder.Params = JsonSerializer.Deserialize<Dictionary<string, object>>(parameters.ToString());

                    var task = Task.Run(async () =>
                    {
                        string output;
                        if (builder.ActionType == ActionType.WebApi)
                        {
                            var response = await builder.ExecuteWebApiAsync(oAuthUserAccessToken, httpClient ?? _httpClient);
                            var responseContent = await response.Content.ReadAsStringAsync();

                            if (builder.ResponseSchemas.TryGetValue("200", out var schemaJson))
                            {
                                try
                                {
                                    var contentJson = JsonDocument.Parse(responseContent).RootElement;

                                    var filteredJson = ChatRunnerUtils.FilterJsonBySchema(contentJson, schemaJson);
                                    output = filteredJson.GetRawText();
                                }
                                catch
                                {
                                    output = responseContent;
                                }
                            }
                            else
                            {
                                output = responseContent;
                            }
                        }
                        else
                        {
                            try
                            {
                                var toolResult = await builder.ExecuteLocalFunctionAsync();
                                if (toolResult != null)
                                {
                                    output = JsonSerializer.Serialize(toolResult);
                                }
                                else
                                {
                                    output = "Operation completed successfully";
                                }
                            }
                            catch (Exception ex)
                            {
                                output = $"ERROR: {ex.Message}";
                            }
                        }
                        return new ToolOutput()
                        {
                            Output = output,
                            ToolCallId = requiredOutput.Id
                        };
                    });

                    toolCallTasks.Add(task);
                }
                else
                {
                    var task = Task.Run(() =>
                    {
                        Trace.TraceError($"No request builder found for {toolName}");
                        return new ToolOutput()
                        {
                            Output = $"Error: {toolName} is not a valid tool.",
                            ToolCallId = requiredOutput.Id
                        };
                    });
                    toolCallTasks.Add(task);
                }
            }

            if (toolCallTasks.Count > 0)
            {
                var toolOutputs = await Task.WhenAll(toolCallTasks);

                foreach (var toolCall in toolCalls)
                {
                    if (!toolCall.IsFunction) continue;
                    var id = toolCall.Id;
                    var toolOutput = toolOutputs.FirstOrDefault(to => to.ToolCallId == id) ?? throw new Exception("No match");
                    messages.Add(new Message(toolCallId: id, toolFunctionName: toolCall.Function.Name, [new(toolOutput.Output!)]));
                }
            }
        }

        static async Task EnsureRequestBuilderCache(string assistantName)
        {
            if (RequestBuilderCache.ContainsKey(assistantName))
            {
                return;
            }

            var assistantRequestBuilders = new Dictionary<string, ToolCaller>();

            var openApiSchemaFiles = await AssistantDefinitionFiles.GetFilesInOpenApiFolder(assistantName);
            if (openApiSchemaFiles == null || openApiSchemaFiles.Count == 0) return;

            foreach (var openApiSchemaFile in openApiSchemaFiles)
            {
                var schema = await AssistantDefinitionFiles.GetFile(openApiSchemaFile);
                if (schema == null)
                {
                    TraceWarning($"openApiSchemaFile {openApiSchemaFile} is null. Ignoring");
                    continue;
                }

                var json = Encoding.Default.GetString(schema);

                var validationResult = OpenApiHelper.ValidateAndParseOpenApiSpec(json);
                var spec = validationResult.Spec;

                if (!validationResult.Status || spec == null)
                {
                    TraceWarning($"Json is not a valid OpenAPI spec {json}. Ignoring");
                    continue;
                }

                var requestBuilders = await ToolCaller.GetToolCallers(spec, assistantName);

                foreach (var tool in requestBuilders.Keys)
                {
                    assistantRequestBuilders[tool] = requestBuilders[tool];
                }
            }

            RequestBuilderCache[assistantName] = assistantRequestBuilders;
        }
    }
}
