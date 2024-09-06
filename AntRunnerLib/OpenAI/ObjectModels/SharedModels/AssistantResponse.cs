using System.Text.Json.Serialization;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;

namespace OpenAI.ObjectModels.SharedModels;

public record AssistantResponse : BaseResponse, IOpenAiModels.IId, IOpenAiModels.ICreatedAt, IOpenAiModels.IModel, IOpenAiModels.IMetaData, IOpenAiModels.ITools
{
    /// <summary>
    ///     The name of the assistant. The maximum length is 256 characters.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    ///     The description of the assistant. The maximum length is 512 characters.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    ///     The system instructions that the assistant uses.
    ///     The maximum length is 32768 characters.
    /// </summary>
    [JsonPropertyName("instructions")]
    public string? Instructions { get; set; }

    /// <summary>
    ///     A list of file IDs attached to this assistant.
    ///     There can be a maximum of 20 files attached to the assistant.
    ///     Files are ordered by their creation date in ascending order.
    /// </summary>
    [JsonPropertyName("file_ids")]
    public List<string> FileIds { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) for when the assistant was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public int CreatedAt { get; set; }

    /// <summary>
    ///     The identifier, which can be referenced in API endpoints.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    ///     A list of tools enabled on the assistant.
    /// </summary>
    [JsonPropertyName("tools")]
    public List<ToolDefinition> Tools { get; set; }

    /// <summary>
    /// A set of resources that are used by the assistant's tools. The resources are specific to the type of tool. For example, the code_interpreter tool requires a list of file IDs, while the file_search tool requires a list of vector store IDs.
    /// </summary>
    [JsonPropertyName("tool_resources")]
    public ToolResources? ToolResources { get; set; }

    /// <summary>
    ///     An alternative to sampling with temperature, called nucleus sampling, where the model considers the results of the
    ///     tokens with top_p probability mass. So 0.1 means only the tokens comprising the top 10% probability mass are
    ///     considered.
    ///     We generally recommend altering this or temperature but not both.
    /// </summary>
    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; set; }

    /// <summary>
    ///     Specifies the format that the model must output. Compatible with
    ///     <a href="https://platform.openai.com/docs/models/gpt-4o">GPT-4o</a>,
    ///     <a href="https://platform.openai.com/docs/models/gpt-4-turbo-and-gpt-4">GPT-4 Turbo</a>, and all GPT-3.5 Turbo
    ///     models since gpt-3.5-turbo-1106.
    ///     Setting to <c>{ "type": "json_object" }</c> enables JSON mode, which guarantees the message the model generates is
    ///     valid JSON. <br />
    ///     <b>Important: </b>when using JSON mode, you must also instruct the model to produce JSON yourself via a system or
    ///     user message.Without this, the model may generate an unending stream of whitespace until the generation reaches the
    ///     token limit, resulting in a long-running and seemingly "stuck" request.Also note that the message content may be
    ///     partially cut off if <c>finish_reason= "length"</c>, which indicates the generation exceeded <c>max_tokens</c> or
    ///     the
    ///     conversation exceeded the max context length.
    /// </summary>
    [JsonPropertyName("response_format")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseFormatOneOfType? ResponseFormat { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object. This can be useful for storing additional information
    ///     about the object in a structured format. Keys can be a maximum of 64 characters long and values can be a maxium of
    ///     512 characters long.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    ///     ID of the model to use. You can use the
    ///     <a href="https://platform.openai.com/docs/api-reference/models/list">List models</a> API to see all of your
    ///     available models, or see our <a href="https://platform.openai.com/docs/models/overview">Model overview</a> for
    ///     descriptions of them.
    /// </summary>
    [JsonPropertyName("model")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string Model { get; set; }

    /// <summary>
    ///     What sampling temperature to use, between 0 and 2. Higher values like 0.8 will make the output more random, while
    ///     lower values like 0.2 will make it more focused and deterministic.
    /// </summary>
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }
}