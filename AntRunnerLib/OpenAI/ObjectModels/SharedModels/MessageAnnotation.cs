using System.Text.Json.Serialization;

namespace OpenAI.ObjectModels.SharedModels;

/// <summary>
///     File citation |File path
/// </summary>
public record MessageAnnotation
{
    /// <summary>
    /// type can be：file_citation、file_path
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     The text in the message content that needs to be replaced.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("start_index")]
    public int StartIndex { get; set; }

    [JsonPropertyName("end_index")]
    public int EndIndex { get; set; }

    [JsonPropertyName("file_citation")]
    public FileCitation? FileCitation { get; set; }

    [JsonPropertyName("file_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FilePathAnnotation? FilePathAnnotation { get; set; }
}

public record FileCitation
{
    /// <summary>
    /// The ID of the specific File the citation/content  is from.
    /// </summary>
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;

    /// <summary>
    /// A patch to file the file name by fetching the file with file_id
    /// </summary>
    [JsonPropertyName("file_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The specific quote in the file. - always null currently from OpenAI api
    /// </summary>
    [JsonPropertyName("quote")]
    public string? Quote { get; set; }
}

public record FilePathAnnotation
{
    /// <summary>
    /// The ID of the file that was generated.
    /// </summary>
    [JsonPropertyName("file_id")]
    public string FileId { get; set; } = string.Empty;
}