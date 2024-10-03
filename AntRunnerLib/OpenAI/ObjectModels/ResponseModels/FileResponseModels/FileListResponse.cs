namespace OpenAI.ObjectModels.ResponseModels.FileResponseModels;

public record FileListResponse : BaseResponse
{
    [JsonPropertyName("data")]
    public List<FileResponse>? Data { get; set; }
}