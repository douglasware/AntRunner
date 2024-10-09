using System.Diagnostics;
using AntRunnerLib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.SharedModels;

namespace AntRunnerFunctions
{
    /// <summary>
    /// Runs the assistant fully and returns the output
    /// </summary>
    public static class AssistantRunner
    {
        /// <summary>
        /// Runs the orchestrator with the provided assistant run options.
        /// </summary>
        /// <param name="context">The orchestration context.</param>
        /// <returns>ThreadRunOutput</returns>
        [Function(nameof(AssistantsRunnerOrchestrator))]
        public static async Task<ThreadRunOutput> AssistantsRunnerOrchestrator(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(AssistantsRunnerOrchestrator));

            var state = await context.CallActivityAsync<AssistantRunnerState>(nameof(InitializeState), context.GetInput<AssistantRunOptions>(), RetryPolicy.Get());

            // Log the assistant name and instructions
            logger.LogInformation("Running assistant: {AssistantName} with instructions: {Instructions}", state.AssistantRunOptions!.AssistantName, state.AssistantRunOptions!.Instructions);

            try
            {
                state.AssistantId = await GetAssistantId(context, logger, state);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get assistant ID for {state.AssistantRunOptions!.AssistantName}", ex);
            }

            var ids = await context.CallActivityAsync<ThreadRun>(nameof(CreateThreadAndRun), state, RetryPolicy.Get());
            state.ThreadId = ids.ThreadId;
            state.ThreadRunId = ids.ThreadRunId;

            ThreadRunOutput runOutput = new();
            do
            {
                state.RootRun = await context.CallActivityAsync<RunResponse>(nameof(GetRun), state, RetryPolicy.Get());
                state.CurrentRun = state.RootRun;

                if (state.RootRun.Status == "requires_action")
                {
                    await context.CallActivityAsync(nameof(PerformRunRequiredActions), state, RetryPolicy.Get());
                }

                else if (state.RootRun.Status == "completed")
                {
                    var runResults = await context.CallActivityAsync<ThreadRunOutput>(nameof(GetThreadOutput), state, RetryPolicy.Get());

                    runResults.Usage = state.RootRun.Usage;

                    if (!string.IsNullOrEmpty(state.AssistantRunOptions.Evaluator))
                    {
                        int turnCounter = 0;

                        //Limit to 3 turns
                        while (turnCounter < 2)
                        {
                            turnCounter++;
                            var evaluatorOptions = new AssistantRunOptions()
                            {
                                AssistantName = state.AssistantRunOptions.Evaluator,
                                Instructions = runResults.Dialog,
                            };
                            var evaluatorOutput = (await context.CallSubOrchestratorAsync<ThreadRunOutput>(nameof(AssistantsRunnerOrchestrator), evaluatorOptions, RetryPolicy.Get())).LastMessage;
                            if (!evaluatorOutput.Contains("End Conversation", StringComparison.OrdinalIgnoreCase))
                            {
                                state.ConversationUserProxyMessage = evaluatorOutput;
                                var turnIds = await context.CallActivityAsync<ThreadRun>(nameof(UpdateThreadAndRun), state, RetryPolicy.Get());
                                state.ThreadRunId = turnIds.ThreadRunId;

                                do
                                {
                                    state.CurrentRun = await context.CallActivityAsync<RunResponse>(nameof(GetRun), state, RetryPolicy.Get());
                                    if (state.CurrentRun.Status == "requires_action")
                                    {
                                        await context.CallActivityAsync(nameof(PerformRunRequiredActions), state);
                                    }
                                    else if (state.CurrentRun.Status == "completed")
                                    {
                                        runResults = await context.CallActivityAsync<ThreadRunOutput>(nameof(GetThreadOutput), state, RetryPolicy.Get());
                                        break;
                                    }
                                    else if (state.CurrentRun.Status == "failed")
                                    {
                                        throw new Exception($"Run failed: {state.RootRun.LastError?.Message}");
                                    }
                                    else if (state.CurrentRun.Status == "incomplete")
                                    {
                                        Trace.TraceError($"{nameof(AssistantsRunnerOrchestrator)}|Content Filter|{runResults.Dialog}");

                                        return new ThreadRunOutput()
                                        {
                                            Status = "incomplete",
                                            LastMessage = $"Run is incomplete because of {state.RootRun.IncompleteDetails?.Reason}"
                                        };
                                    }
                                    else if (state.CurrentRun.Status == "in_progress" || state.RootRun.Status == "queued")
                                    {
                                        var waitTime = TimeSpan.FromMilliseconds(1000);
                                        await context.CreateTimer(context.CurrentUtcDateTime.Add(waitTime), CancellationToken.None);
                                    }
                                    else
                                    {
                                        return new ThreadRunOutput()
                                        {
                                            Status = state.CurrentRun.Status,
                                            LastMessage = $"Run is cancelling or cancelled"
                                        };
                                    }
                                } while (true);
                            }
                            else
                            {
                                break;
                            }
                        };
                    }

                    runOutput = runResults;
                    if (!context.IsReplaying)
                    {
                        logger.LogInformation(state.AssistantRunOptions.AssistantName);
                        logger.LogInformation(state.ThreadRunId);
                        logger.LogInformation("Dialog: {dialog}", runResults.Dialog);
                    }

                }
                else if (state.RootRun.Status == "failed")
                {
                    throw new Exception($"Run failed: {state.RootRun.LastError?.Message}");
                }
                else if (state.RootRun.Status == "incomplete")
                {
                    Trace.TraceError($"{nameof(AssistantsRunnerOrchestrator)}|Content Filter|{state.RootRun.Instructions}");

                    return new ThreadRunOutput()
                    {
                        Status = "incomplete",
                        LastMessage = $"Run is incomplete because of {state.RootRun.IncompleteDetails?.Reason}"
                    };
                }
                else if (state.RootRun.Status == "in_progress" || state.RootRun.Status == "queued")
                {
                    var waitTime = TimeSpan.FromMilliseconds(1000);
                    await context.CreateTimer(context.CurrentUtcDateTime.Add(waitTime), CancellationToken.None);
                }
                else
                {
                    return new ThreadRunOutput()
                    {
                        Status = state.RootRun.Status,
                        LastMessage = $"Run is cancelling or cancelled"
                    };
                }

            } while (state.RootRun.Status != "completed");

            if (!string.IsNullOrEmpty(state.AssistantRunOptions.PostProcessor))
            {
                state.CurrentRunOutput = runOutput;
                runOutput = await context.CallActivityAsync<ThreadRunOutput>(nameof(RunPostProcessor), state, RetryPolicy.Get());
            }

            await context.CallActivityAsync(nameof(Cleanup), state, RetryPolicy.Get());

            return runOutput;
        }

        /// <summary>
        /// Gets the assistant ID, creating the assistant if it does not exist.
        /// </summary>
        /// <param name="context">The orchestration context.</param>
        /// <param name="logger">The logger to log information and errors.</param>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <returns>The assistant ID as a string.</returns>
        private static async Task<string> GetAssistantId(TaskOrchestrationContext context, ILogger logger, AssistantRunnerState state)
        {
            // Get the assistant ID
            var assistantId = await context.CallActivityAsync<string>(nameof(AssistantReader.TryGetAssistantId), state, RetryPolicy.Get());

            // If it isn't found
            var waitCounter = 0;
            while (string.IsNullOrWhiteSpace(assistantId))
            {
                // Trying to make a semi-robust identifier for this so that possibly past creators don't block recreation of the assistant if it doesn't exist now for some reason
                var createAssistantOrchestratorId = $"Create{state.AssistantRunOptions!.AssistantName}{state.Started.ToShortDateString()}{state.Started.Hour}";

                // Check to make sure it isn't being created. This could theoretically take awhile with a lot of knowledge files
                var subOrchestrationStatus = await context.CallActivityAsync<OrchestrationRuntimeStatus?>(nameof(GetInstanceStatus), createAssistantOrchestratorId, RetryPolicy.Get());

                // If subOrchestrationStatus is null, nothing has tried to create this assistant
                if (subOrchestrationStatus == null || subOrchestrationStatus == OrchestrationRuntimeStatus.Failed || subOrchestrationStatus == OrchestrationRuntimeStatus.Terminated)
                {
                    logger.LogInformation("{createAssistantOrchestratorId} Trying to create: {AssistantName}", createAssistantOrchestratorId, state.AssistantRunOptions!.AssistantName);
                    assistantId = await context.CallSubOrchestratorAsync<string>(nameof(AssistantCreator.CreateAssistantOrchestration), state, new TaskOptions(RetryPolicy.Get().Retry).WithInstanceId(createAssistantOrchestratorId));
                }
                else
                {
                    var waitTime = TimeSpan.FromSeconds(10 * waitCounter++);

                    await context.CreateTimer(context.CurrentUtcDateTime.Add(waitTime), CancellationToken.None);
                    assistantId = await context.CallActivityAsync<string>(nameof(AssistantReader.TryGetAssistantId), state, RetryPolicy.Get());

                    if (waitCounter == 5 && string.IsNullOrEmpty(assistantId))
                    {
                        throw new Exception($"{state.AssistantRunOptions!.AssistantName} not found or failed to create.");
                    }
                }
            }
            return assistantId;
        }

        /// <summary>
        /// Retrieves the orchestration runtime status for a given instance ID.
        /// </summary>
        /// <param name="instanceId">The instance ID of the orchestration.</param>
        /// <param name="client">The DurableTask client.</param>
        /// <returns>The orchestration runtime status, or null if not found.</returns>
        [Function(nameof(GetInstanceStatus))]
        public static async Task<OrchestrationRuntimeStatus?> GetInstanceStatus(
            [ActivityTrigger] string instanceId,
            [DurableClient] DurableTaskClient client)
        {
            var status = await client.GetInstanceAsync(instanceId);
            return status?.RuntimeStatus ?? null;
        }

        /// <summary>
        /// Initializes the state for the AssistantRunner.
        /// </summary>
        /// <param name="requestInput">The assistant run options.</param>
        /// <returns>An instance of AssistantRunnerState.</returns>
        [Function(nameof(InitializeState))]
        public static AssistantRunnerState InitializeState([ActivityTrigger] AssistantRunOptions requestInput)
        {
            return new()
            {
                Started = DateTime.UtcNow,
                AssistantRunOptions = requestInput,
                AzureOpenAiConfig = AzureOpenAiConfigFactory.Get()
            };
        }

        /// <summary>
        /// Creates a new thread and run for the assistant.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>A ThreadRun object containing the thread and run IDs.</returns>
        [Function(nameof(CreateThreadAndRun))]
        public static async Task<ThreadRun> CreateThreadAndRun([ActivityTrigger] AssistantRunnerState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CreateThreadAndRun");
            logger.LogInformation("Running {state.AssistantRunOptions.AssistantName}: {state.AssistantRunOptions}", state.AssistantRunOptions!.AssistantName, state.AssistantRunOptions.Instructions);

            var ids = await ThreadUtility.CreateThreadAndRun(state.AssistantId, state.AssistantRunOptions, state.AzureOpenAiConfig);

            return ids;
        }

        /// <summary>
        /// Updates an existing thread and run for the assistant.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>A ThreadRun object containing the updated thread and run IDs.</returns>
        [Function(nameof(UpdateThreadAndRun))]
        public static async Task<ThreadRun> UpdateThreadAndRun([ActivityTrigger] AssistantRunnerState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("UpdateThreadAndRun");
            logger.LogInformation("Running {state.AssistantRunOptions.AssistantName}: {state.AssistantRunOptions.Instructions}", state.AssistantRunOptions!.AssistantName, state.AssistantRunOptions.Instructions);

            var ids = await ThreadUtility.UpdateThreadAndRun(state.ThreadId!, state.AssistantId, state.ConversationUserProxyMessage!, state.AzureOpenAiConfig);

            return ids;
        }

        /// <summary>
        /// Retrieves the run response for the current thread and run.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>An instance of RunResponse representing the current run.</returns>
        [Function(nameof(GetRun))]
        public static async Task<RunResponse> GetRun([ActivityTrigger] AssistantRunnerState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CreateThreadAndRun");
            logger.LogInformation("Running {state.AssistantRunOptions.AssistantName}: {state.AssistantRunOptions.Instructions}", state.AssistantRunOptions!.AssistantName, state.AssistantRunOptions.Instructions);

            if (state.ThreadId == null || state.ThreadRunId == null) { throw new Exception($"Can't get run output for missing {state.ThreadId} or {state.ThreadRunId}"); }

            return await ThreadUtility.GetRun(state.ThreadId, state.ThreadRunId, state.AzureOpenAiConfig);
        }

        /// <summary>
        /// Retrieves the thread output for the current thread.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>An instance of ThreadRunOutput representing the thread output.</returns>
        [Function(nameof(GetThreadOutput))]
        public static async Task<ThreadRunOutput> GetThreadOutput([ActivityTrigger] AssistantRunnerState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CreateThreadAndRun");
            logger.LogInformation("Running {state.AssistantRunOptions.AssistantName}: {state.AssistantRunOptions.Instructions}", state.AssistantRunOptions!.AssistantName, state.AssistantRunOptions.Instructions);

            if (state.ThreadId == null || state.ThreadRunId == null) { throw new Exception($"Can't get run output for missing {state.ThreadId} or {state.ThreadRunId}"); }

            return await ThreadUtility.GetThreadOutput(state.ThreadId, state.AzureOpenAiConfig);
        }

        /// <summary>
        /// Performs the required actions for the current run.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        [Function(nameof(PerformRunRequiredActions))]
        public static async Task PerformRunRequiredActions([ActivityTrigger] AssistantRunnerState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CreateThreadAndRun");
            logger.LogInformation("Running PerformRunRequiredActions {state.AssistantRunOptions.AssistantName}: {state.AssistantRunOptions.Instructions}", state.AssistantRunOptions!.AssistantName, state.AssistantRunOptions.Instructions);

            if (state.ThreadId == null || state.ThreadRunId == null) { throw new Exception($"Can't get run output for missing {state.ThreadId} or {state.ThreadRunId}"); }

            await ThreadUtility.PerformRunRequiredActions(state.AssistantRunOptions.AssistantName, state.CurrentRun!, state.AzureOpenAiConfig, state.AssistantRunOptions.OauthUserAccessToken);
        }

        /// <summary>
        /// Retrieves the thread output for the current thread.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>An instance of ThreadRunOutput representing the thread output.</returns>
        [Function(nameof(RunPostProcessor))]
        public static async Task<ThreadRunOutput?> RunPostProcessor([ActivityTrigger] AssistantRunnerState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CreateThreadAndRun");
            logger.LogInformation("Running {state.AssistantRunOptions.AssistantName}: {state.AssistantRunOptions.Instructions}", state.AssistantRunOptions!.AssistantName, state.AssistantRunOptions.Instructions);

            if (state.ThreadId == null || state.ThreadRunId == null) { throw new Exception($"Can't get run output for missing {state.ThreadId} or {state.ThreadRunId}"); }

            if (state.AssistantRunOptions.PostProcessor != null)
            {
                return await ThreadUtility.RunPostProcessor(state.AssistantRunOptions.PostProcessor, state.CurrentRunOutput);
            }
            else
            {
                return state.CurrentRunOutput;
            }
        }

        /// <summary>
        /// Cleans up resources by deleting the current thread.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        [Function(nameof(Cleanup))]
        public static async Task Cleanup([ActivityTrigger] AssistantRunnerState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("Cleanup");
            logger.LogInformation("Deleting {state.ThreadId}", state.ThreadId);

            await ThreadUtility.DeleteThread(state.ThreadId!, state.AzureOpenAiConfig);
        }
    }
}