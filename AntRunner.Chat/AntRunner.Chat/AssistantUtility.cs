using System;
using System.Collections.Concurrent;
using AntRunner.ToolCalling.Functions;
using System.Linq;
using static AntRunner.ToolCalling.AssistantDefinitions.Storage.AssistantDefinitionFiles;
using AntRunner.ToolCalling.AssistantDefinitions;
using AntRunner.ToolCalling.AssistantDefinitions.Storage;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using AntRunner.ToolCalling;

namespace AntRunner.Chat
{
    /// <summary>
    /// Fetch and autoCreate assistants with caching support.
    ///
    /// Loading Priority:
    /// 1. Template-based Guides (NotebookTemplates/)
    /// 2. File-based assistants (AssistantDefinitions/)
    /// </summary>
    public static class AssistantUtility
    {
        /// <summary>
        /// Cache entry containing the definition and timestamp.
        /// </summary>
        private record CachedAssistant(
            AssistantDefinition? Definition,
            DateTime CachedAt
        );

        // Simple cache keyed by assistant name
        private static readonly ConcurrentDictionary<string, CachedAssistant> AssistantDefinitionCache = new();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Reads the assistant definition from templates or file storage.
        /// </summary>
        /// <param name="assistantName">The name of the assistant</param>
        /// <returns>AssistantDefinition if found, null otherwise</returns>
        public static async Task<AssistantDefinition?> GetAssistantCreateRequest(string assistantName)
        {
            // Check cache with simple time-based expiration
            if (AssistantDefinitionCache.TryGetValue(assistantName, out var cached))
            {
                if (DateTime.UtcNow - cached.CachedAt <= CacheDuration)
                {
                    return cached.Definition;
                }
                // Cache expired, remove it
                AssistantDefinitionCache.TryRemove(assistantName, out _);
            }

            // Load assistant definition
            AssistantDefinition? definition = null;

            // Handle dynamic notebook template Guide assistants, e.g. "Creative Guide" or "Creative Guide Guide"
            if (assistantName.EndsWith(" Guide", StringComparison.OrdinalIgnoreCase))
            {
                var guide = await TryBuildGuideFromTemplate(assistantName);
                if (guide != null)
                {
                    definition = guide;
                }
            }

            // If not a guide template, load from storage (file-based)
            if (definition == null)
            {
                var storageMetadata = await GetAssistantComplete(assistantName);
                
                if (storageMetadata == null) return null;

                // Deserialize manifest with lenient options that allow integer enum values
                var lenientOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    Converters = { new JsonStringEnumConverter(allowIntegerValues: true) }
                };
                definition = JsonSerializer.Deserialize<AssistantDefinition>(storageMetadata.ManifestJson, lenientOptions);
                
                if (definition != null)
                {
                    definition.Name = assistantName;
                    definition.Id = storageMetadata.Id;  // Set database ID if available
                    
                    // Set instructions
                    if (!string.IsNullOrWhiteSpace(storageMetadata.Instructions))
                    {
                        definition.Instructions = storageMetadata.Instructions;
                    }
                    
                    // Set context options
                    if (!string.IsNullOrWhiteSpace(storageMetadata.ContextOptionsJson))
                    {
                        try
                        {
                            var contextList = JsonSerializer.Deserialize<List<ContextOptionItem>>(storageMetadata.ContextOptionsJson);
                            var contextDict = contextList?.ToDictionary(i => i.key, i => i.value ?? string.Empty);
                            if (contextDict != null && contextDict.Any())
                            {
                                definition.ContextOptions = contextDict;
                            }
                        }
                        catch (Exception)
                        {
                            // swallow parse errors for now, could log
                        }
                    }

                    // Apply additional metadata from database (e.g., crew names)
                    if (storageMetadata.AdditionalMetadata != null)
                    {
                        if (definition.Metadata == null)
                        {
                            definition.Metadata = new Dictionary<string, string>();
                        }
                        foreach (var kvp in storageMetadata.AdditionalMetadata)
                        {
                            definition.Metadata[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            if (definition == null) return null;

            // Add function tools (crew bridge, OpenAPI, vector stores, annotation-based)
            await AddFunctionTools(assistantName, definition);

            // Cache the definition
            var cachedEntry = new CachedAssistant(definition, DateTime.UtcNow);
            AssistantDefinitionCache.AddOrUpdate(assistantName, cachedEntry, (k, v) => cachedEntry);

            return definition;
        }

        /// <summary>
        /// Clears a specific assistant from the cache.
        /// </summary>
        /// <param name="assistantName">The name of the assistant to clear</param>
        public static void ClearCache(string assistantName)
        {
            AssistantDefinitionCache.TryRemove(assistantName, out _);
        }

        /// <summary>
        /// Clears all cached assistants.
        /// Useful for testing or when bulk updates are made.
        /// </summary>
        public static void ClearAllCache()
        {
            AssistantDefinitionCache.Clear();
        }

        private static async Task AddFunctionTools(string assistantName, AssistantDefinition options)
        {
            // Add crew-bridge tools for Guide assistants
            if (options.Metadata != null && options.Metadata.TryGetValue("__crew_names__", out var crewNamesStr))
            {
                var crewNames = crewNamesStr.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (crewNames.Any())
                {
                    var bridgeSchema = CrewBridgeSchemaGenerator.GetSchema(crewNames);
                    var bridgeDefs = OpenApiHelper.GetToolDefinitionsFromJson(bridgeSchema);
                    foreach (var td in bridgeDefs)
                    {
                        options.Tools!.Add(td);
                    }
                }
            }

            // Add OpenAPI-based function tools
            var openApiToolDefinitions = await GetOpenApiToolDefinitions(assistantName);
            foreach (var toolDefinition in openApiToolDefinitions)
            {
                options.Tools!.Add(toolDefinition);
            }

            // Add SearchAssistantFiles tool if assistant has indexed content (vector stores)
            // This tool is annotation-based and will be injected via ToolContractRegistry
            var hasVectorStores = options.ToolResources?.FileSearch?.VectorStoreIds != null 
                && options.ToolResources.FileSearch.VectorStoreIds.Any();
            
            if (hasVectorStores)
            {
                // Check if SearchAssistantFiles is registered in ToolContractRegistry
                var allToolOperations = ToolContractRegistry.GetAllToolOperations();
                var searchAssistantFilesOperation = allToolOperations.FirstOrDefault(kvp => 
                    kvp.Key.Equals("SearchAssistantFiles", StringComparison.OrdinalIgnoreCase));
                
                if (!string.IsNullOrEmpty(searchAssistantFilesOperation.Key))
                {
                    try
                    {
                        var schema = ToolContractRegistry.GenerateOpenApiSchema(searchAssistantFilesOperation.Value);
                        var toolDefinitions = OpenApiHelper.GetToolDefinitionsFromJson(schema);
                        
                        foreach (var def in toolDefinitions)
                        {
                            if (def.Function?.AsObject?.Name == "SearchAssistantFiles")
                            {
                                options.Tools!.Add(def);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log warning but continue - non-critical failure
                        Console.WriteLine($"Warning: Failed to inject SearchAssistantFiles tool: {ex.Message}");
                    }
                }
            }

            // ---------------------------------------------------------------------
            // Process dynamic schema placeholders using annotation-driven discovery
            // ---------------------------------------------------------------------
            var requestedPlaceholders = options.Tools?
                .Where(t => t.Type != null)
                .ToList();

            if (requestedPlaceholders?.Any() == true)
            {
                var allToolOperations = ToolContractRegistry.GetAllToolOperations();
                var processedSchemas = new HashSet<string>();
                
                foreach (var placeholder in requestedPlaceholders.Where(p => p.Type != null))
                {
                    // Find the tool operation that matches this placeholder type
                    var matchingOperation = allToolOperations.FirstOrDefault(kvp => 
                        string.Equals(kvp.Key, placeholder.Type, StringComparison.OrdinalIgnoreCase));
                    
                    if (!string.IsNullOrEmpty(matchingOperation.Key) && 
                        !processedSchemas.Contains(matchingOperation.Value))
                    {
                        try
                        {
                            var schema = ToolContractRegistry.GenerateOpenApiSchema(matchingOperation.Value);
                            var toolDefinitions = OpenApiHelper.GetToolDefinitionsFromJson(schema);
                            
                            foreach (var def in toolDefinitions)
                            {
                                if (def.Function?.AsObject?.Name == matchingOperation.Key)
                                {
                                    options.Tools!.Add(def);
                                }
                            }
                            
                            processedSchemas.Add(matchingOperation.Value);
                        }
                        catch (Exception ex)
                        {
                            // Log warning but continue processing other tools
                            Console.WriteLine($"Warning: Failed to generate schema for {matchingOperation.Value}: {ex.Message}");
                        }
                    }
                }

                // Remove all placeholder entries that were successfully processed
                options.Tools = options.Tools!
                    .Where(t => t.Type == null || !allToolOperations.ContainsKey(t.Type!))
                    .ToList();
            }
        }

        // -----------------------------
        // Helper methods
        // -----------------------------
        
        /// <summary>
        /// Gets OpenAPI tool definitions for an assistant from file system.
        /// </summary>
        private static async Task<List<ToolDefinition>> GetOpenApiToolDefinitions(string assistantName)
        {
            var toolDefinitions = new List<ToolDefinition>();

            // Load from file system
            var openApiSchemaFiles = await GetFilesInOpenApiFolder(assistantName);
            if (openApiSchemaFiles != null && openApiSchemaFiles.Any())
            {
                toolDefinitions = await OpenApiHelper.GetToolDefinitionsFromOpenApiSchemaFiles(openApiSchemaFiles);
            }

            return toolDefinitions;
        }
        
        // -----------------------------
        // Helper methods for Guide load
        // -----------------------------
        private static async Task<AssistantDefinition?> TryBuildGuideFromTemplate(string assistantName)
        {
            // File-based template storage
            var templatesRoot = ResolveTemplatesRoot();
            if (templatesRoot == null || !Directory.Exists(templatesRoot))
            {
                return null;
            }

            // Consider both the full assistantName (may already end with Guide) and stripped name
            static string StripGuide(string name) => name.EndsWith(" Guide", StringComparison.OrdinalIgnoreCase)
                ? name[..^6] : name;

            var candidates = new[] { assistantName, StripGuide(assistantName) };
            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var dir = Path.Combine(templatesRoot, candidate);
                if (!Directory.Exists(dir)) continue;

                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    throw new FileNotFoundException("Template manifest.json not found for guide", manifestPath);
                }

                var json = await File.ReadAllTextAsync(manifestPath, Encoding.UTF8);
                var manifest = JsonSerializer.Deserialize<TemplateManifest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (manifest == null)
                {
                    throw new InvalidDataException($"Failed to parse template manifest: {manifestPath}");
                }

                // Determine instructions: manifest.Instructions > instructions.md; otherwise fail fast
                string? instructions = manifest.Instructions;
                if (string.IsNullOrWhiteSpace(instructions))
                {
                    var instructionsPath = Path.Combine(dir, "instructions.md");
                    if (File.Exists(instructionsPath))
                    {
                        instructions = await File.ReadAllTextAsync(instructionsPath, Encoding.UTF8);
                    }
                }
                if (string.IsNullOrWhiteSpace(instructions))
                {
                    throw new InvalidOperationException($"Guide instructions not found. Provide 'instructions' in manifest or an instructions.md in: {dir}");
                }

                // Build assistant definition from template manifest properties
                var def = new AssistantDefinition
                {
                    Name = assistantName,
                    Description = manifest.Description ?? $"Your intelligent guide for {candidate} workflows",
                    Instructions = instructions,
                    InvocationEvaluator = manifest.InvocationEvaluator,
                    Tools = manifest.Tools ?? new List<ToolDefinition>(),
                    ToolResources = manifest.ToolResources,
                    TopP = manifest.TopP,
                    Metadata = manifest.Metadata,
                    Model = manifest.Model ?? manifest.DefaultModel ?? "gpt-4.1",
                    Temperature = manifest.Temperature,
                    ReasoningEffort = manifest.ReasoningEffort
                };

                // Load context options from template if they exist
                var contextOptionsPath = Path.Combine(dir, "HostExtensions", "UI", "contextOptions.json");
                if (File.Exists(contextOptionsPath))
                {
                    try
                    {
                        var contextJson = await File.ReadAllTextAsync(contextOptionsPath, Encoding.UTF8);
                        var contextList = JsonSerializer.Deserialize<List<ContextOptionItem>>(contextJson);
                        if (contextList != null && contextList.Any())
                        {
                            def.ContextOptions = contextList.ToDictionary(i => i.key, i => i.value ?? string.Empty);
                        }
                    }
                    catch (Exception)
                    {
                        // Swallow context options parsing errors - guide can still work without them
                    }
                }

                // Store crew names in metadata for processing in AddFunctionTools
                if (manifest.Crew != null && manifest.Crew.Count > 0)
                {
                    var crewNames = manifest.Crew.Select(c => c.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
                    if (crewNames.Count == 0)
                    {
                        throw new InvalidOperationException($"Template '{candidate}' has an empty crew list.");
                    }
                    // Store crew names in metadata so AddFunctionTools can process them
                    if (def.Metadata == null) def.Metadata = new Dictionary<string, string>();
                    def.Metadata["__crew_names__"] = string.Join(",", crewNames);
                }

                return def;
            }

            // Guide not found in file-based templates
            return null;
        }

        private static string? ResolveTemplatesRoot()
        {
            // 1) Environment variable overrides
            // Primary explicit var used across the server
            var envVar = Environment.GetEnvironmentVariable("NOTEBOOK_TEMPLATES_BASE_FOLDER_PATH");
            if (!string.IsNullOrWhiteSpace(envVar))
            {
                var full = Path.GetFullPath(envVar, AppContext.BaseDirectory);
                if (Directory.Exists(full)) return full;
                if (Directory.Exists(envVar)) return envVar; // already absolute
            }

            // Server publishes configuration keys into environment variables at startup
            // so we can also read the config key directly
            var configKeyVar = Environment.GetEnvironmentVariable("NotebookTemplates:BaseFolderPath");
            if (!string.IsNullOrWhiteSpace(configKeyVar))
            {
                var full = Path.GetFullPath(configKeyVar, AppContext.BaseDirectory);
                if (Directory.Exists(full)) return full;
                if (Directory.Exists(configKeyVar)) return configKeyVar;
            }

            // 2) Common relative locations from current base directory
            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "NotebookTemplates"),
                Path.GetFullPath(Path.Combine(baseDir, "..", "NotebookTemplates")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "NotebookTemplates")),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "NotebookTemplates"))
            };
            foreach (var c in candidates)
            {
                if (Directory.Exists(c)) return c;
            }

            return null;
        }

        // Minimal manifest backing type (subset plus assistant-like fields)
        private class TemplateManifest
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
            public string? DefaultAssistant { get; set; }
            public string? Instructions { get; set; }
            public string? InvocationEvaluator { get; set; }
            public List<ToolDefinition>? Tools { get; set; }
            public ToolResources? ToolResources { get; set; }
            public double? TopP { get; set; }
            public Dictionary<string, string>? Metadata { get; set; }
            public string? Model { get; set; }
            public string? DefaultModel { get; set; }
            public float? Temperature { get; set; }
            public ReasoningEffort? ReasoningEffort { get; set; }
            public List<TemplateCrewMember> Crew { get; set; } = new();
        }

        private class TemplateCrewMember
        {
            public string Name { get; set; } = string.Empty;
        }

        private record ContextOptionItem(string key, string? value);
    }
}
