using System.ComponentModel.DataAnnotations;

namespace AntRunnerLib
{

    /// <summary>
    /// Represents the options for running an assistant.
    /// </summary>
    public class AssistantRunOptions
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
        /// Gets or sets the thread identifier of a previous assistant run.
        /// </summary>
        public string? ThreadId { get; set; }

        /// <summary>
        /// Future...
        /// </summary>
        public List<ResourceFile>? Files { get; set; }

        /// <summary>
        /// Passed in from the starter. The web api gets the Authorization header value if it exists, otherwise null
        /// </summary>
        public string? OauthUserAccessToken { get; set; }

        /// <summary>
        /// If this is false, the orchestration will not use ConversationUserProxy
        /// The suborchestration for ConversationUserProxy uses 'false'
        /// </summary>
        public bool UseConversationEvaluator { get; set; } = true;
    }
}