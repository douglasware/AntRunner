namespace OpenAI.ObjectModels.SharedModels;

public record AssistantFileResponse : BaseResponse, IOpenAiModels.IId, IOpenAiModels.ICreatedAt
{
    /// <summary>
    /// The ID
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string AssistantId { get; set; } = string.Empty;

    /// <summary>
    /// The Unix timestamp (in seconds) for when the assistant file was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public int CreatedAt { get; set; }

    /// <summary>
    /// The identifier, which can be referenced in API endpoints.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}