namespace OpenAI.ObjectModels;

[JsonConverter(typeof(MessageContentConverter))]
public class MessageContentOneOfType
{
    public MessageContentOneOfType(string? contentAsString = null)
    {
        AsString = contentAsString;
    }

    public MessageContentOneOfType(List<MessageContent>? contentAsList = null)
    {
        AsList = contentAsList;
    }

    public MessageContentOneOfType()
    {
    }

    [JsonIgnore]
    public string? AsString { get; set; }

    [JsonIgnore]
    public List<MessageContent>? AsList { get; set; }
}