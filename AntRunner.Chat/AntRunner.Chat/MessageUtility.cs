using OpenAI;
using OpenAI.Chat;

public static class MessageExtensions
{
    public static string GetText(this Message message)
    {
        string messageText = "";

        if (message.Role == Role.User)
        {
            messageText = message.Content!.ToString();
        }
        else if (message.Role == Role.Assistant)
        {
            if (message.Content?.ValueKind == JsonValueKind.String)
            {
                messageText += $"{message.Content.ToString()}";
            }
            if (message.ToolCalls != null)
            {
                foreach (var toolCall in message.ToolCalls)
                {
                    if (toolCall.Function != null)
                    {
                        if (messageText.Length > 0) messageText += "\n";
                        messageText += $"I called the tool named {toolCall.Function.Name} with {toolCall.Function.Arguments}";
                    }
                }
            }
        }
        else if (message.Role == Role.Tool)
        {
            try
            {
                string textContent = message.Content![0].Text.ToString();
                messageText = $"I got this output: {textContent}";
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
                        messageText = $"I got this output: {textValue}";
                    }
                    else
                    {
                        messageText = $"I got this output: {jsonString}";
                    }
                }
                catch
                {
                    messageText = $"I got this output: {jsonString}";
                }
            }
        }

        return messageText;
    }
}