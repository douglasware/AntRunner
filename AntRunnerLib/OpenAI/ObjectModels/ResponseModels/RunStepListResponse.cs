using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAI.ObjectModels.SharedModels;

namespace OpenAI.ObjectModels.ResponseModels;

public record RunStepListResponse : DataWithPagingBaseResponse<List<RunStepResponse>>
{
}

public record RunStepResponse : BaseResponse, IOpenAiModels.IId, IOpenAiModels.ICreatedAt
{
    /// <summary>
    ///     The ID of the [assistant](/docs/api-reference/assistants) associated with the run step.
    /// </summary>
    [JsonPropertyName("assistant_id")]
    public string AssistantId { get; set; } = string.Empty;

    /// <summary>
    ///     The ID of the [thread](/docs/api-reference/threads) that was run.
    /// </summary>
    [JsonPropertyName("thread_id")]
    public string ThreadId { get; set; } = string.Empty;

    /// <summary>
    ///     The ID of the [run](/docs/api-reference/runs) that this run step is a part of.
    /// </summary>
    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    ///     The type of run step, which can be either `message_creation` or `tool_calls`.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     The status of the run step, which can be either `in_progress`, `cancelled`, `failed`, `completed`, `expired`, or
    ///     'incomplete'.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("step_details")]
    public RunStepDetails? StepDetails { get; set; }

    /// <summary>
    ///     The last error associated with this run step. Will be `null` if there are no errors.
    /// </summary>
    [JsonPropertyName("last_error")]
    public Error? LastError { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) for when the run step expired. A step is considered expired if the parent run is
    ///     expired.
    /// </summary>
    [JsonPropertyName("expired_at")]
    public int? ExpiredAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) for when the run step was cancelled.
    /// </summary>
    [JsonPropertyName("cancelled_at")]
    public int? CancelledAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) for when the run step failed.
    /// </summary>
    [JsonPropertyName("failed_at")]
    public int? FailedAt { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) for when the run step completed.
    /// </summary>
    [JsonPropertyName("completed_at")]
    public int? CompletedAt { get; set; }

    /// <summary>
    ///     Set of 16 key-value pairs that can be attached to an object. This can be useful for storing additional information
    ///     about the object in a structured format. Keys can be a maximum of 64 characters long and values can be a maxium of
    ///     512 characters long.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    ///     Usage statistics related to the run step. This value will be `null` while the run step&apos;s status is
    ///     `in_progress`.
    /// </summary>
    [JsonPropertyName("usage")]
    public UsageResponse? Usage { get; set; }

    /// <summary>
    ///     The Unix timestamp (in seconds) for when the run step was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public int CreatedAt { get; set; }

    /// <summary>
    ///     The identifier of the run step, which can be referenced in API endpoints.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; } = string.Empty;
}

public record RunStepDetails
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("message_creation")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunStepMessageCreation? MessageCreation { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonConverter(typeof(ToolCallsConverter))]
    public List<IToolCall> ToolCalls { get; set; } = new();

    public class RunStepMessageCreation
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = string.Empty;
    }
}

public interface IToolCall
{
    string Type { get; set; }
}

public class ToolCallsConverter : JsonConverter<List<IToolCall>>
{
    public override List<IToolCall> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var toolCalls = new List<IToolCall>();
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException();
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var jsonObject = JsonDocument.ParseValue(ref reader).RootElement;
            var type = jsonObject.GetProperty("type").GetString();

            IToolCall? toolCall = type switch
            {
                "code_interpreter" => JsonSerializer.Deserialize<RunStepDetailsToolCallsCodeObject>(jsonObject.GetRawText(), options),
                "file_search" => JsonSerializer.Deserialize<RunStepDetailsToolCallsFileSearchObject>(jsonObject.GetRawText(), options),
                "function" => JsonSerializer.Deserialize<RunStepDetailsToolCallsFunctionObject>(jsonObject.GetRawText(), options),
                _ => throw new JsonException($"Unknown tool call type: {type}")
            };

            if (toolCall != null)
            {
                toolCalls.Add(toolCall);
            }
        }

        return toolCalls;
    }

    public override void Write(Utf8JsonWriter writer, List<IToolCall> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var toolCall in value)
        {
            JsonSerializer.Serialize(writer, toolCall, toolCall.GetType(), options);
        }
        writer.WriteEndArray();
    }
}

public class RunStepDetailsToolCallsCodeObject : IToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("code_interpreter")]
    public CodeInterpreter CodeInterpreterDetails { get; set; } = new();

    public class CodeInterpreter
    {
        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;

        [JsonPropertyName("outputs")]
        public List<object> Outputs { get; set; } = new();
    }

    public class CodeOutputLogs
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("logs")]
        public string Logs { get; set; } = string.Empty;
    }

    public class CodeOutputImage
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public ImageDetails Image { get; set; } = new();

        public class ImageDetails
        {
            [JsonPropertyName("file_id")]
            public string FileId { get; set; } = string.Empty;
        }
    }
}

public class RunStepDetailsToolCallsFileSearchObject : IToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("file_search")]
    public object FileSearch { get; set; } = new();  // This is an empty object.
}

public class RunStepDetailsToolCallsFunctionObject : IToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("function")]
    public FunctionDetails Function { get; set; } = new();

    public class FunctionDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;

        [JsonPropertyName("output")]
        public string? Output { get; set; }  // Nullable
    }
}