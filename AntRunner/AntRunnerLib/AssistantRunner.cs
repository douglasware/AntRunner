namespace AntRunnerLib
{
    /// <summary>
    /// Responsible for running assistant threads through interaction with various utilities.
    /// </summary>
    public class AssistantRunner
    {
        /// <summary>
        /// Runs the assistant thread with the specified run options and configuration.
        /// It manages the lifecycle of an assistant run, handles required actions, and optionally evaluates conversations.
        /// By default, the assistant will be created if it doesn't exist and a definition is found.
        /// </summary>
        /// <param name="assistantRunOptions">The options for running the assistant.</param>
        /// <param name="config">The configuration for Azure OpenAI.</param>
        /// <param name="autoCreate">Whether to automatically create the assistant if it doesn't exist.</param>
        /// <returns>The output of the thread run including possible additional run output from additional messages when using the default evaluator</returns>
        public static async Task<ThreadRunOutput?> RunThread(AssistantRunOptions assistantRunOptions, AzureOpenAiConfig config, bool autoCreate = true)
        {
            // Retrieve the assistant ID using the assistant name from the configuration
            var assistantId = await AssistantUtility.GetAssistantId(assistantRunOptions!.AssistantName, config, autoCreate);
            if (assistantId == null)
            {
                throw new ArgumentNullException(nameof(assistantId));
            }
            // TraceInformation($"{nameof(RunThread)}: Got {assistantRunOptions!.AssistantName}: {assistantId}");

            // 256000 is the maximum instruction length allowed by the API
            if (assistantRunOptions.Instructions.Length >= 256000)
            {
                TraceWarning("Instructions are too long, truncating");
                assistantRunOptions.Instructions = assistantRunOptions.Instructions.Substring(0, 255999);
            }

            // Create a new thread and run it using the assistant ID and run options
            var ids = await ThreadUtility.CreateThreadAndRun(assistantId, assistantRunOptions, config);

            // Check if thread creation and execution was successful
            if (ids.ThreadRunId == null || ids.ThreadId == null)
            {
                throw new Exception($"CreateThreadAndRun failed: {ids.ThreadId} {ids.ThreadRunId}");
            }

            var started = DateTime.UtcNow;
            TraceInformation(
                $"{nameof(RunThread)}: {assistantRunOptions!.AssistantName}: {ids.ThreadId} : {ids.ThreadRunId} : Started {started:yyyy-MM-dd HH:mm:ss}");

            ThreadRunOutput? runResults = null;
            bool completed = false;
            do
            {
                // Retrieve the current status of the run
                var run = await ThreadUtility.GetRun(ids.ThreadId, ids.ThreadRunId, config);

                // Perform any required actions if the status indicates so
                if (run.Status == "requires_action")
                {
                    await ThreadUtility.PerformRunRequiredActions(assistantRunOptions.AssistantName, run, config, assistantRunOptions.OauthUserAccessToken);
                }
                // If the run is completed, retrieve the thread output
                else if (run.Status == "completed")
                {
                    runResults = await ThreadUtility.GetThreadOutput(ids.ThreadId, config);
                    runResults.Usage = run.Usage;
                    // Optionally use a conversation evaluator if specified in the run options
                    if (!string.IsNullOrEmpty(assistantRunOptions.Evaluator))
                    {
                        int turnCounter = 0;

                        // Limit the evaluation to up to 3 turns
                        while (turnCounter < 2)
                        {
                            turnCounter++;
                            var evaluatorOptions = new AssistantRunOptions()
                            {
                                AssistantName = assistantRunOptions.Evaluator,
                                Instructions = runResults.Dialog
                            };

                            // Recursively run the conversation evaluator
                            var evaluatorOutput = (await RunThread(evaluatorOptions, config))?.LastMessage ?? "";
                            if (!evaluatorOutput.Contains("End Conversation", StringComparison.OrdinalIgnoreCase))
                            {
                                // Update the thread and run it again with the evaluator output
                                var turnIds = await ThreadUtility.UpdateThreadAndRun(ids.ThreadId, assistantId, evaluatorOutput, config);

                                bool complete = false;
                                do
                                {
                                    // Continuously check the status and perform required actions if needed
                                    var currentRun = await ThreadUtility.GetRun(ids.ThreadId, turnIds.ThreadRunId!, config);
                                    if (currentRun.Status == "requires_action")
                                    {
                                        await ThreadUtility.PerformRunRequiredActions(assistantRunOptions.AssistantName, currentRun, config, assistantRunOptions.OauthUserAccessToken);
                                    }
                                    else if (currentRun.Status != null && (complete = currentRun.Status == "completed"))
                                    {
                                        // Update run results upon completion
                                        runResults = await ThreadUtility.GetThreadOutput(ids.ThreadId, config);
                                    }
                                } while (!complete);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                    //TraceInformation($"{nameof(RunThread)}: {assistantRunOptions!.AssistantName}: {run.Id}: {runResults.Dialog}");
                    completed = true;
                }
                else if (run.Status == "failed")
                {
                    throw new Exception($"Run failed: {run.LastError?.Message}");
                }
                else if (run.Status == "incomplete")
                {
                    Trace.TraceError($"{nameof(RunThread)}|Content Filter|{run.Instructions}");
                    return new ThreadRunOutput()
                    {
                        Status = "incomplete",
                        LastMessage = $"Run is incomplete because of {run.IncompleteDetails?.Reason}"
                    };
                }
                else if (run.Status == "in_progress" || run.Status == "queued")
                {
                    // Wait for a short period before checking the run status again
                    // TraceInformation($"{nameof(RunThread)}: {assistantRunOptions!.AssistantName}: {run.Id}: Waiting 1 second");
                    await Task.Delay(1000);
                }
                else
                {
                    return new ThreadRunOutput()
                    {
                        Status = run.Status,
                        LastMessage = $"Run is cancelling or cancelled"
                    };
                }
            } while (!completed);

            if (!string.IsNullOrEmpty(assistantRunOptions.PostProcessor))
            {
                runResults = await ThreadUtility.RunPostProcessor(assistantRunOptions.PostProcessor, runResults);
            }

            TraceInformation(
                $"Usage:{runResults!.Usage?.PromptTokens}:{runResults.Usage?.CompletionTokens}:{runResults.Usage?.TotalTokens}");

            TraceInformation(
                $"{nameof(RunThread)} : {assistantRunOptions!.AssistantName} : {ids.ThreadId} : {ids.ThreadRunId} : Run Completed {(DateTime.UtcNow - started).TotalMilliseconds}");

            // Delete the thread after completion
            await ThreadUtility.DeleteThread(ids.ThreadId, config);

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
            var assistantRunOptions = new AssistantRunOptions()
            {
                AssistantName = assistantName,
                Instructions = instructions,
                Evaluator = evaluator
            };

            // The primary purpose of this method is to provide a simplified way to run an assistant thread to allow the use of a thread run as a tool call via local functions.
            // Accordingly, autoCreate is set to false to avoid creating an assistant if it doesn't exist because otherwise parallel runs would create multiple assistants.
            var output = await RunThread(assistantRunOptions, config!, false);
            if (output != null)
            {
                return output.LastMessage;
            }

            return "Unable to process request";
        }
    }
}