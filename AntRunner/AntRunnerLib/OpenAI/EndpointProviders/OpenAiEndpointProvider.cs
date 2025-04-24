using System.Net;

namespace OpenAI.EndpointProviders;

internal class OpenAiEndpointProvider : IOpenAiEndpointProvider
{
    private readonly string _apiVersion;

    public OpenAiEndpointProvider(string apiVersion)
    {
        _apiVersion = apiVersion;
    }

    public string FileDelete(string fileId)
    {
        return $"{_apiVersion}/files/{fileId}";
    }

    public string FilesList()
    {
        return $"{_apiVersion}/files";
    }

    public string FilesUpload()
    {
        return $"{_apiVersion}/files";
    }

    public string FileRetrieve(string fileId)
    {
        return $"{_apiVersion}/files/{fileId}";
    }

    public string FileRetrieveContent(string fileId)
    {
        return $"{_apiVersion}/files/{fileId}/content";
    }
    public string AssistantCreate()
    {
        return $"{_apiVersion}/assistants";
    }

    public string AssistantRetrieve(string assistantId)
    {
        return $"{_apiVersion}/assistants/{assistantId}";
    }

    public string AssistantModify(string assistantId)
    {
        return $"{_apiVersion}/assistants/{assistantId}";
    }

    public string AssistantDelete(string assistantId)
    {
        return $"{_apiVersion}/assistants/{assistantId}";
    }

    public string AssistantList(PaginationRequest? assistantListRequest)
    {
        var url = $"{_apiVersion}/assistants";

        var query = assistantListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }

    public string AssistantFileCreate(string assistantId)
    {
        return $"{_apiVersion}/assistants/{assistantId}/files";
    }

    public string AssistantFileRetrieve(string assistantId, string fileId)
    {
        return $"{_apiVersion}/assistants/{assistantId}/files/{fileId}";
    }

    public string AssistantFileDelete(string assistantId, string fileId)
    {
        return $"{_apiVersion}/assistants/{assistantId}/files/{fileId}";
    }

    public string AssistantFileList(string assistantId, PaginationRequest? assistantFileListRequest)
    {
        var url = $"{_apiVersion}/assistants/{assistantId}/files";

        var query = assistantFileListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }

    public string ThreadCreate()
    {
        return $"{_apiVersion}/threads";
    }

    public string ThreadRetrieve(string threadId)
    {
        return $"{_apiVersion}/threads/{threadId}";
    }

    public string ThreadModify(string threadId)
    {
        return $"{_apiVersion}/threads/{threadId}";
    }

    public string ThreadDelete(string threadId)
    {
        return $"{_apiVersion}/threads/{threadId}";
    }

    public string MessageCreate(string threadId)
    {
        return $"{_apiVersion}/threads/{threadId}/messages";
    }

    public string MessageRetrieve(string threadId, string messageId)
    {
        return $"{_apiVersion}/threads/{threadId}/messages/{messageId}";
    }

    public string MessageModify(string threadId, string messageId)
    {
        return $"{_apiVersion}/threads/{threadId}/messages/{messageId}";
    }

    public string MessageList(string threadId, PaginationRequest? messageListRequest)
    {
        var url = $"{_apiVersion}/threads/{threadId}/messages";

        var query = messageListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }

    public string MessageDelete(string threadId, string messageId)
    {
        return $"{_apiVersion}/threads/{threadId}/messages/{messageId}";
    }

    public string RunCreate(string threadId)
    {
        return $"{_apiVersion}/threads/{threadId}/runs";
    }

    public string RunRetrieve(string threadId, string runId)
    {
        return $"{_apiVersion}/threads/{threadId}/runs/{runId}";
    }

    public string RunModify(string threadId, string runId)
    {
        return $"{_apiVersion}/threads/{threadId}/runs/{runId}";
    }

    public string RunList(string threadId, PaginationRequest? runListRequest)
    {
        var url = $"{_apiVersion}/threads/{threadId}/runs";

        var query = runListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }

    public string RunSubmitToolOutputs(string threadId, string runId)
    {
        return $"{_apiVersion}/threads/{threadId}/runs/{runId}/submit_tool_outputs";
    }

    public string RunCancel(string threadId, string runId)
    {
        return $"{_apiVersion}/threads/{threadId}/runs/{runId}/cancel";
    }

    public string ThreadAndRunCreate()
    {
        return $"{_apiVersion}/threads/runs";
    }

    public string RunStepRetrieve(string threadId, string runId, string stepId)
    {
        return $"{_apiVersion}/threads/{threadId}/runs/{runId}/steps/{stepId}";
    }

    public string RunStepList(string threadId, string runId, PaginationRequest? runStepListRequest)
    {
        var url = $"{_apiVersion}/threads/{threadId}/runs/{runId}/steps";

        var query = runStepListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }

    public string VectorStoreCreate()
    {
        return $"{_apiVersion}/vector_stores";
    }

    public string VectorStoreList(PaginationRequest baseListRequest)
    {
        var url = $"{_apiVersion}/vector_stores";

        var query = baseListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }

    public string VectorStoreRetrieve(string vectorStoreId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}";
    }

    public string VectorStoreModify(string vectorStoreId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}";
    }

    public string VectorStoreDelete(string vectorStoreId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}";
    }

    public string VectorStoreFileCreate(string vectorStoreId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}/files";
    }

    public string VectorStoreFileRetrieve(string vectorStoreId, string fileId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}/files/{fileId}";
    }

    public string VectorStoreFileDelete(string vectorStoreId, string fileId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}/files/{fileId}";
    }

    public string VectorStoreFileList(string vectorStoreId, VectorStoreFileListRequest? baseListRequest)
    {
        var url = $"{_apiVersion}/vector_stores/{vectorStoreId}/files";

        var query = baseListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }

    public string VectorStoreFileBatchCreate(string vectorStoreId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}/file_batches";
    }

    public string VectorStoreFileBatchRetrieve(string vectorStoreId, string batchId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}/file_batches/{batchId}";
    }

    public string VectorStoreFileBatchCancel(string vectorStoreId, string batchId)
    {
        return $"{_apiVersion}/vector_stores/{vectorStoreId}/file_batches/{batchId}/cancel";
    }

    public string VectorStoreFileBatchList(string vectorStoreId, string batchId, PaginationRequest? baseListRequest)
    {
        var url = $"{_apiVersion}/vector_stores/{vectorStoreId}/file_batches/{batchId}/files";

        var query = baseListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}?{query}";
        }

        return url;
    }
}