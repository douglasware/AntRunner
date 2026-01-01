using OpenAI;
using OpenAI.Chat;

public static class MessageExtensions
{
    public static string GetText(this Message message)
    {
        string messageText = "";

        if (message.Role == Role.User || message.Role == Role.System)
        {
            // Content is a string for user messages
            messageText = message.Content?.ToString() ?? "";
        }
        else if (message.Role == Role.Assistant)
        {
            // Content is a string for assistant messages
            // Only return the content - don't format tool calls into text
            messageText = message.Content?.ToString() ?? "";
        }
        else if (message.Role == Role.Tool)
        {
            try
            {
                // Return raw tool result without formatting
                string textContent = message.Content![0].Text.ToString();
                messageText = textContent;
            }
            catch
            {
                // Workaround for serialize/deserialize behavior in the Message class
                // TODO: Identify root cause and file an issue
                string jsonString = message.Content![0].ToString();

                try
                {
                    JsonDocument jsonDoc = JsonDocument.Parse(jsonString);
                    JsonElement root = jsonDoc.RootElement;

                    if (root.TryGetProperty("text", out JsonElement textElement))
                    {
                        string textValue = textElement.GetString() ?? jsonString;
                        messageText = textValue;
                    }
                    else
                    {
                        messageText = jsonString;
                    }
                }
                catch
                {
                    messageText = jsonString;
                }
            }
        }

        return messageText;
    }
}