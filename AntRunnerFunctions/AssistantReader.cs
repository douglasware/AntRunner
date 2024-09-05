using AntRunnerLib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AntRunnerFunctions
{
    /// <summary>
    /// Activity function to get assistant creation options and return an assistant ID.
    /// </summary>
    public static class AssistantReader
    {
        /// <summary>
        /// Gets the assistant ID based on the assistant name in the state.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>The assistant ID as a string.</returns>
        [Function(nameof(TryGetAssistantId))]
        public static async Task<string?> TryGetAssistantId(
            [ActivityTrigger] AssistantRunnerState state,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GetAssistant");
            logger.LogInformation("Getting assistant ID for: {AssistantName}", state.AssistantRunOptions!.AssistantName);

            return await AssistantUtility.GetAssistantId(state.AssistantRunOptions!.AssistantName, state.AzureOpenAIConfig);
        }
    }
}
