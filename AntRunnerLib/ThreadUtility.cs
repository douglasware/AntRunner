using AntRunnerLib.AssistantDefinitions;
using FunctionCalling;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using static AntRunnerLib.ClientUtility;

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
        /// <param name="message">The message content.</param>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <returns>A task representing the asynchronous operation, with a result of the created thread run.</returns>
        public static async Task<ThreadRun> CreateThreadAndRun(string assistantId, string message, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Get the OpenAI client using the provided Azure OpenAI configuration.
            var client = GetOpenAiClient(azureOpenAiConfig);

            // Construct the thread creation request with the user message.
            ThreadCreateRequest threadOptions = new ThreadCreateRequest();
            threadOptions.Messages = [new() { Role = "user", Content = new OpenAI.ObjectModels.MessageContentOneOfType(message) }];

            // Create a new thread and run it with the given assistant ID and thread options.
            var run = await client.Runs.CreateThreadAndRun(new CreateThreadAndRunRequest
            {
                AssistantId = assistantId,
                Thread = threadOptions,
            });

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
                Trace.TraceWarning($"User messages and thread run counts differ: {userMessages.Count()} != {threadRuns.Count()}");
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

                            if (messageContent.Text?.Annotations != null)
                            {
                                annotations.AddRange(messageContent.Text.Annotations);
                            }

                            // Add the message to the conversation messages.
                            output.ConversationMessages.Add(new() { MessageType = message.Role == "assistant" ? ThreadConversationMessageType.Assistant : ThreadConversationMessageType.User, Message = message.Content!.First()!.Text!.Value });
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

        private static readonly ConcurrentDictionary<string, Dictionary<string, ActionRequestBuilder>> RequestBuilderCache = new();

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

            var requiredToolcalls = new Dictionary<string, ToolCall>();
            var submitToolOutputsToRunRequest = new SubmitToolOutputsToRunRequest();

            // Iterate through each required tool call and execute the necessary requests.
            foreach (var requiredOutput in currentRun.RequiredAction!.SubmitToolOutputs?.ToolCalls!)
            {
                if (requiredOutput.FunctionCall == null) continue;

                requiredToolcalls[requiredOutput.FunctionCall.Name!] = requiredOutput;

                if (builders.ContainsKey(requiredOutput.FunctionCall.Name!))
                {
                    var builder = builders[requiredOutput.FunctionCall.Name!];
                    builder.Params = requiredOutput.FunctionCall.ParseArguments();

                    var output = string.Empty;
                    if (builder.ActionType == ActionType.WebApi)
                    {
                        // Execute the request and collect the response.
                        var response = await builder.ExecuteWebApiAsync(oAuthUserAccessToken);
                        output = await response.Content.ReadAsStringAsync();
                    }
                    else
                    {
                        output = JsonSerializer.Serialize(await builder.ExecuteLocalFunctionAsync());   
                    }
                    // Add the tool output to the submission request.
                    submitToolOutputsToRunRequest.ToolOutputs.Add(new ToolOutput()
                    {
                        Output = output,
                        ToolCallId = requiredOutput.Id
                    });
                }
            }

            // Submit the tool outputs to complete the current run.
            var client = GetOpenAiClient(azureOpenAiConfig);
            await client.RunSubmitToolOutputs(threadId, threadRunId ?? throw new InvalidOperationException(), submitToolOutputsToRunRequest);
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
            if (!RequestBuilderCache.TryGetValue(assistantId, out Dictionary<string, ActionRequestBuilder>? actionRequestBuilders))
            {
                var assistantRequestBuilders = new Dictionary<string, ActionRequestBuilder>();

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
                    var openApiHelper = new OpenApiHelper();

                    // Validate and parse the OpenAPI specification from the JSON string.
                    var validationResult = openApiHelper.ValidateAndParseOpenApiSpec(json);
                    var spec = validationResult.Spec;

                    // Check if the validation was successful and if the specification is not null.
                    if (!validationResult.Status || spec == null)
                    {
                        Trace.TraceWarning("Json is not a valid OpenAPI spec {json}. Ignoring", json);
                        continue;
                    }

                    // Extract tool definitions from the OpenAPI specification.
                    var toolDefinitions = openApiHelper.GetToolDefinitions(spec);

                    // Get request builders for the extracted tool definitions and assistant name.
                    var requestBuilders = await openApiHelper.GetRequestBuilders(spec, toolDefinitions, assistantName);

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
    }
}