namespace OpenAI.ObjectModels.RequestModels;

public class RunModifyRequest : IOpenAiModels.IMetaData
{
    /// <summary>
    /// Set of 16 key-value pairs that can be attached to an object.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}