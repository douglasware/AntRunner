using AntRunner.ToolCalling.Functions;
using OpenAI.Chat;
using OpenAI;
using System.Text;
using System.Collections.Concurrent;
using AntRunner.ToolCalling.AssistantDefinitions.Storage;
using AntRunner.ToolCalling.AssistantDefinitions;

namespace AntRunner.Chat
{
    public static class ChatRunnerUtils
    {
        internal static readonly ConcurrentDictionary<string, Dictionary<string, ToolCaller>> RequestBuilderCache = new();

        public static ChatRunOutput? BuildRunResults(List<Message> messages, ChatResponse response)
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
                if (message.Role == Role.User)
                {
                    runResults.ConversationMessages.Add(new() { Message = message.Content!.ToString(), MessageType = ThreadConversationMessageType.User });
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
                    runResults.ConversationMessages.Add(new() { Message = $"I got this output: {message.Content![0].Text.ToString()}\n", MessageType = ThreadConversationMessageType.Tool });
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

        public static async Task PerformRunRequiredActions(AssistantDefinition assistantDef, List<Tool> tools, IReadOnlyList<OpenAI.ToolCall> toolCalls, List<Message> messages, string? oAuthUserAccessToken = null)
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

                                    var filteredJson = FilterJsonBySchema(contentJson, schemaJson);
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

        public static JsonElement FilterJsonBySchema(JsonElement content, JsonElement schema)
        {
            var filteredContent = new Dictionary<string, object>();

            foreach (var property in schema.GetProperty("properties").EnumerateObject())
            {
                if (content.TryGetProperty(property.Name, out var value))
                {
                    var type = property.Value.GetProperty("type").GetString();

                    if (type == "object" && property.Value.TryGetProperty("properties", out var nestedSchema))
                    {
                        filteredContent[property.Name] = FilterJsonBySchema(value, property.Value);
                    }
                    else if (type == "array" && property.Value.TryGetProperty("items", out var itemSchema))
                    {
                        var filteredArray = new List<object>();

                        foreach (var item in value.EnumerateArray())
                        {
                            filteredArray.Add(FilterJsonBySchema(item, itemSchema));
                        }

                        filteredContent[property.Name] = filteredArray;
                    }
                    else
                    {
                        filteredContent[property.Name] = value;
                    }
                }
            }

            var jsonString = JsonSerializer.Serialize(filteredContent);
            return JsonDocument.Parse(jsonString).RootElement;
        }

        public static async Task EnsureRequestBuilderCache(string assistantName)
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