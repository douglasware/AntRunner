using System.Text.Json.Serialization;

namespace OpenAI.ObjectModels.RequestModels;

/// <summary>
/// The content of a message.
/// </summary>
public class MessageContent
{
    /// <summary>
    /// The value of Type property must be one of "text", "image_url"
    /// note: Currently openAI doesn't support images in the first system message.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// If the value of Type property is "text" then Text property must contain the message content text
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// Static helper method to create MessageContent Text
    /// <param name="text">The text content</param>
    /// </summary>
    public static MessageContent TextContent(string text)
    {
        return new() { Type = "text", Text = text };
    }
}