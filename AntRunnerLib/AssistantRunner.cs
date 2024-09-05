using OpenAI.ObjectModels.SharedModels;
using System.Diagnostics;
using System.Text.Json.Serialization;

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
        /// </summary>
        /// <param name="logger">The logger used to log information.</param>
        /// <param name="assistantRunOptions">The options for running the assistant.</param>
        /// <param name="config">The configuration for Azure OpenAI.</param>
        /// <returns>The output of the thread run including possible additional run output from addtional messages when using the default evaluator</returns>
        public static async Task<ThreadRunOutput?> RunThread(AssistantRunOptions assistantRunOptions, AzureOpenAIConfig config)
        {
            // Retrieve the assistant ID using the assistant name from the configuration
            var assistantId = await AssistantUtility.GetAssistantId(assistantRunOptions!.AssistantName, config);
            if (assistantId == null)
            {
                throw new ArgumentNullException(nameof(assistantId));
            }
            Trace.TraceInformation($"RunAssistant got assistant: {assistantId}");

            // Create a new thread and run it using the assistant ID and run options
            var ids = await ThreadUtility.CreateThreadAndRun(assistantId, assistantRunOptions.Instructions, config);

            // Check if thread creation and execution was successful
            if (ids.ThreadRunId == null || ids.ThreadId == null)
            {
                throw new Exception($"CreateThreadAndRun failed: {ids.ThreadId} {ids.ThreadRunId}");
            }

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

                    // Optionally use a conversation evaluator if specified in the run options
                    if (assistantRunOptions.UseConversationEvaluator)
                    {
                        int turnCounter = 0;

                        // Limit the evaluation to up to 3 turns
                        while (turnCounter < 2)
                        {
                            turnCounter++;
                            var evaluatorOptions = new AssistantRunOptions()
                            {
                                AssistantName = "ConversationUserProxy",
                                Instructions = runResults.Dialog,
                                UseConversationEvaluator = false
                            };

                            // Recursively run the conversation evaluator
                            var evaluatorOutput = (await RunThread(evaluatorOptions, config)).LastMessage ?? "";
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
                                    else if (complete = currentRun.Status == "completed")
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
                        };
                    }

                    Trace.TraceInformation($"Dialog: {runResults.Dialog}");
                    completed = true;
                }
                else
                {
                    // Wait for a short period before checking the run status again
                    Trace.TraceInformation("RunAssistant waiting 1/4 second");
                    await Task.Delay(250);
                }
            } while (!completed);

            // Delete the thread after completion
            await ThreadUtility.DeleteThread(ids.ThreadId, config);

            return runResults;
        }
    }
}