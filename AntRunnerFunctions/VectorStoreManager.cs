using AntRunnerLib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace AntRunnerFunctions
{
    /// <summary>
    /// Sub-orchestration function to get assistant creation options and return an assistant ID.
    /// </summary>
    public static class VectorStoreManager
    {
        /// <summary>
        /// Ensures the vector store referred to by the assistant exists and is ready
        /// </summary>
        /// <param name="context">The orchestration context.</param>
        /// <returns>The assistant ID.</returns>
        [Function(nameof(EnsureVectorStoreOrchestration))]
        public static async Task<Dictionary<string, string?>> EnsureVectorStoreOrchestration(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(VectorStoreManager));
            var parentState = context.GetInput<AssistantRunnerState>();

            if (parentState?.AssistantDefinition == null) { throw new Exception("No assistant definition"); }

            if (parentState.AssistantDefinition.ToolResources?.FileSearch?.VectorStoreIds == null || parentState.AssistantDefinition.ToolResources?.FileSearch?.VectorStoreIds?.Count == 0)
            {
                { throw new Exception("No vector store in assistant to ensure"); }
            }

            var ensureVectorStoreOrchestrationState = new EnsureVectorStoreOrchestrationState() { AssistantRunnerState = parentState };
            ensureVectorStoreOrchestrationState = await context.CallActivityAsync<EnsureVectorStoreOrchestrationState>(nameof(EnsureVectorStores), ensureVectorStoreOrchestrationState, RetryPolicy.Get());
            ensureVectorStoreOrchestrationState = await context.CallActivityAsync<EnsureVectorStoreOrchestrationState>(nameof(CreateVectorFiles), ensureVectorStoreOrchestrationState, RetryPolicy.Get());
            bool ready = false;
            while (!ready)
            {
                ready = await context.CallActivityAsync<bool>(nameof(CheckForVectorStoreCompletion), ensureVectorStoreOrchestrationState, RetryPolicy.Get());
                await context.CreateTimer(new TimeSpan(0, 0, 30), CancellationToken.None);
            }
            return ensureVectorStoreOrchestrationState.VectorStores;
        }

        /// <summary>
        /// Ensures it exists and uploads the files
        /// </summary>
        /// <param name="state"></param>
        /// <param name="executionContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Function(nameof(EnsureVectorStores))]
        public static async Task<EnsureVectorStoreOrchestrationState> EnsureVectorStores([ActivityTrigger] EnsureVectorStoreOrchestrationState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(AssistantCreator));

            foreach (var vectorStoreName in state.AssistantRunnerState!.AssistantDefinition!.ToolResources!.FileSearch!.VectorStoreIds!)
            {
                var vectorStoreId = await VectorStore.EnsureVectorStore(state.AssistantRunnerState.AssistantDefinition!, vectorStoreName!, state.AssistantRunnerState!.AzureOpenAIConfig);
                state.VectorStores[vectorStoreName] = vectorStoreId;
            }

            return state;
        }

        /// <summary>
        /// Ensures it exists and uploads the files
        /// </summary>
        /// <param name="state"></param>
        /// <param name="executionContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Function(nameof(CreateVectorFiles))]
        public static async Task<EnsureVectorStoreOrchestrationState> CreateVectorFiles([ActivityTrigger] EnsureVectorStoreOrchestrationState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(AssistantCreator));
            logger.LogInformation("CreateVectorFiless for: {AssistantName}", state!.AssistantRunnerState!.AssistantRunOptions!.AssistantName);

            foreach (var vectorStoreName in state.AssistantRunnerState!.AssistantDefinition!.ToolResources!.FileSearch!.VectorStoreIds!)
            {
                await VectorStore.CreateVectorFiles(state.AssistantRunnerState.AssistantDefinition!, vectorStoreName!, state.VectorStores[vectorStoreName]!, state.AssistantRunnerState!.AzureOpenAIConfig);
            }

            return state;
        }

        /// <summary>
        /// Ensures it exists and uploads the files
        /// </summary>
        /// <param name="state"></param>
        /// <param name="executionContext"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        [Function(nameof(CheckForVectorStoreCompletion))]
        public static async Task<bool> CheckForVectorStoreCompletion([ActivityTrigger] EnsureVectorStoreOrchestrationState state, FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(AssistantCreator));
            logger.LogInformation("CreateVectorFiless for: {AssistantName}", state!.AssistantRunnerState!.AssistantRunOptions!.AssistantName);

            return await VectorStore.CheckForVectorStoreCompletion(state.VectorStores, state.AssistantRunnerState!.AzureOpenAIConfig);
        }
    }

    /// <summary>
    /// State for the EnsureVectorStoreOrchestration
    /// </summary>
    public class EnsureVectorStoreOrchestrationState
    {
        /// <summary>
        /// State for use by the AssistantRunner orchestration
        /// </summary>
        public AssistantRunnerState? AssistantRunnerState { get; set; }
        
        /// <summary>
        /// Map of vector store names to IDs
        /// </summary>
        public Dictionary<string, string?> VectorStores { get; set; } = new Dictionary<string, string?>();
    }
}
