using AntRunner.ToolCalling;
using AntRunner.ToolCalling.AssistantDefinitions;
using AntRunner.ToolCalling.AssistantDefinitions.Storage;
using AntRunner.ToolCalling.Functions;
using OpenAI;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AntRunner.Chat
{
    /// <summary>
    /// Internal execution engine for running assistant threads.
    /// Extracted from ChatRunner to centralize execution logic.
    /// </summary>
    public static class ThreadRun
    {
        static readonly HttpClient _httpClient = HttpClientUtility.Get();

        // Cache of OpenAIClient instances keyed by API credentials to avoid per-call header races
        private static readonly ConcurrentDictionary<string, OpenAIClient> _openAiClientCache = new();

        private static OpenAIClient GetOrCreateOpenAiClient(AzureOpenAiConfig cfg, string? deploymentId, HttpClient? overrideClient = null)
        {
            if (overrideClient != null)
            {
                var auth = new OpenAIAuthentication(cfg.ApiKey);
                var settings = new OpenAISettings(resourceName: cfg.ResourceName, deploymentId: deploymentId, apiVersion: cfg.ApiVersion);
                return new OpenAIClient(auth, settings, overrideClient);
            }
            var key = $"{cfg.ApiKey}:{cfg.ResourceName}:{cfg.ApiVersion}:{deploymentId ?? string.Empty}";
            return _openAiClientCache.GetOrAdd(key, _ =>
            {
                var auth = new OpenAIAuthentication(cfg.ApiKey);
                var settings = new OpenAISettings(resourceName: cfg.ResourceName, deploymentId: deploymentId, apiVersion: cfg.ApiVersion);
                return new OpenAIClient(auth, settings, overrideClient ?? _httpClient);
            });
        }

        private static readonly ConcurrentDictionary<string, Dictionary<string, ToolCaller>> RequestBuilderCache = new();

        // Tracks which files have already been announced in a conversation to avoid duplicates
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> ConversationFileAnnouncements = new();

        private static bool AssistantHasFilesContextOption(AssistantDefinition def)
        {
            if (def.ContextOptions == null) return false;
            return def.ContextOptions.Any(kv => kv.Value != null && kv.Value.Contains("[@files]", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Formats new and modified file paths as a console-style code block for improved LLM attention.
        /// </summary>
        private static string FormatFileChangesConsole(IReadOnlyList<string> newFiles, IReadOnlyList<string> modifiedFiles)
        {
            var sb = new StringBuilder();
            sb.AppendLine("```console");
            if (newFiles.Count > 0)
            {
                sb.AppendLine("# New Files");
                foreach (var p in newFiles)
                {
                    sb.AppendLine(p);
                }
            }
            if (modifiedFiles.Count > 0)
            {
                if (newFiles.Count > 0) sb.AppendLine();
                sb.AppendLine("# Modified Files");
                foreach (var p in modifiedFiles)
                {
                    sb.AppendLine(p);
                }
            }
            sb.Append("```");
            return sb.ToString();
        }

        /// <summary>
        /// Clears the RequestBuilderCache for a specific assistant.
        /// </summary>
        /// <param name="assistantName">The name of the assistant to clear</param>
        public static void ClearRequestBuilderCache(string assistantName)
        {
            RequestBuilderCache.TryRemove(assistantName, out _);
        }

        /// <summary>
        /// Clears all cached request builders.
        /// Useful for testing or when bulk updates are made to assistants.
        /// </summary>
        public static void ClearAllRequestBuilderCache()
        {
            RequestBuilderCache.Clear();
        }

        /// <summary>
        /// Normalizes assistant-generated text emitted by the LLM stream.
        /// Currently replaces Unicode em-dash (\u2014) with ASCII hyphen-minus ('-').
        /// Applied only to assistant text at the LLM boundary (streaming deltas and final assistant message).
        /// </summary>
        private static string NormalizeAssistantText(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? string.Empty;
            }

            return text.Replace("\u2014", "\u2013");
        }

        /// <summary>
        /// Core execution engine for running assistant threads.
        /// </summary>
        /// <param name="isAgentInvocation">If true, seeds messages as System → previous → User. Previous must contain only current-turn attachment messages.</param>
        public static Task<ChatRunOutput?> ExecuteAsync(
            ChatRunOptions options,
            AzureOpenAiConfig config,
            List<Message>? previous,
            HttpClient? httpClient,
            MessageAddedEventHandler? onMessage,
            StreamingMessageProgressEventHandler? onStream,
            CancellationToken token,
            bool isAgentInvocation = false,
            string? contextMessage = null)
        {
            // Backward-compatible overload that forwards to the extended signature
            return ExecuteAsync(
                options,
                config,
                previous,
                httpClient,
                onMessage,
                onStream,
                onExternalToolCall: null,
                resumeWithoutNewUserMessage: false,
                token,
                isAgentInvocation,
                contextMessage);
        }

        /// <summary>
        /// Core execution engine for running assistant threads.
        /// </summary>
        /// <param name="isAgentInvocation">If true, seeds messages as System → previous → User. Previous must contain only current-turn attachment messages.</param>
        /// <param name="contextMessage">Optional context options message to inject before assistant instructions.</param>
        public static async Task<ChatRunOutput?> ExecuteAsync(
            ChatRunOptions options,
            AzureOpenAiConfig config,
            List<Message>? previous,
            HttpClient? httpClient,
            MessageAddedEventHandler? onMessage,
            StreamingMessageProgressEventHandler? onStream,
            ExternalToolCallEventHandler? onExternalToolCall,
            bool resumeWithoutNewUserMessage,
            CancellationToken token,
            bool isAgentInvocation = false,
            string? contextMessage = null)
        {
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(options.AssistantName);
            if (assistantDef == null)
            {
                throw new Exception($"Can't find assistant definition for '{options.AssistantName}'");
            }

            // 256000 is the maximum instruction length allowed by the API
            if (options.Instructions.Length >= 256000)
            {
                TraceWarning("Instructions are too long, truncating");
                options.Instructions = options.Instructions[..255999];
            }

            options.DeploymentId = assistantDef.Model ?? options.DeploymentId;

            var api = GetOrCreateOpenAiClient(config, options.DeploymentId, httpClient);

            var messages = new List<Message>();
            var hasKnowledge = assistantDef.Tools?.FirstOrDefault(t => t.Type == "file_search") != null;

            if (isAgentInvocation)
            {
                // Agent invocation: System instruction(s) → optional knowledge hint → Context Options → previous (attachments) → User

                if (!string.IsNullOrEmpty(assistantDef.Instructions))
                {
                    messages.Add(new Message(Role.System, assistantDef.Instructions));
                }

                if (hasKnowledge)
                {
                    messages.Add(new Message(Role.System, "Use SearchAssistantFiles for extended instructions and guidance on performing tasks"));
                }

                // Context options come AFTER primary system prompts to improve LLM attention
                if (!string.IsNullOrEmpty(contextMessage))
                {
                    messages.Add(new Message(Role.System, contextMessage));
                }

                if (messages.Count > 0)
                {
                    onMessage?.Invoke(null, new MessageAddedEventArgs(messages.Last().Role.ToString(), messages.Last().GetText()));
                }

                if (previous != null && previous.Count > 0)
                {
                    foreach (var previousMessage in previous)
                    {
                        messages.Add(previousMessage);
                    }
                }

                messages.Add(new Message(Role.User, options.Instructions));
                onMessage?.Invoke(null, new MessageAddedEventArgs(messages.Last().Role.ToString(), messages.Last().GetText()));
            }
            else if (previous != null && previous.Count > 0)
            {
                foreach (var previousMessage in previous)
                {
                    messages.Add(previousMessage);
                }
                if (!resumeWithoutNewUserMessage)
                {
                    messages.Add(new Message(Role.User, options.Instructions));
                    onMessage?.Invoke(null, new MessageAddedEventArgs(messages.Last().Role.ToString(), messages.Last().GetText()));
                }
            }
            else
            {
                messages =
                [
                    new Message(Role.System, !string.IsNullOrEmpty(assistantDef.Instructions) ? assistantDef.Instructions : "You are a helpful assistant"),
                ];
                if (hasKnowledge)
                {
                    messages.Add(new Message(Role.System, "Use SearchAssistantFiles for extended instructions and reference guidance"));
                }
                messages.Add(new Message(Role.User, options.Instructions));

                onMessage?.Invoke(null, new MessageAddedEventArgs(messages.First().Role.ToString(), messages.First().GetText()));
                onMessage?.Invoke(null, new MessageAddedEventArgs(messages.Last().Role.ToString(), messages.Last().GetText()));
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

                        var newFunction = new Function(function.Name, function.Description, functionParametersJsonNode, null);
                        var newTool = new Tool(newFunction);
                        tools.Add(newTool);
                    }
                }
            }

            bool continueChat = true;

            Choice? choice = null;
            ChatRunOutput? runResults = null;
            int evaluatorTurnCounter = 0;
            
            // Track files created/modified across all tool calls in this run
            var accumulatedNewFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var accumulatedModifiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                while (continueChat)
                {
                    token.ThrowIfCancellationRequested();
                    bool isReasoningModel = IsReasoningModel(options.DeploymentId ?? assistantDef.Model);

                    double? tempParam = null;
                    double? topPParam = null;
                    OpenAI.ReasoningEffort? reasonParam = null;

                    if (isReasoningModel)
                    {
                        // Use reasoning_effort for o3/o4 style models
                        if (assistantDef.ReasoningEffort != null)
                        {
                            reasonParam = (OpenAI.ReasoningEffort)assistantDef.ReasoningEffort;
                        }
                    }
                    else
                    {
                        tempParam = assistantDef.Temperature;
                        topPParam = assistantDef.TopP;
                    }

                    var chatRequest = new ChatRequest(messages, tools: tools, model: options.DeploymentId, temperature: tempParam, topP: topPParam, reasoningEffort: reasonParam);

                    ChatResponse response;
                    if (onStream != null)
                    {
                        response = await GetCompletionAndStreamAsync(api, chatRequest, onStream, token);
                    }
                    else
                    {
                        response = await api.ChatEndpoint.GetCompletionAsync(chatRequest, token);
                    }

                    messages.Add(response.FirstChoice.Message);

                    string? toolCallJson = null;
                    if (response.FirstChoice.Message.ToolCalls != null && response.FirstChoice.Message.ToolCalls.Count > 0)
                    {
                        toolCallJson = JsonSerializer.Serialize(response.FirstChoice.Message.ToolCalls);
                    }

                    var lastRole = messages.Last().Role;
                    var lastText = messages.Last().GetText();
                    if (lastRole == Role.Assistant)
                    {
                        lastText = NormalizeAssistantText(lastText);
                    }
                    onMessage?.Invoke(null, new MessageAddedEventArgs(
                        lastRole.ToString(),
                        lastText,
                        null,
                        null,
                        toolCallJson));
                    choice = response.FirstChoice;

                    switch (choice.FinishReason)
                    {
                        case "stop":
                            continueChat = false;
                            break;
                        case "tool_calls":
                            {
                                // Partition tool calls into client-handled vs server-handled based on ActionType
                                await EnsureRequestBuilderCache(assistantDef.Name!);
                                if (!RequestBuilderCache.TryGetValue(assistantDef.Name!, out var buildersPartition) || buildersPartition.Count == 0)
                                {
                                    // If no builders available, fall back to executing as server-handled
                                    var (newFiles, modifiedFiles) = await DoToolCalls(
                                        assistantDef,
                                        choice.Message.ToolCalls!,
                                        messages,
                                        oAuthUserAccessToken: options.oAuthUserAccessToken,
                                        httpClient: httpClient,
                                        messageAdded: onMessage);
                                    foreach (var f in newFiles) accumulatedNewFiles.Add(f);
                                    foreach (var f in modifiedFiles) accumulatedModifiedFiles.Add(f);
                                    break;
                                }

                                var clientHandled = new List<ToolCall>();
                                var serverHandled = new List<ToolCall>();
                                foreach (var tc in choice.Message.ToolCalls!)
                                {
                                    if (!tc.IsFunction)
                                    {
                                        serverHandled.Add(tc);
                                        continue;
                                    }
                                    if (buildersPartition.TryGetValue(tc.Function.Name, out var b))
                                    {
                                        if (b.ActionType == ActionType.ClientHandled)
                                        {
                                            clientHandled.Add(tc);
                                        }
                                        else
                                        {
                                            // WebApi and LocalFunction are server-side
                                            serverHandled.Add(tc);
                                        }
                                    }
                                    else
                                    {
                                        // Unknown tool → treat as server-handled to preserve existing error behavior
                                        serverHandled.Add(tc);
                                    }
                                }

                                if (clientHandled.Count > 0)
                                {
                                    // Emit client-handled subset to the host/client and pause the run
                                    try
                                    {
                                        var json = JsonSerializer.Serialize(clientHandled);
                                        onExternalToolCall?.Invoke(null, new ExternalToolCallEventArgs(json));
                                    }
                                    catch { /* non-fatal */ }

                                    // Mark run results as pending client tool and end loop
                                    runResults = BuildRunResults(messages, response) ?? new ChatRunOutput { Messages = messages };
                                    runResults.Status = "pending_client_tool";
                                    continueChat = false;
                                }
                                else
                                {
                                    // No client tools → execute all tools as usual
                                    var (newFiles, modifiedFiles) = await DoToolCalls(
                                        assistantDef,
                                        serverHandled,
                                        messages,
                                        oAuthUserAccessToken: options.oAuthUserAccessToken,
                                        httpClient: httpClient,
                                        messageAdded: onMessage);
                                    foreach (var f in newFiles) accumulatedNewFiles.Add(f);
                                    foreach (var f in modifiedFiles) accumulatedModifiedFiles.Add(f);
                                }
                            }
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

                    if (choice.FinishReason == "stop" && !string.IsNullOrEmpty(options.Evaluator))
                    {
                        while (evaluatorTurnCounter < 2)
                        {
                            evaluatorTurnCounter++;
                            var evaluatorOptions = new ChatRunOptions()
                            {
                                AssistantName = options.Evaluator,
                                Instructions = runResults?.Dialog ?? ""
                            };

                            var evaluatorOutput = (await ExecuteAsync(evaluatorOptions, config, null, httpClient, null, null, token, isAgentInvocation: false))?.LastMessage ?? "";
                            if (!evaluatorOutput.Contains("End Conversation", StringComparison.OrdinalIgnoreCase))
                            {
                                messages.Add(new Message(Role.User, evaluatorOutput));
                                continueChat = true;
                                break;
                            }
                        }
                    }
                }

                // Store accumulated files in the result for bubbling up to parent
                if (runResults != null)
                {
                    if (accumulatedNewFiles.Count > 0)
                        runResults.NewFiles = accumulatedNewFiles.ToList();
                    if (accumulatedModifiedFiles.Count > 0)
                        runResults.ModifiedFiles = accumulatedModifiedFiles.ToList();
                }

                return runResults;
            }
            catch (OperationCanceledException)
            {
                // Propagate cancellation so upstream callers can react appropriately
                throw;
            }
            catch (Exception ex)
            {
                throw new ChatConversationException(ex, runResults);
            }
        }

        private static async Task<ChatResponse> GetCompletionAndStreamAsync(OpenAIClient api, ChatRequest chatRequest, StreamingMessageProgressEventHandler streamingMessageProgress, CancellationToken cancellationToken)
        {
            if (streamingMessageProgress == null)
            {
                // should not happen but guard anyway
                return await api.ChatEndpoint.GetCompletionAsync(chatRequest, cancellationToken);
            }

            // The OpenAI-DotNet helper method will call our handler for every partial response (delta)
            ChatResponse finalResponse = await api.ChatEndpoint.StreamCompletionAsync(chatRequest, partialResponse =>
            {
                var delta = partialResponse.FirstChoice?.Delta;
                if (delta?.Content != null)
                {
                    var roleName = delta.Role != 0 ? delta.Role.ToString() : Role.Assistant.ToString();
                    var normalized = NormalizeAssistantText(delta.Content);
                    streamingMessageProgress.Invoke(null, new StreamingMessageProgressEventArgs(roleName, normalized));
                }
            }, streamUsage: true, cancellationToken);

            return finalResponse;
        }

        private static ChatRunOutput? BuildRunResults(List<Message> messages, ChatResponse response)
        {
            ChatRunOutput? runResults = new() { Messages = messages };

            var last = messages.Last();
            var lastText = last.GetText();
            if (last.Role == Role.Assistant)
            {
                lastText = NormalizeAssistantText(lastText);
            }
            runResults.LastMessage = lastText;

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
                    var normalizedAssistant = NormalizeAssistantText(messageText);
                    runResults.ConversationMessages.Add(new() { Message = normalizedAssistant, MessageType = ThreadConversationMessageType.Assistant });
                }
                else if (message.Role == Role.Tool)
                {
                    runResults.ConversationMessages.Add(new() { Message = messageText, MessageType = ThreadConversationMessageType.Tool });
                }
            }

            if (response.Usage != null)
            {
                runResults.Usage = new()
                {
                    CompletionTokens = response.Usage.CompletionTokens,
                    PromptTokens = response.Usage.PromptTokens ?? 0,
                    CachedPromptTokens = response.Usage.PromptTokensDetails?.CachedTokens ?? 0,
                    TotalTokens = response.Usage.TotalTokens ?? 0
                };
            }

            return runResults;
        }

        /// <summary>
        /// Executes tool calls and returns any files created/modified by the tools.
        /// </summary>
        /// <returns>Tuple of (NewFiles, ModifiedFiles) containing CWD-relative paths.</returns>
        public static async Task<(List<string> NewFiles, List<string> ModifiedFiles)> DoToolCalls(
            AssistantDefinition assistantDef,
            IReadOnlyList<ToolCall> toolCalls,
            List<Message> messages,
            string? oAuthUserAccessToken = null,
            HttpClient? httpClient = null,
            MessageAddedEventHandler? messageAdded = null)
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
                    if (builder.ActionType == ActionType.ClientHandled)
                    {
                        // Skip client-handled tools in server execution path
                        continue;
                    }

                    builder.Params = JsonSerializer.Deserialize<Dictionary<string, object>>(parameters.ToString());

                    // Fill in any missing required parameters using defaults from the schema
                    builder.AddMissingRequiredParamsFromSchema();

                    var task = Task.Run(async () =>
                    {
                        string output;
                        if (builder.ActionType == ActionType.WebApi)
                        {
                            string responseContent;
                            try
                            {
                                var response = await builder.ExecuteWebApiAsync(oAuthUserAccessToken, httpClient ?? _httpClient);
                                responseContent = await response.Content.ReadAsStringAsync();
                            }
                            catch (AntRunner.ToolCalling.Functions.ToolCaller.MissingAssistantAuthException)
                            {
                                return new ToolOutput
                                {
                                    Output = "This tool requires an API key for this host, but it isn't set. Open Guide Builder → Auth and provide the required value. Until then this API cannot be used.",
                                    ToolCallId = requiredOutput.Id
                                };
                            }

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

            // Collect all files from tool outputs (always, for bubbling up to parent)
            var allNewFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allModifiedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (toolCallTasks.Count > 0)
            {
                var toolOutputs = await Task.WhenAll(toolCallTasks);

                foreach (var toolCall in toolCalls)
                {
                    if (!toolCall.IsFunction) continue;
                    var id = toolCall.Id;
                    var toolOutput = toolOutputs.FirstOrDefault(to => to.ToolCallId == id) ?? throw new Exception("No match");
                    messages.Add(new Message(toolCallId: id, toolFunctionName: toolCall.Function.Name, [new(toolOutput.Output!)]));
                    messageAdded?.Invoke(null, new MessageAddedEventArgs(messages.Last().Role.ToString(), messages.Last().GetText(), toolCall.Id, toolCall.Function.Name, toolCall.Function.Arguments.ToString()));
                }

                // Extract file lists from all tool outputs
                foreach (var toolOutput in toolOutputs)
                {
                    if (string.IsNullOrEmpty(toolOutput.Output)) continue;

                    // Try to parse as ScriptExecutionResult to get file lists directly
                    try
                    {
                        var result = JsonSerializer.Deserialize<ScriptExecutionResult>(toolOutput.Output, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (result?.NewFiles != null)
                        {
                            foreach (var f in result.NewFiles) allNewFiles.Add(f);
                        }
                        if (result?.ModifiedFiles != null)
                        {
                            foreach (var f in result.ModifiedFiles) allModifiedFiles.Add(f);
                        }
                    }
                    catch { /* Not a ScriptExecutionResult, skip */ }
                }

            }

            return (allNewFiles.ToList(), allModifiedFiles.ToList());
        }

        static async Task EnsureRequestBuilderCache(string assistantName)
        {
            if (RequestBuilderCache.ContainsKey(assistantName))
            {
                return;
            }

            var assistantRequestBuilders = new Dictionary<string, ToolCaller>();

            // Get OpenAPI schemas from file system
            var openApiSchemaFiles = await AssistantDefinitionFiles.GetFilesInOpenApiFolder(assistantName);
            if (openApiSchemaFiles != null && openApiSchemaFiles.Count > 0)
            {
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
            }

            // Inject annotated tool builders dynamically based on assistant's tool list
            var assistantDef = await AssistantUtility.GetAssistantCreateRequest(assistantName);
            if (assistantDef?.Tools != null)
            {
                var allToolOperations = ToolContractRegistry.GetAllToolOperations();
                var assistantOperationIds = assistantDef.Tools
                    .Select(t => t.Function?.AsObject?.Name)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var (operationId, fullyQualifiedMethodName) in allToolOperations)
                {
                    if (assistantOperationIds.Contains(operationId))
                    {
                        try
                        {
                            var schema = ToolContractRegistry.GenerateOpenApiSchema(fullyQualifiedMethodName);
                            var validationResult = OpenApiHelper.ValidateAndParseOpenApiSpec(schema);

                            if (validationResult.Status && validationResult.Spec != null)
                            {
                                var requestBuilders = await ToolCaller.GetToolCallers(validationResult.Spec, assistantName);
                                foreach (var (toolName, builder) in requestBuilders)
                                {
                                    if (assistantOperationIds.Contains(toolName))
                                    {
                                        assistantRequestBuilders[toolName] = builder;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            TraceWarning($"Failed to generate schema for {fullyQualifiedMethodName}: {ex.Message}");
                        }
                    }
                }
            }

            // Inject crew-bridge tool builders for Guide assistants ONLY
            if (assistantDef?.Metadata != null && assistantDef.Metadata.TryGetValue("__crew_names__", out var crewNamesStr))
            {
                var crewNames = crewNamesStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (crewNames.Any())
                {
                    var bridgeSchema = CrewBridgeSchemaGenerator.GetSchema(crewNames);
                    var validationResult = OpenApiHelper.ValidateAndParseOpenApiSpec(bridgeSchema);
                    var spec = validationResult.Spec;

                    if (validationResult.Status && spec != null)
                    {
                        var bridgeRequestBuilders = await ToolCaller.GetToolCallers(spec, assistantName);
                        foreach (var tool in bridgeRequestBuilders.Keys)
                        {
                            assistantRequestBuilders[tool] = bridgeRequestBuilders[tool];
                        }
                    }
                }
            }

            RequestBuilderCache[assistantName] = assistantRequestBuilders;
        }

        /// <summary>
        /// Determines if a tool requires notebook context parameters to be injected.
        /// Uses the ToolContractRegistry to check for RequiresNotebookContext attribute.
        /// </summary>
        /// <param name="path">The fully qualified method path</param>
        /// <param name="toolName">The name of the tool (operation ID)</param>
        /// <returns>True if the tool requires notebook context parameters</returns>
        private static bool RequiresNotebookContext(string path, string toolName)
        {
            // First check if we have a direct path match in the registry
            var contract = ToolContractRegistry.GetContract(path);
            return contract.RequiresNotebookContext;
        }

        private static bool IsReasoningModel(string? model)
        {
            if (model == null) return false;
            return model.StartsWith("o", StringComparison.OrdinalIgnoreCase) || model.Equals("gpt-5");
        }

    }
}
