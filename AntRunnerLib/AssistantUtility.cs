using FunctionCalling;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using OpenAI.ObjectModels.SharedModels;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using static AntRunnerLib.AssistantDefinitions.AssistantDefinitionFiles;
using static AntRunnerLib.ClientUtility;

namespace AntRunnerLib
{
    /// <summary>
    /// Fetch and create assistants
    /// </summary>
    public static class AssistantUtility
    {
        // The ConcurrentDictionary to act as our in-memory cache
        private static readonly ConcurrentDictionary<string, AssistantCreateRequest?> _cache = new();

        /// <summary>
        /// Looks for an assistant and returns an Id if found, otherwise null
        /// </summary>
        /// <param name="assistantResourceName">The name of the embedded resource </param>
        /// <param name="azureOpenAIConfig"></param>
        /// <returns></returns>
        public static async Task<string?> GetAssistantId(string assistantResourceName, AzureOpenAIConfig? azureOpenAIConfig)
        {
            // I am on the fence about this design, but the intention is to allow invocation of an assitant if it exists in the endpoint
            // even if the definition is not stored anywhere used by the orchestrator
            var assistantDefinition = await GetAssistantCreateRequest(assistantResourceName);
            var assistantName = assistantDefinition?.Name ?? assistantResourceName;
            var client = GetOpenAIClient(azureOpenAIConfig);

            var allAssistants = new List<AssistantResponse>();
            var hasMore = true;
            var lastId = string.Empty;
            while (hasMore)
            {
                var result = await client.AssistantList(new() { After = lastId, Limit = 5 });
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

            var assistant = allAssistants.FirstOrDefault(o => o.Name == assistantName);

            if (assistant == null && assistantDefinition == null)
            {
                throw new InvalidOperationException($"Assistant {assistantName} does not exist and no definition was found");
            }

            return assistant?.Id;
        }

        /// <summary>
        /// Creates an assistant from a stored definition
        /// </summary>
        /// <param name="assistantName"></param>
        /// <param name="azureOpenAIConfig"></param>
        /// <returns></returns>
        public static async Task<string> Create(string assistantName, AzureOpenAIConfig? azureOpenAIConfig)
        {
            var assistantId = await GetAssistantId(assistantName, azureOpenAIConfig);

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
                        var vectorStoreId = await VectorStore.EnsureVectorStore(assistantDefinition, vectorStoreName, azureOpenAIConfig);
                        vectorStoreIds[vectorStoreName] = vectorStoreId;
                        await VectorStore.CreateVectorFiles(assistantDefinition, vectorStoreName, vectorStoreId, azureOpenAIConfig);
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

                if (assistantDefinition.ToolResources?.CodeInterpreter?.FileIds != null && assistantDefinition.ToolResources?.CodeInterpreter?.FileIds.Count > 0)
                {
                    assistantDefinition.ToolResources.CodeInterpreter.FileIds = await CodeInterpreterFiles.CreateCodeInterpreterFiles(assistantDefinition!, azureOpenAIConfig);
                }

                assistantId = await Create(assistantDefinition!, azureOpenAIConfig);
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
        /// <param name="azureOpenAIConfig"></param>
        /// <returns></returns>
        public static async Task<string> Create(AssistantCreateRequest assistantCreateRequest, AzureOpenAIConfig? azureOpenAIConfig)
        {
            var client = GetOpenAIClient(azureOpenAIConfig);

            AssistantResponse newAssistant;
            try
            {
                newAssistant = await client.AssistantCreate(assistantCreateRequest, assistantCreateRequest.Model);

                if (newAssistant.Error != null)
                {
                    throw new Exception(newAssistant.Error.ToString());
                }

                return newAssistant.Id;
            }
            catch
            {
                throw new Exception("Unable to create assistant. Model not found and default model is missing or not set in config.");
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
            if (!_cache.TryGetValue(assistantName, out var cachedOptions))
            {
                // If not in cache, load it from resources or blob storage
                var json = await GetManifest(assistantName);

                // If no data is found, return null
                if (json == null) return null;

                var options = JsonSerializer.Deserialize<AssistantCreateRequest>(json);

                if (options != null)
                {

                    var instructions = await GetInstructions(assistantName);
                    if (instructions != null)
                    {
                        options.Instructions = instructions;
                    }
                    await AddFunctionTools(assistantName, options);
                    // Add to cache or update the existing cached value. 
                    // This method will add the value if the key isn't present, or update it if it is present.
                    _cache.AddOrUpdate(assistantName, options, (key, oldValue) => options);
                }

                return options;
            }

            // Return the cached options
            return cachedOptions;
        }

        /// <summary>
        /// Lists the assistants in the OpenAI deployment
        /// </summary>
        /// <param name="azureOpenAIConfig"></param>
        /// <returns></returns>
        public static async Task<List<AssistantResponse>?> ListAssistants(AzureOpenAIConfig? azureOpenAIConfig)
        {
            var client = GetOpenAIClient(azureOpenAIConfig);
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
        /// <returns></returns>
        public static async Task DeleteAssistant(string assistantName, AzureOpenAIConfig? azureOpenAIConfig)
        {
            var client = GetOpenAIClient(azureOpenAIConfig);
            var assistants = await client.AssistantList();
            var assistant = (await client.AssistantList())?.Data?.FirstOrDefault(o => o.Name == assistantName);
            if (assistant != null) await client.AssistantDelete(assistant.Id);
            else throw new Exception($"{assistantName} not found.");
        }

        private static async Task AddFunctionTools(string assistantName, AssistantCreateRequest options)
        {
            var openApiSchemaFiles = await GetFilesInOpenAPIFolder(assistantName);
            if (openApiSchemaFiles == null || !openApiSchemaFiles.Any()) return;

            foreach (var openApiSchemaFile in openApiSchemaFiles)
            {
                var schema = await GetFile(openApiSchemaFile);
                if (schema == null)
                {
                    Trace.TraceWarning("openApiSchemaFile {openApiSchemaFile} is null. Ignoring", openApiSchemaFile);
                    continue;
                }
                var json = Encoding.Default.GetString(schema);
                var openApiHelper = new OpenApiHelper();

                var validationResult = openApiHelper.ValidateAndParseOpenAPISpec(json);
                var spec = validationResult.Spec;

                if (!validationResult.Status || spec == null)
                {
                    Trace.TraceWarning("Json is not a valid openapi spec {json}. Ignoring", json);
                    continue;
                }

                var toolDefinitions = openApiHelper.GetToolDefinitions(spec);

                foreach (var toolDefinition in toolDefinitions)
                {
                    options.Tools!.Add(toolDefinition);
                }
            }
        }
    }
}
