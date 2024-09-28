using Functions;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AntRunnerLib.Functions;
using static AntRunnerLib.AssistantDefinitions.AssistantDefinitionFiles;
using static AntRunnerLib.ClientUtility;

namespace AntRunnerLib
{
    /// <summary>
    /// Fetch and autoCreate assistants
    /// </summary>
    public static class AssistantUtility
    {
        // The ConcurrentDictionary to act as our in-memory cache
        private static readonly ConcurrentDictionary<string, AssistantCreateRequest?> AssistantDefinitionCache = new();
        private static readonly ConcurrentDictionary<string, (List<AssistantResponse> Assistants, DateTime Timestamp)> AssistantCache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Looks for an assistant and returns an ID if found, otherwise null
        /// </summary>
        /// <param name="assistantResourceName">The name of the embedded resource </param>
        /// <param name="azureOpenAiConfig"></param>
        /// <param name="autoCreate">Whether to automatically create the assistant if it doesn't exist.</param>
        /// <returns></returns>
        public static async Task<string?> GetAssistantId(string assistantResourceName, AzureOpenAiConfig? azureOpenAiConfig, bool autoCreate)
        {
            var assistantDefinition = await GetAssistantCreateRequest(assistantResourceName);
            var assistantName = assistantDefinition?.Name ?? assistantResourceName;

            var client = GetOpenAiClient(azureOpenAiConfig);

            var cacheKey = "allAssistants";
            var cachedData = AssistantCache.GetValueOrDefault(cacheKey);
            var allAssistants = cachedData.Assistants;
            var cacheTimestamp = cachedData.Timestamp;

            if (DateTime.UtcNow - cacheTimestamp > CacheDuration)
            {
                allAssistants = new List<AssistantResponse>();
                var hasMore = true;
                var lastId = string.Empty;
                while (hasMore)
                {
                    var result = await client.AssistantList(new() { After = lastId, Limit = 25 });
                    if (result.Successful)
                    {
                        if (result.Data != null)
                        {
                            allAssistants.AddRange(result.Data);
                        }

                        hasMore = result.HasMore;
                        lastId = result.LastId;
                    }
                    else
                    {
                        throw new Exception($"Failed to get assistantsList - {result.Error?.Code}: {result.Error?.Message}");
                    }
                }

                AssistantCache[cacheKey] = (allAssistants, DateTime.UtcNow);
            }

            var assistant = allAssistants.FirstOrDefault(o => o.Name == assistantName);
            if (assistantDefinition != null && assistant == null)
            {
                assistantDefinition.Model ??= azureOpenAiConfig?.DeploymentId ?? string.Empty;
            }
            if (assistant == null && assistantDefinition == null)
            {
                throw new InvalidOperationException($"Assistant {assistantName} does not exist and no definition was found");
            }

            if (assistant == null && autoCreate) return await Create(assistantDefinition!, azureOpenAiConfig);

            return assistant?.Id;
        }

        /// <summary>
        /// Creates an assistant from a stored definition
        /// </summary>
        /// <param name="assistantName"></param>
        /// <param name="azureOpenAiConfig"></param>
        /// <returns></returns>
        public static async Task<string> Create(string assistantName, AzureOpenAiConfig? azureOpenAiConfig)
        {
            var assistantId = await GetAssistantId(assistantName, azureOpenAiConfig, false);

            if (string.IsNullOrWhiteSpace(assistantId))
            {
                var assistantDefinition = await GetAssistantCreateRequest(assistantName);
                if (assistantDefinition == null)
                {
                    throw new Exception($"{assistantName} does not exist and no definition was found. Can't continue.");
                }

                if (assistantDefinition.ToolResources?.FileSearch?.VectorStoreIds?.Count > 0)
                {
                    var vectorStoreIds = new Dictionary<string, string>();

                    Trace.TraceInformation($"Ensuring vector store files.");
                    foreach (var vectorStoreName in assistantDefinition!.ToolResources.FileSearch.VectorStoreIds!)
                    {
                        var vectorStoreId = await VectorStore.EnsureVectorStore(assistantDefinition, vectorStoreName, azureOpenAiConfig);
                        vectorStoreIds[vectorStoreName] = vectorStoreId;
                        await VectorStore.CreateVectorFiles(assistantDefinition, vectorStoreName, vectorStoreId, azureOpenAiConfig);
                    }

                    // Replace the file names in the assistant definition with the file ids
                    for (int i = 0; i < assistantDefinition.ToolResources.FileSearch.VectorStoreIds.Count; i++)
                    {
                        var storeName = assistantDefinition.ToolResources.FileSearch.VectorStoreIds[i];
                        if (vectorStoreIds[storeName] == null)
                        {
                            throw new Exception($"No ID for {storeName}");
                        }
                        assistantDefinition.ToolResources.FileSearch.VectorStoreIds[i] = vectorStoreIds[storeName]!;
                    }
                }

                if (assistantDefinition.ToolResources?.CodeInterpreter?.FileIds is { Count: > 0 })
                {
                    assistantDefinition.ToolResources.CodeInterpreter.FileIds = await CodeInterpreterFiles.CreateCodeInterpreterFiles(assistantDefinition!, azureOpenAiConfig);
                }

                assistantId = await Create(assistantDefinition!, azureOpenAiConfig);
                Trace.TraceInformation($"Created assistant {assistantName}. Returning {assistantId}");
            }
            else
            {
                Trace.TraceInformation($"Found assistant ID {assistantId} for: {assistantName}. No action taken.");
            }

            return assistantId;
        }

        /// <summary>
        /// Creates an assistant
        /// </summary>
        /// <param name="assistantCreateRequest"></param>
        /// <param name="azureOpenAiConfig"></param>
        /// <returns></returns>
        public static async Task<string> Create(AssistantCreateRequest assistantCreateRequest, AzureOpenAiConfig? azureOpenAiConfig)
        {
            var client = GetOpenAiClient(azureOpenAiConfig);

            try
            {
                var newAssistant = await client.AssistantCreate(assistantCreateRequest, assistantCreateRequest.Model);

                if (newAssistant.Error != null)
                {
                    throw new Exception(newAssistant.Error.Message);
                }

                return newAssistant.Id!;
            }
            catch(Exception ex)
            {
                throw new Exception($"Unable to create assistant. {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the assistant definition from an embedded resource or storage
        /// </summary>
        /// <param name="assistantName"></param>
        /// <returns></returns>
        public static async Task<AssistantCreateRequest?> GetAssistantCreateRequest(string assistantName)
        {
            // Attempt to retrieve the assistant options from the cache.
            if (!AssistantDefinitionCache.TryGetValue(assistantName, out var cachedOptions))
            {
                // If not in cache, load it from resources or blob storage
                var json = await GetManifest(assistantName);

                // If no data is found, return null
                if (json == null) return null;

                var options = JsonSerializer.Deserialize<AssistantCreateRequest>(json);

                if (options != null)
                {
                    options.Name = assistantName;
                    var instructions = await GetInstructions(assistantName);
                    if (instructions != null)
                    {
                        options.Instructions = instructions;
                    }
                    await AddFunctionTools(assistantName, options);
                    // Add to cache or update the existing cached value. 
                    // This method will add the value if the key isn't present, or update it if it is present.
                    AssistantDefinitionCache.AddOrUpdate(assistantName, options, (key, oldValue) => options);
                }

                return options;
            }

            // Return the cached options
            return cachedOptions;
        }

        /// <summary>
        /// Lists the assistants in the OpenAI deployment
        /// </summary>
        /// <param name="azureOpenAiConfig"></param>
        /// <returns></returns>
        public static async Task<List<AssistantResponse>?> ListAssistants(AzureOpenAiConfig? azureOpenAiConfig)
        {
            var client = GetOpenAiClient(azureOpenAiConfig);
            var resp = await client.AssistantList();
            if (resp.Error == null && resp.Data != null)
            {
                return resp.Data;
            }
            return null;
        }

        /// <summary>
        /// Delete an assistant by name
        /// </summary>
        /// <param name="assistantName"></param>
        /// <param name="azureOpenAiConfig"></param>
        /// <returns></returns>
        public static async Task DeleteAssistant(string assistantName, AzureOpenAiConfig? azureOpenAiConfig)
        {
            var client = GetOpenAiClient(azureOpenAiConfig);
            var assistant = (await client.AssistantList())?.Data?.FirstOrDefault(o => o.Name == assistantName);
            if (assistant != null && !string.IsNullOrWhiteSpace(assistant.Id)) await client.AssistantDelete(assistant.Id);
            else throw new Exception($"{assistantName} not found.");
        }

        private static async Task AddFunctionTools(string assistantName, AssistantCreateRequest options)
        {
            var openApiSchemaFiles = await GetFilesInOpenApiFolder(assistantName);
            if (openApiSchemaFiles == null || !openApiSchemaFiles.Any()) return;

            var toolDefinitions = await OpenApiHelper.GetToolDefinitionsFromOpenApiSchemaFiles(openApiSchemaFiles);

            foreach (var toolDefinition in toolDefinitions)
            {
                options.Tools!.Add(toolDefinition);
            }
        }
    }
}
