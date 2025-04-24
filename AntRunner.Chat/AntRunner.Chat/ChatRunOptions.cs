using System.ComponentModel.DataAnnotations;

namespace AntRunner.Chat
{

    /// <summary>
    /// Represents the options for running an assistant.
    /// </summary>
    public class ChatRunOptions
    {
        /// <summary>
        /// Gets or sets the name of the assistant.
        /// </summary>
        [Required]
        public required string AssistantName { get; set; }

        /// <summary>
        /// Gets or sets the instructions for the assistant.
        /// </summary>
        [Required]
        public required string Instructions { get; set; }

        /// <summary>
        /// Files to upload to the assistant and attach to the message.
        /// </summary>
        public List<ResourceFile>? Files { get; set; }

        /// <summary>
        /// Passed in from the starter. The web api gets the Authorization header value if it exists, otherwise null.
        /// </summary>
        public string? OauthUserAccessToken { get; set; }

        /// <summary>
        /// The optional name of an assistant to use for evaluation of the run.
        /// Note that the named assistant must be created ahead of time.
        /// </summary>
        public string? Evaluator { get; set; }

        /// <summary>
        /// Optional method to run after the assistant completes.
        /// Must be a static method in a loaded assembly as follows:
        /// public static async Task&lt;ThreadRunOutput&gt; PostProcessor(ThreadRunOutput threadRunOutput)
        /// 
        /// Example value: WebSearchFunctions.SearchTool.PostProcessor
        /// </summary>
        public string? PostProcessor { get; set; }
        
        /// <summary>
        /// The deployment Id of the model, e.g. 03-mini
        /// </summary>
        public string? DeploymentId { get; set; }
    }
}