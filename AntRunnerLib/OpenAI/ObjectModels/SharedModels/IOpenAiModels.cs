using OpenAI.ObjectModels.RequestModels;

namespace OpenAI.ObjectModels.SharedModels;

public interface IOpenAiModels
{
    public interface IId
    {
        string Id { get; set; }
    }

    public interface IModel
    {
        string? Model { get; set; }
    }
    
    public interface ITemperature
    {
        float? Temperature { get; set; }
    }

    public interface IAssistantId
    {
        string AssistantId { get; set; }
    }

    public interface ICreatedAt
    {
        public int CreatedAt { get; set; }
    }

    public interface IMetaData
    {
        public Dictionary<string, string> Metadata { get; set; }
    }

    public interface IFileIds
    {
        public List<string> FileIds { get; set; }
    }

    public interface ITools
    {
        public List<ToolDefinition> Tools { get; set; }
    }
}