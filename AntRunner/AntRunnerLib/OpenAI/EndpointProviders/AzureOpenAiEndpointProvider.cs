using System.Net;

namespace OpenAI.EndpointProviders;

internal class AzureOpenAiEndpointProvider : IOpenAiEndpointProvider
{
    private const string DeploymentsPrefix = "deployments";
    private const string ApiPrefix = "openai";
    private readonly string _apiVersion;
    private readonly string _deploymentId;


    public AzureOpenAiEndpointProvider(string apiVersion, string deploymentId)
    {
        _apiVersion = apiVersion;
        _deploymentId = deploymentId;
    }

    private string Prefix => $"{ApiPrefix}/{DeploymentsPrefix}/{WebUtility.UrlEncode(_deploymentId)}";
    private string AzureVersionQueryString => $"?api-version={_apiVersion}";
    private string AssistantPrefix => $"{ApiPrefix}/";

    public string FileDelete(string fileId)
    {
        return $"{AssistantPrefix}/files/{fileId}{AzureVersionQueryString}";
    }

    public string FilesList()
    {
        return Files();
    }

    public string FilesUpload()
    {
        return Files();
    }

    public string FileRetrieve(string fileId)
    {
        return $"{AssistantPrefix}/files/{fileId}{AzureVersionQueryString}";
    }

    public string FileRetrieveContent(string fileId)
    {
        return $"{AssistantPrefix}/files/{fileId}/content{AzureVersionQueryString}";
    }

    public string AssistantCreate()
    {
        return $"{AssistantPrefix}/assistants{AzureVersionQueryString}";
    }

    public string AssistantRetrieve(string assistantId)
    {
        return $"{AssistantPrefix}/assistants/{assistantId}{AzureVersionQueryString}";
    }

    public string AssistantModify(string assistantId)
    {
        return $"{AssistantPrefix}/assistants/{assistantId}{AzureVersionQueryString}";
    }

    public string AssistantDelete(string assistantId)
    {
        return $"{AssistantPrefix}/assistants/{assistantId}{AzureVersionQueryString}";
    }

    public string AssistantList(PaginationRequest? assistantListRequest)
    {
        var url = $"{AssistantPrefix}/assistants{AzureVersionQueryString}";

        var query = assistantListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    public string AssistantFileCreate(string assistantId)
    {
        return $"{AssistantPrefix}/assistants/{assistantId}/files{AzureVersionQueryString}";
    }

    public string AssistantFileRetrieve(string assistantId, string fileId)
    {
        return $"{AssistantPrefix}/assistants/{assistantId}/files/{fileId}{AzureVersionQueryString}";
    }

    public string AssistantFileDelete(string assistantId, string fileId)
    {
        return $"{AssistantPrefix}/assistants/{assistantId}/files/{fileId}{AzureVersionQueryString}";
    }

    public string AssistantFileList(string assistantId, PaginationRequest? assistantFileListRequest)
    {
        var url = $"{AssistantPrefix}/assistants/files{AzureVersionQueryString}";

        var query = assistantFileListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    public string ThreadCreate()
    {
        return $"{AssistantPrefix}/threads{AzureVersionQueryString}";
    }

    public string ThreadRetrieve(string threadId)
    {
        return $"{AssistantPrefix}/threads/{threadId}{AzureVersionQueryString}";
    }

    public string ThreadModify(string threadId)
    {
        return $"{AssistantPrefix}/threads/{threadId}{AzureVersionQueryString}";
    }

    public string ThreadDelete(string threadId)
    {
        return $"{AssistantPrefix}/threads/{threadId}{AzureVersionQueryString}";
    }

    public string MessageCreate(string threadId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/messages{AzureVersionQueryString}";
    }

    public string MessageRetrieve(string threadId, string messageId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/messages/{messageId}{AzureVersionQueryString}";
    }

    public string MessageModify(string threadId, string messageId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/messages/{messageId}{AzureVersionQueryString}";
    }

    public string MessageList(string threadId, PaginationRequest? messageListRequest)
    {
        var url = $"{AssistantPrefix}/threads/{threadId}/messages{AzureVersionQueryString}";

        var query = messageListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    public string MessageDelete(string threadId, string messageId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/messages/{messageId}{AzureVersionQueryString}";
    }

    public string RunCreate(string threadId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/runs{AzureVersionQueryString}";
    }

    public string RunRetrieve(string threadId, string runId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/runs/{runId}{AzureVersionQueryString}";
    }

    public string RunModify(string threadId, string runId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/runs/{runId}{AzureVersionQueryString}";
    }

    public string RunList(string threadId, PaginationRequest? runListRequest)
    {
        var url = $"{AssistantPrefix}/threads/{threadId}/runs{AzureVersionQueryString}";

        var query = runListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    public string RunSubmitToolOutputs(string threadId, string runId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/runs/{runId}/submit_tool_outputs{AzureVersionQueryString}";
    }

    public string RunCancel(string threadId, string runId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/runs/{runId}/cancel{AzureVersionQueryString}";
    }

    public string ThreadAndRunCreate()
    {
        return $"{AssistantPrefix}/threads/runs{AzureVersionQueryString}";
    }

    public string RunStepRetrieve(string threadId, string runId, string stepId)
    {
        return $"{AssistantPrefix}/threads/{threadId}/runs/{runId}/steps/{stepId}{AzureVersionQueryString}";
    }

    public string RunStepList(string threadId, string runId, PaginationRequest? runStepListRequest)
    {
        var url = $"{AssistantPrefix}/threads/{threadId}/runs/{runId}/steps{AzureVersionQueryString}";

        var query = runStepListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    public string VectorStoreCreate()
    {
        return $"{AssistantPrefix}/vector_stores{AzureVersionQueryString}";
    }

    public string VectorStoreList(PaginationRequest baseListRequest)
    {
        var url = $"{AssistantPrefix}/vector_stores{AzureVersionQueryString}";

        var query = baseListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    public string VectorStoreRetrieve(string vectorStoreId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}{AzureVersionQueryString}";
    }

    public string VectorStoreModify(string vectorStoreId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}{AzureVersionQueryString}";
    }

    public string VectorStoreDelete(string vectorStoreId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}{AzureVersionQueryString}";
    }

    public string VectorStoreFileCreate(string vectorStoreId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}/files{AzureVersionQueryString}";
    }

    public string VectorStoreFileRetrieve(string vectorStoreId, string fileId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}/files/{fileId}{AzureVersionQueryString}";
    }

    public string VectorStoreFileDelete(string vectorStoreId, string fileId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}/files/{fileId}{AzureVersionQueryString}";
    }

    public string VectorStoreFileList(string vectorStoreId, VectorStoreFileListRequest? baseListRequest)
    {
        var url = $"{AssistantPrefix}/vector_stores/{vectorStoreId}/files{AzureVersionQueryString}";

        var query = baseListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    public string VectorStoreFileBatchCreate(string vectorStoreId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}/file_batches{AzureVersionQueryString}";
    }

    public string VectorStoreFileBatchRetrieve(string vectorStoreId, string batchId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}/file_batches/{batchId}{AzureVersionQueryString}";
    }

    public string VectorStoreFileBatchCancel(string vectorStoreId, string batchId)
    {
        return $"{AssistantPrefix}/vector_stores/{vectorStoreId}/file_batches/{batchId}/cancel{AzureVersionQueryString}";
    }

    public string VectorStoreFileBatchList(string vectorStoreId, string batchId, PaginationRequest? baseListRequest)
    {
        var url = $"{AssistantPrefix}/vector_stores/{vectorStoreId}/file_batches/{batchId}{AzureVersionQueryString}";

        var query = baseListRequest?.GetQueryParameters();
        if (!string.IsNullOrWhiteSpace(query))
        {
            url = $"{url}&{query}";
        }

        return url;
    }

    private string Files()
    {
        return $"{AssistantPrefix}/files{AzureVersionQueryString}";
    }
}