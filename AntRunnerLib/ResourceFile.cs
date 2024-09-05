
namespace AntRunnerLib
{
    /// <summary>
    /// Specifies the file's purpose
    /// </summary>
    public enum ResourceType
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
        FileSearchToolResource,
        CodeInterpreterToolResource
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }

    /// <summary>
    /// Base64 file content for use by the assistant or thread
    /// </summary>
    public class ResourceFile
    {
        /// <summary>
        /// File name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The file content as Base64 string
        /// </summary>
        public string Base64Content { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the file's purpose
        /// </summary>
        public ResourceType ResourceType { get; set; }
    }
}
