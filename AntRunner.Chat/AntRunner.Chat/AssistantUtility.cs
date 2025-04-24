using System.Collections.Concurrent;
using AntRunner.ToolCalling.Functions;
using static AntRunner.ToolCalling.AssistantDefinitions.Storage.AssistantDefinitionFiles;
using AntRunner.ToolCalling.AssistantDefinitions;

namespace AntRunner.Chat
{
    /// <summary>
    /// Fetch and autoCreate assistants
    /// </summary>
    public static class AssistantUtility
    {
        // The ConcurrentDictionary to act as our in-memory cache
        private static readonly ConcurrentDictionary<string, AssistantDefinition?> AssistantDefinitionCache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Reads the assistant definition from an embedded resource or storage
        /// </summary>
        /// <param name="assistantName"></param>
        /// <returns></returns>
        public static async Task<AssistantDefinition?> GetAssistantCreateRequest(string assistantName)
        {
            // Attempt to retrieve the assistant options from the cache.
            if (!AssistantDefinitionCache.TryGetValue(assistantName, out var cachedOptions))
            {
                // If not in cache, load it from resources or blob storage
                var json = await GetManifest(assistantName);

                // If no data is found, return null
                if (json == null) return null;

                var options = JsonSerializer.Deserialize<AssistantDefinition>(json);

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

        private static async Task AddFunctionTools(string assistantName, AssistantDefinition options)
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
