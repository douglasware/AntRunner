using AntRunnerLib.AssistantDefinitions;
using AntRunnerLib.Functions;
using OpenAI;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using static OpenAI.ObjectModels.SharedModels.IOpenAiModels;

namespace AntRunnerLib
{
    /// <summary>
    /// Responsible for running assistant threads through interaction with various utilities.
    /// </summary>
    public class ChatRunner
    {
        private static readonly ConcurrentDictionary<string, Dictionary<string, ToolCaller>> RequestBuilderCache = new();

        /// <summary>
        /// Runs the assistant thread with the specified run options and configuration.
        /// It manages the lifecycle of an assistant run, handles required actions, and optionally evaluates conversations.
        /// By default, the assistant will be created if it doesn't exist and a definition is found.
        /// </summary>
        /// <param name="chatRunOptions">The options for running the assistant.</param>
        /// <param name="config">The configuration for Azure OpenAI.</param>
        /// <param name="autoCreate">Whether to automatically create the assistant if it doesn't exist.</param>
        /// <returns>The output of the thread run including possible additional run output from additional messages when using the default evaluator</returns>
        public static async Task<ThreadRunOutput?> RunThread(ChatRunOptions chatRunOptions, AzureOpenAiConfig config)
        {
            // Retrieve the assistant ID using the assistant name from the configuration
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(chatRunOptions.AssistantName);
            if (assistantDef == null)
            {
                throw new ArgumentNullException(nameof(chatRunOptions.AssistantName));
            }

            // 256000 is the maximum instruction length allowed by the API
            if (chatRunOptions.Instructions.Length >= 256000)
            {
                TraceWarning("Instructions are too long, truncating");
                chatRunOptions.Instructions = chatRunOptions.Instructions.Substring(0, 255999);
            }

            var auth = new OpenAIAuthentication(config.ApiKey);
            var settings = new OpenAIClientSettings(resourceName: config.ResourceName, config.DeploymentId, apiVersion: config.ApiVersion);
            using var api = new OpenAIClient(auth, settings);

            var messages = new List<Message>
            {
                new Message(Role.System, assistantDef.Instructions),
                new Message(Role.User, chatRunOptions.Instructions),
            };

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
            ThreadRunOutput? runResults = null;
            int evaluatorTurnCounter = 0;

            while (continueChat)
            {
                var chatRequest = new ChatRequest(messages, tools: tools);
                var response = await api.ChatEndpoint.GetCompletionAsync(chatRequest);
                messages.Add(response.FirstChoice.Message);

                choice = response.FirstChoice;

                Console.WriteLine($"[{choice.Index}] {choice.Message.Role}: {choice.Message} | Finish Reason: {choice.FinishReason}");

                switch (choice.FinishReason)
                {
                    case "stop":
                        evaluatorTurnCounter = 0;
                        continueChat = false;
                        break;
                    case "tool_calls":
                        // Perform required actions for the tool calls
                        await PerformRunRequiredActions(assistantDef, tools, choice.Message.ToolCalls, messages);
                        break;
                    case "length":
                        // Handle the case where the maximum token length is reached
                        continueChat = false;
                        break;
                    case "function_call":
                        // Handle the case where a function call is needed
                        continueChat = false;
                        break;
                    default:
                        messages.Add(choice.Message);
                        break;
                }

                runResults = BuildRunResults(messages, choice);

                if (choice.FinishReason == "stop" && !string.IsNullOrEmpty(chatRunOptions.Evaluator))
                {
                    // Limit the evaluation to up to 3 turns
                    while (evaluatorTurnCounter < 2)
                    {
                        evaluatorTurnCounter++;
                        var evaluatorOptions = new AssistantRunOptions()
                        {
                            AssistantName = chatRunOptions.Evaluator,
                            Instructions = runResults.Dialog
                        };

                        // Run the conversation evaluator
                        var evaluatorOutput = (await AssistantRunner.RunThread(evaluatorOptions, config))?.LastMessage ?? "";
                        if (!evaluatorOutput.Contains("End Conversation", StringComparison.OrdinalIgnoreCase))
                        {
                            messages.Add(new Message(Role.User, evaluatorOutput));

                            continueChat = true;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(chatRunOptions.PostProcessor))
            {
                runResults = await ThreadUtility.RunPostProcessor(chatRunOptions.PostProcessor, runResults);
            }

            return runResults;
        }

        private static ThreadRunOutput BuildRunResults(List<Message> messages, Choice? choice)
        {
            ThreadRunOutput? runResults = new();
            if (messages.Last().Content is JsonElement && messages.Last().Content.ValueKind == JsonValueKind.String)
            {
                runResults.LastMessage = messages.Last().Content.ToString();
            }
            else
            {
                runResults.LastMessage = messages.Last().Content[0].Text;
            }
            runResults.Status = choice?.FinishReason ?? "unknown";
            foreach (var message in messages)
            {
                if (message.Role == Role.System || message.Role == Role.Developer) continue;
                if(message.Role == Role.User)
                {
                    runResults.ConversationMessages.Add(new() { Message = message.Content.ToString(), MessageType = ThreadConversationMessageType.User });
                }
                else if (message.Role == Role.Assistant)
                {
                    string messageText = "";
                    if (message.Content?.ValueKind == JsonValueKind.String)
                    {
                        messageText += $"{message.Content.ToString()}\n";
                    }
                    if (message.ToolCalls != null)
                    {
                        foreach (var toolCall in message.ToolCalls)
                        {
                            if (toolCall.Function != null)
                            {
                                messageText += $"I called the tool named {toolCall.Function.Name} with {toolCall.Function.Arguments.ToString()}\n";
                            }
                        }    
                    }
                    runResults.ConversationMessages.Add(new() { Message = messageText, MessageType = ThreadConversationMessageType.Assistant });
                }
                else if (message.Role == Role.Tool)
                {
                    runResults.ConversationMessages.Add(new() { Message = $"I got this output: {message.Content[0].Text.ToString()}\n", MessageType = ThreadConversationMessageType.Tool });
                }
            }

            return runResults;
        }

        /// <summary>
        /// Runs the assistant thread with the specified assistant using the environment configuration.
        /// It manages the lifecycle of an assistant run, handles required actions, and optionally evaluates conversations.
        /// By default, the assistant will be created if it doesn't exist and a definition is found.
        /// This method's main purpose is to provide a simplified way to run an assistant thread to allow the use of a thread run as a tool call via local functions.
        /// </summary>
        /// <param name="assistantName">The options for running the assistant.</param>
        /// <param name="instructions">The configuration for Azure OpenAI.</param>
        /// <param name="evaluator">A named evaluator. In this version it causes the UseConversationEvaluator to be true but does NOT use an assistant with the provided name</param>
        /// <returns>The LastMessage from the thread run</returns>
        public static async Task<string> RunThread(string assistantName, string instructions, string? evaluator = "")
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

            // The primary purpose of this method is to provide a simplified way to run an assistant thread to allow the use of a thread run as a tool call via local functions.
            // Accordingly, autoCreate is set to false to avoid creating an assistant if it doesn't exist because otherwise parallel runs would create multiple assistants.
            var output = await RunThread(chatRunOptions, config!);
            if (output != null)
            {
                return output.LastMessage;
            }

            return "Unable to process request";
        }
        private static async Task PerformRunRequiredActions(AssistantCreateRequest assistantDef, List<Tool> tools, IReadOnlyList<OpenAI.ToolCall> toolCalls, List<Message> messages, string? oAuthUserAccessToken = null)
        {
            var assistantName = assistantDef.Name!;

            // Ensure the request builder cache is populated for the given assistant.
            await EnsureRequestBuilderCache(assistantName);

            if (!RequestBuilderCache.TryGetValue(assistantName, out var builders)) throw new Exception($"No request builders found for {assistantName}");

            var toolCallTasks = new List<Task<ToolOutput>>();

            // Iterate through each required tool call and execute the necessary requests.
            foreach (var requiredOutput in toolCalls)
            {
                if (!requiredOutput.IsFunction) continue;
                var toolCallId = requiredOutput.Id;
                var toolName = requiredOutput.Function.Name;
                var parameters = requiredOutput.Function.Arguments;
                if (builders.ContainsKey(toolName))
                {
                    // Create a new builder instance for each tool call to avoid shared state.
                    var builder = builders[toolName].Clone();
                    builder.Params = JsonSerializer.Deserialize<Dictionary<string, object>>(parameters.ToString());

                    var task = Task.Run(async () =>
                    {
                        string output;
                        if (builder.ActionType == ActionType.WebApi)
                        {
                            // Execute the request and collect the response.
                            var response = await builder.ExecuteWebApiAsync(oAuthUserAccessToken);
                            var responseContent = await response.Content.ReadAsStringAsync();

                            if (builder.ResponseSchemas.TryGetValue("200", out var schemaJson))
                            {
                                try
                                {
                                    var contentJson = JsonDocument.Parse(responseContent).RootElement;

                                    var filteredJson = ThreadUtility.FilterJsonBySchema(contentJson, schemaJson);
                                    output = filteredJson.GetRawText();
                                }
                                catch
                                {
                                    // If filtering fails, use the original response content.
                                    output = responseContent;
                                }
                            }
                            else
                            {
                                // If no response schema, use the original response content.
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
                        // Create a ToolOutput object.
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
                // Wait for all tool call tasks to complete.
                var toolOutputs = await Task.WhenAll(toolCallTasks);

                foreach (var toolCall in toolCalls)
                {
                    if (!toolCall.IsFunction) continue;
                    var id = toolCall.Id;
                    var toolOutput = toolOutputs.FirstOrDefault(to => to.ToolCallId == id);
                    if (toolOutput == null) throw new Exception("No match");
                    messages.Add(new Message(toolCallId: id, toolFunctionName: toolCall.Function.Name, new List<Content>() { new(toolOutput.Output!) }));
                }
            }
        }

        static async Task EnsureRequestBuilderCache(string assistantName)
        {
            // Check if the request builder cache already contains the assistant name.
            if (!RequestBuilderCache.TryGetValue(assistantName, out Dictionary<string, ToolCaller>? actionRequestBuilders))
            {
                var assistantRequestBuilders = new Dictionary<string, ToolCaller>();

                // Retrieve the OpenAPI schema files from the assistant definition folder.
                var openApiSchemaFiles = await AssistantDefinitionFiles.GetFilesInOpenApiFolder(assistantName);
                if (openApiSchemaFiles == null || !openApiSchemaFiles.Any()) return;

                foreach (var openApiSchemaFile in openApiSchemaFiles)
                {
                    var schema = await AssistantDefinitionFiles.GetFile(openApiSchemaFile);
                    if (schema == null)
                    {
                        Trace.TraceWarning("openApiSchemaFile {openApiSchemaFile} is null. Ignoring", openApiSchemaFile);
                        continue;
                    }

                    var json = Encoding.Default.GetString(schema);

                    // Validate and parse the OpenAPI specification from the JSON string.
                    var validationResult = OpenApiHelper.ValidateAndParseOpenApiSpec(json);
                    var spec = validationResult.Spec;

                    // Check if the validation was successful and if the specification is not null.
                    if (!validationResult.Status || spec == null)
                    {
                        Trace.TraceWarning("Json is not a valid OpenAPI spec {json}. Ignoring", json);
                        continue;
                    }

                    var requestBuilders = await ToolCaller.GetToolCallers(spec, assistantName);

                    // Add the request builders to the assistant request builders dictionary.
                    foreach (var tool in requestBuilders.Keys)
                    {
                        assistantRequestBuilders[tool] = requestBuilders[tool];
                    }
                }

                // Add the assistant request builders to the request builder cache.
                RequestBuilderCache[assistantName] = assistantRequestBuilders;
            }
        }
    }
}