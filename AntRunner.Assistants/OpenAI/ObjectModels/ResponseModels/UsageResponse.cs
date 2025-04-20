namespace OpenAI.ObjectModels.ResponseModels;

public record UsageResponse
{
    [JsonPropertyName("prompt_tokens")]
    public int? PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int? CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int? TotalTokens { get; set; }

    // TODO: Refactor this for general use
    public int? CachedPromptTokens { get; set; }
}