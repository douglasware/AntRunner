using System.Diagnostics;
using AntRunnerLib.Functions;
using System.Text.Json;
using AntRunnerLib;
using HtmlAgility;
using System.Text.RegularExpressions;

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

        private static readonly string SearchApi =
            "{\"openapi\": \"3.0.0\",\"info\": {\"title\": \"Bing Web Search API\",\"version\": \"v7.0\",\"description\": \"The Bing Web Search API returns web search results for a given query.\"},\"servers\": [{\"url\": \"https://api.bing.microsoft.com\",\"description\": \"Bing Web Search API server\"}],\"paths\": {\"/v7.0/search\": {\"get\": {\"summary\": \"Search the web\",\"operationId\": \"crawl\",\"parameters\": [{\"name\": \"q\",\"in\": \"query\",\"required\": true,\"schema\": {\"type\": \"string\"},\"description\": \"The user's search query string.\"},{\"name\": \"answerCount\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"integer\"},\"description\": \"The number of answers that you want the response to include.\"},{\"name\": \"cc\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"A 2-character country code of the country where the results come from.\"},{\"name\": \"count\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"integer\",\"default\": 10,\"maximum\": 50},\"description\": \"The number of search results to return in the response.\"},{\"name\": \"freshness\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"Filter search results by their age.\"},{\"name\": \"mkt\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"The market where the results come from.\"},{\"name\": \"offset\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"integer\",\"default\": 0},\"description\": \"The zero-based offset that indicates the number of search results to skip before returning results.\"},{\"name\": \"promote\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"A comma-delimited list of answers that you want the response to include regardless of their ranking.\"},{\"name\": \"responseFilter\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"A comma-delimited list of answers to include in the response.\"},{\"name\": \"safeSearch\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\",\"enum\": [\"Off\",\"Moderate\",\"Strict\"],\"default\": \"Moderate\"},\"description\": \"Used to filter webpages, images, and videos for adult content.\"},{\"name\": \"setLang\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"string\"},\"description\": \"The language to use for user interface strings.\"},{\"name\": \"textDecorations\",\"in\": \"query\",\"required\": false,\"schema\": {\"type\": \"boolean\",\"default\": false},\"description\": \"A Boolean value that determines whether display strings in the results should contain decoration markers such as hit highlighting characters.\"},{\"name\": \"textFormat\",\"in\": \"query\",\"required\": true,\"schema\": {\"type\": \"string\",\"enum\": [\"Raw\"],\"default\": \"Raw\"},\"description\": \"Required\"}],\"responses\": {\"200\": {\"description\": \"Successful response\",\"content\": {\"application/json\": {\"schema\": {\"type\": \"object\",\"properties\": {\"queryContext\": {\"type\": \"object\",\"properties\": {\"originalQuery\": {\"type\": \"string\"}}},\"webPages\": {\"type\": \"object\",\"properties\": {\"value\": {\"type\": \"array\",\"items\": {\"type\": \"object\",\"properties\": {\"name\": {\"type\": \"string\"},\"url\": {\"type\": \"string\"},\"cachedPageUrl\": {\"type\": \"string\"}},\"required\": [\"name\",\"url\",\"cachedPageUrl\"]}}}},\"images\": {\"type\": \"object\",\"properties\": {\"value\": {\"type\": \"array\",\"items\": {\"type\": \"object\",\"properties\": {\"thumbnailUrl\": {\"type\": \"string\"},\"contentUrl\": {\"type\": \"string\"},\"hostPageUrl\": {\"type\": \"string\"}},\"required\": [\"thumbnailUrl\",\"contentUrl\",\"hostPageUrl\"]}}}}}}}}}}}}}}";

        private static readonly string SearchAuth =
            "{\"hosts\":{\"api.bing.microsoft.com\":{\"auth_type\":\"service_http\",\"header_name\":\"Ocp-Apim-Subscription-Key\",\"header_value_env_var\":\"SEARCH_API_KEY\"}}}";

        public static async Task<string> Search(string searchQuery, string userRequest)
        {
            var searchFunctionAuth = JsonSerializer.Deserialize<DomainAuth>(SearchAuth);
            var searchFunctionSpec = OpenApiHelper.ValidateAndParseOpenApiSpec(SearchApi).Spec;
            var searchTools = OpenApiHelper.GetToolDefinitionsFromSchema(searchFunctionSpec!);
            var searchToolCallers =
                ToolCallers.GetToolCallers(searchFunctionSpec ?? throw new InvalidOperationException(), searchTools,
                    searchFunctionAuth);

            var crawlTool = searchToolCallers["crawl"];

            crawlTool.Params = new()
            {
                { "q", searchQuery },
                { "count", 20 },
                { "textFormat", "RAW" }
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
                        var cachedPageUrl = url;

                        if (page.TryGetProperty("cachedPageUrl", out JsonElement cachedPageUrlElement))
                        {
                            cachedPageUrl = cachedPageUrlElement.GetString();
                        }

                        if (url != null)
                        {
                            // Made this change because the web search inexplicably sometimes returns full results with no cached page URL in any of the items
                            searchResults.Add(new()
                            {
                                CachedPageUrl = !string.IsNullOrWhiteSpace(cachedPageUrl) ? cachedPageUrl : url,
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
                    Trace.TraceInformation($"{nameof(Search)}|Scraping markdown from {searchResult.CachedPageUrl}");
                    var markdown =
                        await HtmlAgilityPackExtensions.ConvertUrlToMarkdownAsync(searchResult.CachedPageUrl);
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

            //Filter out results without markdown
            var markdownResults = searchResults
                .Where(result => !string.IsNullOrEmpty(result.PageMarkdown))
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
                    var extractContentThreadRunOutput = await AssistantRunner.RunThread(new AssistantRunOptions()
                    {
                        AssistantName = "PageContentExtractor",
                        Instructions = $"Question:{userRequest}\n```Content:{page.PageMarkdown}"
                    }, AzureOpenAiConfigFactory.Get());

                    if (extractContentThreadRunOutput == null) return;

                    var extractedContent = SearchPostProcessor(extractContentThreadRunOutput).LastMessage;

                    if (!extractedContent.ToLowerInvariant().Contains("not found") &&
                        extractedContent.Split(' ').Length > 10)
                    {
                        page.Extract = extractedContent;
                        selectedFacts.Add(page);
                    }
                    else
                    {
                        Trace.TraceInformation(extractedContent);
                    }
                }).ToList();

                await Task.WhenAll(evaluationTasks);
                Trace.TraceInformation(
                    $"{nameof(Search)}|Batch elapsed time {(DateTime.Now - start).TotalMilliseconds}");
            }

            // Shuffle the selected facts and take 5
            selectedFacts = selectedFacts.OrderBy(_ => random.Next()).Take(5).ToList();

            var output = string.Join("\n\n",
                selectedFacts.Select(fact => $"# [{fact.PageTitle}]({fact.Url})\n\n{fact.Extract}"));

            Trace.TraceInformation($"{nameof(Search)}|Total elapsed time {(DateTime.Now - start).TotalMilliseconds}");

            return output;
        }

        public static ThreadRunOutput SearchPostProcessor(ThreadRunOutput threadRunOutput)
        {
            var dialogWithoutLastMessage = threadRunOutput.Dialog.Replace(threadRunOutput.LastMessage, "");

            var urlsToValidate = ExtractWebAddresses(threadRunOutput.LastMessage);
            foreach (var urlToValidate in urlsToValidate)
            {
                if (!dialogWithoutLastMessage.Contains(urlToValidate))
                {
                    var linkText = ExtractLinkText(threadRunOutput.LastMessage, urlToValidate);
                    if (!string.IsNullOrEmpty(linkText))
                    {
                        var correctUrl = FindUrlByLinkText(dialogWithoutLastMessage, linkText);
                        if (!string.IsNullOrEmpty(correctUrl) && !urlToValidate.Contains(correctUrl))
                        {
                            Trace.TraceInformation(
                                $"{nameof(SearchPostProcessor)}|{urlToValidate} is a hallucination. Changing to {correctUrl} based on Dialog");
                            threadRunOutput.LastMessage =
                                threadRunOutput.LastMessage.Replace(urlToValidate, correctUrl);
                        }
                    }
                    else
                    {
                        Trace.TraceWarning(
                            $"{nameof(SearchPostProcessor)}|{urlToValidate} is a hallucination and unable to fix");

                        // Hack to try... 
                        threadRunOutput.LastMessage = "not found";
                        break;
                    }
                }
            }

            // Handle invalid image links
            threadRunOutput.LastMessage =
                RemoveInvalidImageLinks(threadRunOutput.LastMessage, dialogWithoutLastMessage);

            return threadRunOutput;
        }

        private static List<string> ExtractWebAddresses(string input)
        {
            var urls = new List<string>();
            var regex = new Regex(@"\[(.*?)\]\((.*?)\)");
            var matches = regex.Matches(input);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 2)
                {
                    urls.Add(match.Groups[2].Value);
                }
            }

            return urls;
        }

        private static string ExtractLinkText(string input, string url)
        {
            var regex = new Regex(@"\[(.*?)\]\(" + Regex.Escape(url) + @"\)");
            var match = regex.Match(input);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return "";
        }

        private static string FindUrlByLinkText(string input, string linkText)
        {
            var regex = new Regex(@"\[" + Regex.Escape(linkText) + @"\]\((.*?)\)");
            var match = regex.Match(input);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }

            return "";
        }

        private static string RemoveInvalidImageLinks(string lastMessage, string dialogWithoutLastMessage)
        {
            var regex = new Regex(@"!\[(.*?)\]\((.*?)\)");
            var matches = regex.Matches(lastMessage);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 2)
                {
                    var imageUrl = match.Groups[2].Value;
                    var fullImageMarkdown = match.Value;

                    if (!ContainsImageLink(dialogWithoutLastMessage, imageUrl))
                    {
                        Trace.TraceInformation($"{nameof(SearchPostProcessor)}|Removing invalid image link: {fullImageMarkdown}");
                        lastMessage = lastMessage.Replace(fullImageMarkdown, "");
                    }
                }
            }
            return lastMessage;
        }

        private static bool ContainsImageLink(string input, string url)
        {
            // Decode HTML entities in URL for matching
            string decodedUrl = System.Net.WebUtility.HtmlDecode(url);
            // Replace & with a pattern that matches both & and &amp;
            string pattern = Regex.Escape(decodedUrl).Replace("&", "(&|&amp;)");
            var regex = new Regex(@"!\[.*?\]\(" + pattern + @"\)");

            //Trace.TraceInformation($"{nameof(ContainsImageLink)}|Regex Pattern: {regex}");
            //Trace.TraceInformation($"{nameof(ContainsImageLink)}|Input: {input}");

            bool isMatch = regex.IsMatch(input);
            //Trace.TraceInformation($"{nameof(ContainsImageLink)}|Match Found: {isMatch}");

            return isMatch;
        }
    }
}