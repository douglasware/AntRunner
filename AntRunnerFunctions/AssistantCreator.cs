using AntRunnerLib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using OpenAI.ObjectModels.RequestModels;
using static AntRunnerFunctions.AssistantReader;

namespace AntRunnerFunctions
{
    /// <summary>
    /// Sub-orchestration function to get assistant creation options and return an assistant ID.
    /// </summary>
    public static class AssistantCreator
    {
        /// <summary>
        /// Runs the sub-orchestration with the provided assistant name.
        /// </summary>
        /// <param name="context">The orchestration context.</param>
        /// <returns>The assistant ID.</returns>
        [Function(nameof(CreateAssistantOrchestration))]
        public static async Task<string> CreateAssistantOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(AssistantCreator));
            var state = context.GetInput<AssistantRunnerState>();

            // Log the assistant name
            logger.LogInformation("Getting assistant creation options for: {AssistantName}", state!.AssistantRunOptions!.AssistantName);

            // Try to get the ID (just in case)
            var assistantId = await context.CallActivityAsync<string>(nameof(TryGetAssistantId), state, RetryPolicy.Get());
            if(string.IsNullOrWhiteSpace(assistantId)) {
                var assistantDefinition = await context.CallActivityAsync<AssistantCreateRequest>(nameof(GetAssistantCreateRequest), state, RetryPolicy.Get());
                if(assistantDefinition == null) {
                    throw new Exception($"{state!.AssistantRunOptions!.AssistantName} does not exist and no definition was found. Can't continue.");
                }
                
                state.AssistantDefinition = assistantDefinition;

                if (assistantDefinition.ToolResources?.FileSearch?.VectorStoreIds?.Count > 0)
                {
                    // Trying to make a semi-robust identifier for this so that possibly past creators don't block recreation of the assistant if it doesn't exist now for some reason
                    var ensureVectorStoreOrchestratorId = $"EnsureVectorStoreOrchestration{state.AssistantRunOptions!.AssistantName}{state.Started.ToShortDateString()}{state.Started.Hour}";
                    logger.LogInformation("{createAssistantOrchestratorId} Trying to create: {AssistantName}", ensureVectorStoreOrchestratorId, state.AssistantRunOptions!.AssistantName);
                    var vectorStoreIds = await context.CallSubOrchestratorAsync<Dictionary<string, string?>>(nameof(VectorStoreManager.EnsureVectorStoreOrchestration), state, new TaskOptions(RetryPolicy.Get().Retry).WithInstanceId(ensureVectorStoreOrchestratorId));

                    for(int i=0; i < assistantDefinition.ToolResources.FileSearch.VectorStoreIds.Count; i++)
                    {
                        var storeName = assistantDefinition.ToolResources.FileSearch.VectorStoreIds[i];
                        if (vectorStoreIds == null || vectorStoreIds[storeName] == null)
                        {
                            throw new Exception($"No ID for {storeName}");
                        }
                        assistantDefinition.ToolResources.FileSearch.VectorStoreIds[i] = vectorStoreIds[storeName]!;
                    }
                }

                if (assistantDefinition.ToolResources?.CodeInterpreter?.FileIds != null && assistantDefinition.ToolResources?.CodeInterpreter?.FileIds.Count > 0)
                {
                    assistantDefinition.ToolResources.CodeInterpreter.FileIds = await context.CallActivityAsync<List<string>>(nameof(AddCodeInterpreterFiles), state, RetryPolicy.Get());
                }
                
                assistantId = await context.CallActivityAsync<string>(nameof(CreateAssistant), state, RetryPolicy.Get());
                logger.LogInformation("Created assistant {AssistantName}. Returning {assistantId}", state!.AssistantRunOptions!.AssistantName, assistantId);
            }
            else
            {
                logger.LogInformation("Found assistant ID for: {AssistantName}. Returning", state!.AssistantRunOptions!.AssistantName);
            }

            return assistantId;
        }
        /// <summary>
        /// Gets the assistant creation request based on the assistant run options in the state.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>The assistant creation request.</returns>
        [Function(nameof(GetAssistantCreateRequest))]
        public static async Task<AssistantCreateRequest?> GetAssistantCreateRequest(
            [ActivityTrigger] AssistantRunnerState state,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(AssistantCreator));
            logger.LogInformation("Getting assistant creation options for: {AssistantName}", state.AssistantRunOptions!.AssistantName);

            return await AssistantUtility.GetAssistantCreateRequest(state.AssistantRunOptions!.AssistantName);
        }

        /// <summary>
        /// Creates an assistant based on the assistant definition in the state.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>The assistant creation result as a string.</returns>
        [Function(nameof(CreateAssistant))]
        public static async Task<string?> CreateAssistant(
            [ActivityTrigger] AssistantRunnerState state,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(AssistantCreator));
            logger.LogInformation("Creating assistant from definition: {AssistantDefinition}", state.AssistantDefinition);

            return await AssistantUtility.Create(state.AssistantDefinition!, state.AzureOpenAIConfig);
        }

        /// <summary>
        /// Adds code interpreter files to the service based on the assistant definition in the state.
        /// </summary>
        /// <param name="state">The current state of the AssistantRunner.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>A list of strings indicating the result of adding the code interpreter files.</returns>
        [Function(nameof(AddCodeInterpreterFiles))]
        public static async Task<List<string>> AddCodeInterpreterFiles(
            [ActivityTrigger] AssistantRunnerState state,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(AssistantCreator));
            logger.LogInformation("Adding code interpreter files for assistant: {AssistantDefinition}", state.AssistantDefinition);

            return await CodeInterpreterFiles.CreateCodeInterpreterFiles(state.AssistantDefinition!, state.AzureOpenAIConfig);
        }
    }
}
