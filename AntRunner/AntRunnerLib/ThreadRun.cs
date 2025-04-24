namespace AntRunnerLib
{
    /// <summary>
    /// Represents a run of a thread within the assistant orchestrator.
    /// This class holds identifiers for both the thread and the specific run instance.
    /// </summary>
    public class ThreadRun
    {
        /// <summary>
        /// Gets or sets the identifier for the thread.
        /// </summary>
        public string? ThreadId { get; set; }

        /// <summary>
        /// Gets or sets the identifier for the specific run instance of the thread.
        /// </summary>
        public string? ThreadRunId { get; set; }
    }
}