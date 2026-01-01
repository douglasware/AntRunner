using AntRunner.ToolCalling.Functions;
using OpenAI.Chat;
using OpenAI;
using System.Text;
using System.Collections.Concurrent;
using AntRunner.ToolCalling.AssistantDefinitions.Storage;
using AntRunner.ToolCalling.AssistantDefinitions;

namespace AntRunner.Chat
{
    public static class ChatRunnerUtils
    {
        internal static readonly ConcurrentDictionary<string, Dictionary<string, ToolCaller>> RequestBuilderCache = new();

        public static ChatRunOutput? BuildRunResults(List<Message> messages, ChatResponse response)
        {
            ChatRunOutput? runResults = new()
            {
                Messages = messages,             // Handle Content as string
                LastMessage = messages.Last().Content?.ToString() ?? ""
            };
            var choice = response.FirstChoice;
            runResults.Status = choice?.FinishReason ?? "unknown";
            foreach (var message in messages)
            {
                if (message.Role == Role.System || message.Role == Role.Developer) continue;
                if (message.Role == Role.User)
                {
                    // Handle Content as string for user messages
                    string userMessage = message.Content?.ToString() ?? "";
                    runResults.ConversationMessages.Add(new() { Message = userMessage, MessageType = ThreadConversationMessageType.User });
                }
                else if (message.Role == Role.Assistant)
                {
                    string messageText = "";
                    // Handle Content as string for assistant messages
                    if (!string.IsNullOrEmpty(message.Content?.ToString()))
                    {
                        messageText += $"{message.Content}\n";
                    }
                    // Tool calls are preserved as structured data in the message object
                    // Don't format them into text here
                    runResults.ConversationMessages.Add(new() { Message = messageText, MessageType = ThreadConversationMessageType.Assistant });
                }
                else if (message.Role == Role.Tool)
                {
                    // Return raw tool result without formatting
                    runResults.ConversationMessages.Add(new() { Message = message.Content![0].Text.ToString(), MessageType = ThreadConversationMessageType.Tool });
                }
            }

            runResults.Usage = new()
            {
                CompletionTokens = response.Usage.CompletionTokens,
                PromptTokens = response.Usage.PromptTokens ?? 0,
                CachedPromptTokens = response.Usage.PromptTokensDetails.CachedTokens,
                TotalTokens = response.Usage.TotalTokens ?? 0
            };

            return runResults;
        }
        public static JsonElement FilterJsonBySchema(JsonElement content, JsonElement schema)
        {
            var filteredContent = new Dictionary<string, object>();

            foreach (var property in schema.GetProperty("properties").EnumerateObject())
            {
                if (content.TryGetProperty(property.Name, out var value))
                {
                    var type = property.Value.GetProperty("type").GetString();
                    if (type == "object" && property.Value.TryGetProperty("properties", out _))
                    {
                        filteredContent[property.Name] = FilterJsonBySchema(value, property.Value);
                    }
                    else if (type == "array" && property.Value.TryGetProperty("items", out var itemSchema))
                    {
                        var filteredArray = new List<object>();

                        foreach (var item in value.EnumerateArray())
                        {
                            filteredArray.Add(FilterJsonBySchema(item, itemSchema));
                        }

                        filteredContent[property.Name] = filteredArray;
                    }
                    else
                    {
                        filteredContent[property.Name] = value;
                    }
                }
            }

            var jsonString = JsonSerializer.Serialize(filteredContent);
            return JsonDocument.Parse(jsonString).RootElement;
        }
    }
}