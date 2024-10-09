namespace AntRunnerLib
{
    /// <summary>
    /// State for use by the AssistantRunner orchestration
    /// </summary>
    public class AssistantRunnerState
    {
        /// <summary>
        /// Initial data input from the API
        /// </summary>
        public AssistantRunOptions? AssistantRunOptions { get; set; }

        /// <summary>
        /// Assistant to run
        /// </summary>
        public string AssistantId { get; set; } = string.Empty;

        /// <summary>
        /// Endpoint and API key
        /// </summary>
        public AzureOpenAiConfig? AzureOpenAiConfig { get; set; }
        
        /// <summary>
        /// Time the orchestration started
        /// </summary>
        public DateTime Started { get; set; }

        /// <summary>
        /// ID of the thread created for the run
        /// </summary>
        public string? ThreadId { get; set; }

        /// <summary>
        /// ID of the run
        /// </summary>
        public string? ThreadRunId { get; set; }

        /// <summary>
        /// Assistant definition from storage for use in the API to create an assistant
        /// </summary>
        public AssistantCreateRequest? AssistantDefinition { get; set; }

        /// <summary>
        /// The first run in the chain before evaluation by ConversationUserProxy
        /// </summary>
        public RunResponse? RootRun { get; set; }

        /// <summary>
        /// The message from the evaluator for coninuations 
        /// </summary>
        public string? ConversationUserProxyMessage { get; set; }
        
        /// <summary>
        /// The current run when extended by the conversation proxy
        /// </summary>
        public RunResponse? CurrentRun { get; set; }

        /// <summary>
        /// Current output for post processor
        /// </summary>
        public ThreadRunOutput? CurrentRunOutput { get; set; }
    }
}
