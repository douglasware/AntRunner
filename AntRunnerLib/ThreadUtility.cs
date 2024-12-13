using AntRunnerLib.AssistantDefinitions;
using Functions;
using System.Collections.Concurrent;
using System.Text;
using AntRunnerLib.Functions;
using static System.Diagnostics.Trace;
using System.Reflection;

namespace AntRunnerLib
{
    /// <summary>
    /// Utility class for managing threads and runs within the assistant orchestrator.
    /// </summary>
    public class ThreadUtility
    {
        /// <summary>
        /// Creates a thread and runs with the specified assistant ID and message.
        /// </summary>
        /// <param name="assistantId">The assistant ID.</param>
        /// <param name="assistantRunOptions"></param>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <returns>A task representing the asynchronous operation, with a result of the created thread run.</returns>
        public static async Task<ThreadRun> CreateThreadAndRun(string assistantId, AssistantRunOptions assistantRunOptions, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Get the OpenAI client using the provided Azure OpenAI configuration.
            var client = GetOpenAiClient(azureOpenAiConfig);

            // Construct the thread creation request with the user message.
            ThreadCreateRequest threadOptions = new ThreadCreateRequest();
            threadOptions.Messages = [new() { Role = "user", Content = new OpenAI.ObjectModels.MessageContentOneOfType(assistantRunOptions.Instructions) }];

            var createThreadAndRunRequest = new CreateThreadAndRunRequest
            {
                AssistantId = assistantId,
                Thread = threadOptions,
                MaxCompletionTokens = 4096
            };

            if (assistantRunOptions.Files is { Count: > 0 })
            {
                var filePaths = new List<string>();
                foreach (var resourceFile in assistantRunOptions.Files)
                {
                    filePaths.Add(resourceFile.Path);
                }

                var fileIds = await Files.UploadFiles(filePaths, azureOpenAiConfig);
                if (fileIds.Count != filePaths.Count)
                {
                    throw new Exception($"Mismatch in counts {fileIds.Count} != {filePaths.Count}. Not all files were uploaded successfully");
                }

                for (int i = 0; i < assistantRunOptions.Files.Count; i++)
                {
                    if (assistantRunOptions.Files[i].ResourceType == ResourceType.CodeInterpreterToolResource)
                    {
                        createThreadAndRunRequest.ToolResources ??= new();
                        createThreadAndRunRequest.ToolResources.CodeInterpreter ??= new();
                        createThreadAndRunRequest.ToolResources.CodeInterpreter.FileIds ??= new();
                        createThreadAndRunRequest.ToolResources.CodeInterpreter.FileIds.Add(fileIds[i]);
                    }
                    else
                    {
                        throw new Exception($"Unsupported resource type {assistantRunOptions.Files[i].ResourceType}");
                    }
                }
            }

            // Create a new thread and run it with the given assistant ID and thread options.
            var run = await client.Runs.CreateThreadAndRun(createThreadAndRunRequest);

            if(run.Error != null)
            {
                throw new Exception($"Error creating run! {run.Error.Message}");
            }

            // Return the thread ID and the newly created thread run ID.
            return new() { ThreadId = run.ThreadId, ThreadRunId = run.Id };
        }

        /// <summary>
        /// Updates a thread and runs it with the specified assistant ID and message.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="assistantId">The assistant ID.</param>
        /// <param name="message">The message content.</param>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <returns>A task representing the asynchronous operation, with a result of the updated thread run.</returns>
        public static async Task<ThreadRun> UpdateThreadAndRun(string threadId, string assistantId, string message, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Get the OpenAI client using the provided Azure OpenAI configuration.
            var client = GetOpenAiClient(azureOpenAiConfig);

            // Construct the message creation request with the new message content.
            var newMessageRequest = new MessageCreateRequest();
            newMessageRequest.Content = new OpenAI.ObjectModels.MessageContentOneOfType(message);

            // Run the updated thread with the new message and assistant ID.
            var run = await client.Runs.RunCreate(threadId, new RunCreateRequest()
            {
                AdditionalMessages = [newMessageRequest],
                AssistantId = assistantId
            });

            // Check if the run encountered any errors and throw an exception if so.
            if (run.Error != null)
            {
                throw new Exception($"Error creating run! {run.Error.Message}");
            }

            // Return the thread ID and the newly updated thread run ID.
            return new() { ThreadId = run.ThreadId, ThreadRunId = run.Id };
        }

        /// <summary>
        /// Retrieves the specified run of a thread.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="threadRunId">The thread run ID.</param>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <returns>A task representing the asynchronous operation, with a result of the run response.</returns>
        public static async Task<RunResponse> GetRun(string threadId, string threadRunId, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Get the OpenAI client using the provided Azure OpenAI configuration.
            var client = GetOpenAiClient(azureOpenAiConfig);

            // Retrieve and return the specified run of the thread.
            return await client.RunRetrieve(threadId, threadRunId);
        }

        /// <summary>
        /// Retrieves the final output of the thread by aggregating runs and messages.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <returns>A task representing the asynchronous operation, with a result of the thread run output.</returns>
        public static async Task<ThreadRunOutput> GetThreadOutput(string threadId, AzureOpenAiConfig? azureOpenAiConfig)
        {
            var output = new ThreadRunOutput() { ThreadId = threadId };
            var client = GetOpenAiClient(azureOpenAiConfig);

            // List runs of the thread in ascending order.
            var threadRuns = (await client.ListRuns(threadId, new PaginationRequest { Order = "asc" })).Data;

            // List messages of the thread in ascending order.
            var messages = (await client.ListMessages(threadId, new PaginationRequest { Order = "asc" })).Data;

            if (threadRuns == null || messages == null)
            {
                throw new Exception("Couldn't get thread details");
            }

            // Filter out user messages from the messages list.
            var userMessages = messages.Where(o => o.Role == "user").ToArray();

            // Log a warning if the number of user messages and thread runs differ.
            if (userMessages.Count() != threadRuns.Count())
            {
                TraceWarning($"User messages and thread run counts differ: {userMessages.Count()} != {threadRuns.Count()}");
            }

            var annotations = new List<MessageAnnotation>();

            // Iterate through each run and process the messages.
            for (int i = 0; i < threadRuns.Count(); i++)
            {
                output.ConversationMessages.Add(new() { MessageType = ThreadConversationMessageType.User, Message = userMessages[i].Content![0].Text!.Value });

                var threadRun = threadRuns[i];
                if (threadRun.Id != null)
                {
                    var runSteps = await client.RunStepsList(threadId, threadRun.Id, new PaginationRequest { Order = "asc" });

                    output.Status = threadRun.Status;

                    foreach (var runStep in runSteps.Data!)
                    {
                        if (runStep.Status == "failed") continue;

                        if (runStep.Type == "message_creation")
                        {
                            var message = messages.FirstOrDefault(o => o.Id == runStep.StepDetails?.MessageCreation!.MessageId);
                            if (message == null) throw new Exception($"Couldn't get message {runStep.StepDetails?.MessageCreation!.MessageId}");

                            var messageContent = message.Content!.First();
                            if (messageContent == null) throw new Exception($"Couldn't get message content {message.Id}");

                            var annotationsContent = (message.Content?.FirstOrDefault(o =>
                                o.Text is { Annotations.Count: > 0 }));

                            if (annotationsContent != null)
                            {
                                annotations.AddRange(annotationsContent.Text!.Annotations!);
                            }

                            // Add the message to the conversation messages.
                            output.ConversationMessages.Add(new() { MessageType = message.Role == "assistant" ? ThreadConversationMessageType.Assistant : ThreadConversationMessageType.User, Message = message.Content!.First(o => o.Text != null).Text!.Value });
                        }
                        else // run_step.Type == "tool_calls"
                        {
                            foreach (var toolCall in runStep.StepDetails!.ToolCalls)
                            {
                                if (toolCall.Type == "code_interpreter")
                                {
                                    output.ConversationMessages.Add(new() { MessageType = ThreadConversationMessageType.Assistant, Message = $"I ran this code:\n```\n{((RunStepDetailsToolCallsCodeObject)toolCall).CodeInterpreterDetails.Input}\n```" });
                                }
                                else if (toolCall.Type == "file_search")
                                {
                                    output.ConversationMessages.Add(new() { MessageType = ThreadConversationMessageType.Assistant, Message = "I searched my knowledge base for the answer." });
                                }
                                else
                                {
                                    var functionCall = (RunStepDetailsToolCallsFunctionObject)toolCall;
                                    output.ConversationMessages.Add(new() { MessageType = ThreadConversationMessageType.Assistant, Message = $"I called the tool named {functionCall.Function.Name} with these arguments:\n```\n{functionCall.Function.Arguments}\n```\nand got this result:\n```\n{functionCall.Function.Output}\n```" });
                                }
                            }
                        }
                    }
                }
            }
            if (annotations.Count > 0)
            {
                output.Annotations = annotations;
            }

            output.LastMessage = output.ConversationMessages.Last().Message;
            return output;
        }

        /// <summary>
        /// Deletes the specified thread.
        /// </summary>
        /// <param name="threadId">The thread ID.</param>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task DeleteThread(string threadId, AzureOpenAiConfig? azureOpenAiConfig)
        {
            var client = GetOpenAiClient(azureOpenAiConfig);

            // Delete the specified thread.
            _ = await client.ThreadDelete(threadId);
        }

        private static readonly ConcurrentDictionary<string, Dictionary<string, ToolCallers>> RequestBuilderCache = new();

        /// <summary>
        /// Performs the required actions for the given run.
        /// </summary>
        /// <param name="assistantName">The assistant name.</param>
        /// <param name="currentRun">The current run response.</param>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <param name="oAuthUserAccessToken">Optional: The OAuth user access token.</param>
        public static async Task PerformRunRequiredActions(string assistantName, RunResponse currentRun, AzureOpenAiConfig? azureOpenAiConfig, string? oAuthUserAccessToken = null)
        {
            var threadId = currentRun.ThreadId;
            var threadRunId = currentRun.Id;
            var assistantId = currentRun.AssistantId;

            // Ensure the request builder cache is populated for the given assistant.
            await EnsureRequestBuilderCache(assistantName, assistantId);

            if (!RequestBuilderCache.TryGetValue(assistantId, out var builders)) throw new Exception($"No request builders found for {assistantName}: {assistantId}");

            var submitToolOutputsToRunRequest = new SubmitToolOutputsToRunRequest();

            var toolCallTasks = new List<Task<ToolOutput>>();

            // Iterate through each required tool call and execute the necessary requests.
            foreach (var requiredOutput in currentRun.RequiredAction!.SubmitToolOutputs?.ToolCalls!)
            {
                if (requiredOutput.FunctionCall == null) continue;

                if (builders.ContainsKey(requiredOutput.FunctionCall.Name!))
                {
                    // Create a new builder instance for each tool call to avoid shared state.
                    var builder = builders[requiredOutput.FunctionCall.Name!].Clone();
                    builder.Params = requiredOutput.FunctionCall.ParseArguments();

                    TraceInformation(
                        $"{nameof(PerformRunRequiredActions)} : {assistantName} : {currentRun.ThreadId} : {currentRun.Id} : Using {builder.Operation} with {requiredOutput.FunctionCall.Arguments}");

                    // Create a task to execute the tool call asynchronously.
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
                                output = JsonSerializer.Serialize(await builder.ExecuteLocalFunctionAsync());
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
                    throw new Exception($"No request builder found for {requiredOutput.FunctionCall.Name}");
                }
            }

            if (toolCallTasks.Count > 0)
            {
                // Wait for all tool call tasks to complete.
                var toolOutputs = await Task.WhenAll(toolCallTasks);

                // Add all tool outputs to the submission request.
                submitToolOutputsToRunRequest.ToolOutputs.AddRange(toolOutputs);

                // Submit the tool outputs to complete the current run.
                var client = GetOpenAiClient(azureOpenAiConfig);
                await client.RunSubmitToolOutputs(threadId, threadRunId ?? throw new InvalidOperationException(), submitToolOutputsToRunRequest);
            }
        }

        /// <summary>
        /// Ensures that the request builder cache is populated for the given assistant.
        /// </summary>
        /// <param name="assistantName">The assistant name.</param>
        /// <param name="assistantId">The assistant ID.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task EnsureRequestBuilderCache(string assistantName, string assistantId)
        {
            // Check if the request builder cache already contains the assistant ID.
            if (!RequestBuilderCache.TryGetValue(assistantId, out Dictionary<string, ToolCallers>? actionRequestBuilders))
            {
                var assistantRequestBuilders = new Dictionary<string, ToolCallers>();

                // Retrieve the OpenAPI schema files from the assistant definition folder.
                var openApiSchemaFiles = await AssistantDefinitionFiles.GetFilesInOpenApiFolder(assistantName);
                if (openApiSchemaFiles == null || !openApiSchemaFiles.Any()) return;

                foreach (var openApiSchemaFile in openApiSchemaFiles)
                {
                    var schema = await AssistantDefinitionFiles.GetFile(openApiSchemaFile);
                    if (schema == null)
                    {
                        TraceWarning("openApiSchemaFile {openApiSchemaFile} is null. Ignoring", openApiSchemaFile);
                        continue;
                    }

                    var json = Encoding.Default.GetString(schema);

                    // Validate and parse the OpenAPI specification from the JSON string.
                    var validationResult = OpenApiHelper.ValidateAndParseOpenApiSpec(json);
                    var spec = validationResult.Spec;

                    // Check if the validation was successful and if the specification is not null.
                    if (!validationResult.Status || spec == null)
                    {
                        TraceWarning("Json is not a valid OpenAPI spec {json}. Ignoring", json);
                        continue;
                    }

                    // Extract tool definitions from the OpenAPI specification.
                    var toolDefinitions = OpenApiHelper.GetToolDefinitionsFromSchema(spec);

                    // Get request builders for the extracted tool definitions and assistant name.
                    var requestBuilders = await ToolCallers.GetToolCallers(spec, toolDefinitions, assistantName);

                    // Add the request builders to the assistant request builders dictionary.
                    foreach (var tool in requestBuilders.Keys)
                    {
                        assistantRequestBuilders[tool] = requestBuilders[tool];
                    }
                }

                // Add the assistant request builders to the request builder cache.
                RequestBuilderCache[assistantId] = assistantRequestBuilders;
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

        /// <summary>
        /// Uses reflection to execute a post-processor function.
        /// </summary>
        /// <param name="postProcessor">The full name of the static method</param>
        /// <param name="runResults">The result to process</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task<ThreadRunOutput?> RunPostProcessor(string postProcessor, ThreadRunOutput? runResults)
        {
            if (string.IsNullOrEmpty(postProcessor))
            {
                throw new ArgumentException("PostProcessor cannot be null or empty", nameof(postProcessor));
            }

            if (runResults == null)
            {
                throw new ArgumentNullException(nameof(runResults));
            }

            // Split the postProcessor string to get the type and method names
            int lastDotIndex = postProcessor.LastIndexOf('.');
            if (lastDotIndex == -1)
            {
                throw new ArgumentException("Invalid PostProcessor format. Expected format: Namespace.Type.MethodName", nameof(postProcessor));
            }

            string typeName = postProcessor.Substring(0, lastDotIndex);
            string methodName = postProcessor.Substring(lastDotIndex + 1);

            // Get the containing type from the loaded assemblies
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == typeName);

            if (type == null)
            {
                throw new TypeLoadException($"Unable to load type '{typeName}'");
            }

            // Get the method
            MethodInfo? methodInfo = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public);
            if (methodInfo == null)
            {
                throw new MissingMethodException($"Method '{methodName}' not found in type '{typeName}'");
            }

            // Ensure the method signature is correct
            var parameters = methodInfo.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(ThreadRunOutput))
            {
                throw new InvalidOperationException($"Method '{methodName}' does not match the required signature: public static Task<ThreadRunOutput> {methodName}(ThreadRunOutput threadRunOutput) or public static ThreadRunOutput {methodName}(ThreadRunOutput threadRunOutput)");
            }

            // Invoke the method
            object? result;
            if (methodInfo.ReturnType == typeof(Task<ThreadRunOutput>))
            {
                result = await (Task<ThreadRunOutput?>)methodInfo.Invoke(null, new object[] { runResults })!;
            }
            else if (methodInfo.ReturnType == typeof(Task))
            {
                await (Task)methodInfo.Invoke(null, new object[] { runResults })!;
                result = runResults;
            }
            else if (methodInfo.ReturnType == typeof(ThreadRunOutput))
            {
                result = methodInfo.Invoke(null, new object[] { runResults });
            }
            else
            {
                throw new InvalidOperationException($"Method '{methodName}' does not match the required return type: Task<ThreadRunOutput> or ThreadRunOutput");
            }

            return (ThreadRunOutput?)result;
        }
    }
}