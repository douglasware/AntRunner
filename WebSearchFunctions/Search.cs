using System.Diagnostics;
using AntRunnerLib.Functions;
using System.Text.Json;
using AntRunnerLib;
using HtmlAgility;

namespace WebSearchFunctions
{
    public class SearchTool
    {
        public record PageContent
        {
            public string PageTitle { get; set; } = "";
            public string Url { get; set; } = "";
            public string CachedPageUrl { get; set; } = "";
            public string PageMarkdown { get; set; } = "";
            public string Extract { get; set; } = "";
        }

        private static readonly string SearchApi = "{\"openapi\": \"3.0.0\",\"info\": {\"title\": \"Bing Web Search API\",\"version\": \"v7.0\",\"description\": \"The Bing Web Search API returns web search results for a given query.\"},\"servers\": [{\"url\": \"https://api.bing.microsoft.com\",\"description\": \"Bing Web Search API server\"}],\"paths\": {\"/v7.0/search\": {\"get\": {\"summary\": \"Search the web\",\"operationId\": \"crawl\",\"parameters\": [{\"name\": \"q\",\"in\": \"query\",\"required\": true,\"schema\": {\"type\": \"string\"},\"description\": \"The user's search query string.\"},{\"name\": \"answerCount\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"integer\"},\"description\": \"The number of answers that you want the response to include.\"},{\"name\": \"cc\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"A 2-character country code of the country where the results come from.\"},{\"name\": \"count\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"integer\",\"default\": 10,\"maximum\": 50},\"description\": \"The number of search results to return in the response.\"},{\"name\": \"freshness\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"Filter search results by their age.\"},{\"name\": \"mkt\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"The market where the results come from.\"},{\"name\": \"offset\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"integer\",\"default\": 0},\"description\": \"The zero-based offset that indicates the number of search results to skip before returning results.\"},{\"name\": \"promote\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"A comma-delimited list of answers that you want the response to include regardless of their ranking.\"},{\"name\": \"responseFilter\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"A comma-delimited list of answers to include in the response.\"},{\"name\": \"safeSearch\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\",\"enum\": [\"Off\",\"Moderate\",\"Strict\"],\"default\": \"Moderate\"},\"description\": \"Used to filter webpages, images, and videos for adult content.\"},{\"name\": \"setLang\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"The language to use for user interface strings.\"},{\"name\": \"textDecorations\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"boolean\",\"default\": false},\"description\": \"A Boolean value that determines whether display strings in the results should contain decoration markers such as hit highlighting characters.\"},{\"name\": \"textFormat\",\"in\": \"query\",\"required\": true,\"schema\": {\"type\": \"string\",\"enum\": [\"Raw\"],\"default\": \"Raw\"},\"description\": \"Required\"}],\"responses\": {\"200\": {\"description\": \"Successful response\",\"content\": {\"application/json\": {\"schema\": {\"type\": \"object\",\"properties\": {\"queryContext\": {\"type\": \"object\",\"properties\": {\"originalQuery\": {\"type\": \"string\"}}},\"webPages\": {\"type\": \"object\",\"properties\": {\"value\": {\"type\": \"array\",\"items\": {\"type\": \"object\",\"properties\": {\"name\": {\"type\": \"string\"},\"url\": {\"type\": \"string\"},\"cachedPageUrl\": {\"type\": \"string\"}},\"required\": [\"name\",\"url\",\"cachedPageUrl\"]}}}},\"images\": {\"type\": \"object\",\"properties\": {\"value\": {\"type\": \"array\",\"items\": {\"type\": \"object\",\"properties\": {\"thumbnailUrl\": {\"type\": \"string\"},\"contentUrl\": {\"type\": \"string\"},\"hostPageUrl\": {\"type\": \"string\"}},\"required\": [\"thumbnailUrl\",\"contentUrl\",\"hostPageUrl\"]}}}}}}}}}}}}}}";

        private static readonly string SearchAuth = "{\"hosts\":{\"api.bing.microsoft.com\":{\"auth_type\":\"service_http\",\"header_name\":\"Ocp-Apim-Subscription-Key\",\"header_value_env_var\":\"SEARCH_API_KEY\"}}}";

        public static async Task<string> Search(string searchQuery, string userRequest)
        {
            var searchFunctionAuth = JsonSerializer.Deserialize<DomainAuth>(SearchAuth);
            var searchFunctionSpec = OpenApiHelper.ValidateAndParseOpenApiSpec(SearchApi).Spec;
            var searchTools = OpenApiHelper.GetToolDefinitionsFromSchema(searchFunctionSpec!);
            var searchToolCallers = ToolCallers.GetToolCallers(searchFunctionSpec ?? throw new InvalidOperationException(), searchTools, searchFunctionAuth);

            var crawlTool = searchToolCallers["crawl"];

            crawlTool.Params = new()
    {
        {"q", searchQuery},
        {"count", 20},
        {"textFormat", "RAW"}
    };

            var response = await crawlTool.ExecuteWebApiAsync();
            var responseContent = await response.Content.ReadAsStringAsync();

            List<PageContent> searchResults = new();

            if (crawlTool.ResponseSchemas.TryGetValue("200", out var schemaJson))
            {
                try
                {
                    var contentJson = JsonDocument.Parse(responseContent).RootElement;

                    var filteredJson = ThreadUtility.FilterJsonBySchema(contentJson, schemaJson);
                    var pages = filteredJson.GetProperty("webPages");
                    var pagesValue = pages.GetProperty("value");

                    foreach (var page in pagesValue.EnumerateArray())
                    {
                        var name = page.GetProperty("name").GetString();
                        var url = page.GetProperty("url").GetString();

                        // Ignore pages without a url and cachedPageUrl.
                        if (url != null && page.TryGetProperty("cachedPageUrl", out JsonElement cachedPageUrl))
                        {
                            searchResults.Add(new()
                            {
                                CachedPageUrl = cachedPageUrl.GetString() ?? url,
                                PageTitle = name!,
                                Url = url
                            });
                        }
                    }
                }
                catch
                {
                    // ignored
                }
            }
           
            var tasks = searchResults.Select(async searchResult =>
            {
                try
                {
                    var markdown = await HtmlAgilityPackExtensions.ConvertUrlToMarkdownAsync(searchResult.CachedPageUrl);
                    if (markdown.Trim().Split(' ').Length > 10)
                    {
                        searchResult.PageMarkdown = markdown;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }).ToList();

            await Task.WhenAll(tasks);
            
            var random = new Random();

            var markdownResults = searchResults
                .Where(result => !string.IsNullOrEmpty(result.PageMarkdown))
                .OrderBy(_ => random.Next())
                .Take(10)
                .ToList();

            var selectedFacts = new List<PageContent>();

            int batches = 2;
            int batchSize = 5;
            var start = DateTime.Now;
            
            for (int loop = 0; loop < batches; loop++)
            {
                var pagesToEvaluate = markdownResults.Skip(loop * batchSize).Take(batchSize).ToList();

                var evaluationTasks = pagesToEvaluate.Select(async page =>
                {
                    var extractedContent = await AssistantRunner.RunThread("PageContentExtractor", $"Question:{userRequest}\n```Content:{page.PageMarkdown}");
                    if (!extractedContent.ToLowerInvariant().Contains("not found") && extractedContent.Split(' ').Length > 10)
                    {
                        page.Extract = extractedContent;
                        selectedFacts.Add(page);
                    }
                }).ToList();

                await Task.WhenAll(evaluationTasks);
                Trace.TraceInformation($"Batch elapsed time {(DateTime.Now - start).TotalMilliseconds}");
            }

            var output = string.Join("\n\n", selectedFacts.Select(fact => $"# [{fact.PageTitle}]({fact.Url})\n\n{fact.Extract}"));

            Trace.TraceInformation($"Total elapsed time {(DateTime.Now - start).TotalMilliseconds}");

            return output;
        }
    }
}