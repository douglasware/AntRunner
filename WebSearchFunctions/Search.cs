﻿using AntRunnerLib.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AntRunnerLib;

namespace WebSearchFunctions
{
    public class SearchTool
    {
        private static string _searchApi = "{\r\n  \"openapi\": \"3.0.0\",\r\n  \"info\": {\r\n    \"title\": \"Bing Web Search API\",\r\n    \"version\": \"v7.0\",\r\n    \"description\": \"The Bing Web Search API returns web search results for a given query.\"\r\n  },\r\n  \"servers\": [\r\n    {\r\n      \"url\": \"https://api.bing.microsoft.com\",\r\n      \"description\": \"Bing Web Search API server\"\r\n    }\r\n  ],\r\n  \"paths\": {\r\n    \"/v7.0/search\": {\r\n      \"get\": {\r\n        \"summary\": \"Search the web\",\r\n        \"operationId\": \"crawl\",\r\n        \"parameters\": [\r\n          {\r\n            \"name\": \"q\",\r\n            \"in\": \"query\",\r\n            \"required\": true,\r\n            \"schema\": {\r\n              \"type\": \"string\"\r\n            },\r\n            \"description\": \"The user's search query string.\"\r\n          },\r\n          {\r\n            \"name\": \"answerCount\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"integer\"\r\n            },\r\n            \"description\": \"The number of answers that you want the response to include.\"\r\n          },\r\n          {\r\n            \"name\": \"cc\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"string\"\r\n            },\r\n            \"description\": \"A 2-character country code of the country where the results come from.\"\r\n          },\r\n          {\r\n            \"name\": \"count\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"integer\",\r\n              \"default\": 10,\r\n              \"maximum\": 50\r\n            },\r\n            \"description\": \"The number of search results to return in the response.\"\r\n          },\r\n          {\r\n            \"name\": \"freshness\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"string\"\r\n            },\r\n            \"description\": \"Filter search results by their age.\"\r\n          },\r\n          {\r\n            \"name\": \"mkt\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"string\"\r\n            },\r\n            \"description\": \"The market where the results come from.\"\r\n          },\r\n          {\r\n            \"name\": \"offset\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"integer\",\r\n              \"default\": 0\r\n            },\r\n            \"description\": \"The zero-based offset that indicates the number of search results to skip before returning results.\"\r\n          },\r\n          {\r\n            \"name\": \"promote\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"string\"\r\n            },\r\n            \"description\": \"A comma-delimited list of answers that you want the response to include regardless of their ranking.\"\r\n          },\r\n          {\r\n            \"name\": \"responseFilter\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"string\"\r\n            },\r\n            \"description\": \"A comma-delimited list of answers to include in the response.\"\r\n          },\r\n          {\r\n            \"name\": \"safeSearch\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"string\",\r\n              \"enum\": [\r\n                \"Off\",\r\n                \"Moderate\",\r\n                \"Strict\"\r\n              ],\r\n              \"default\": \"Moderate\"\r\n            },\r\n            \"description\": \"Used to filter webpages, images, and videos for adult content.\"\r\n          },\r\n          {\r\n            \"name\": \"setLang\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"string\"\r\n            },\r\n            \"description\": \"The language to use for user interface strings.\"\r\n          },\r\n          {\r\n            \"name\": \"textDecorations\",\r\n            \"in\": \"query\",\r\n            \"required\": false,\r\n            \"schema\": {\r\n              \"type\": \"boolean\",\r\n              \"default\": false\r\n            },\r\n            \"description\": \"A Boolean value that determines whether display strings in the results should contain decoration markers such as hit highlighting characters.\"\r\n          },\r\n          {\r\n            \"name\": \"textFormat\",\r\n            \"in\": \"query\",\r\n            \"required\": true,\r\n            \"schema\": {\r\n              \"type\": \"string\",\r\n              \"enum\": [\r\n                \"Raw\"\r\n              ],\r\n              \"default\": \"Raw\"\r\n            },\r\n            \"description\": \"Required\"\r\n          }\r\n        ],\r\n        \"responses\": {\r\n          \"200\": {\r\n            \"description\": \"Successful response\",\r\n            \"content\": {\r\n              \"application/json\": {\r\n                \"schema\": {\r\n                  \"type\": \"object\",\r\n                  \"properties\": {\r\n                    \"queryContext\": {\r\n                      \"type\": \"object\",\r\n                      \"properties\": {\r\n                        \"originalQuery\": {\r\n                          \"type\": \"string\"\r\n                        }\r\n                      }\r\n                    },\r\n                    \"webPages\": {\r\n                      \"type\": \"object\",\r\n                      \"properties\": {\r\n                        \"value\": {\r\n                          \"type\": \"array\",\r\n                          \"items\": {\r\n                            \"type\": \"object\",\r\n                            \"properties\": {\r\n                              \"name\": {\r\n                                \"type\": \"string\"\r\n                              },\r\n                              \"url\": {\r\n                                \"type\": \"string\"\r\n                              },\r\n                              \"cachedPageUrl\": {\r\n                                \"type\": \"string\"\r\n                              }\r\n                            },\r\n                            \"required\": [\r\n                              \"name\",\r\n                              \"url\",\r\n                              \"cachedPageUrl\"\r\n                            ]\r\n                          }\r\n                        }\r\n                      }\r\n                    },\r\n                    \"images\": {\r\n                      \"type\": \"object\",\r\n                      \"properties\": {\r\n                        \"value\": {\r\n                          \"type\": \"array\",\r\n                          \"items\": {\r\n                            \"type\": \"object\",\r\n                            \"properties\": {\r\n                              \"thumbnailUrl\": {\r\n                                \"type\": \"string\"\r\n                              },\r\n                              \"contentUrl\": {\r\n                                \"type\": \"string\"\r\n                              },\r\n                              \"hostPageUrl\": {\r\n                                \"type\": \"string\"\r\n                              }\r\n                            },\r\n                            \"required\": [\r\n                              \"thumbnailUrl\",\r\n                              \"contentUrl\",\r\n                              \"hostPageUrl\"\r\n                            ]\r\n                          }\r\n                        }\r\n                      }\r\n                    }\r\n                  },\r\n                  \"required\": [\r\n                    \"queryContext\",\r\n                    \"webPages\",\r\n                    \"images\"\r\n                  ]\r\n                }\r\n              }\r\n            }\r\n          }\r\n        }\r\n      }\r\n    }\r\n  }\r\n}";

        private static string _searchAuth = "{\"hosts\":{\"api.bing.microsoft.com\":{\"auth_type\":\"service_http\",\"header_name\":\"Ocp-Apim-Subscription-Key\",\"header_value_env_var\":\"SEARCH_API_KEY\"}}}";

        private static string _pageAnt = "{\"openapi\":\"3.0.0\",\"info\":{\"title\":\"Local Test Function\",\"description\":\"Describes a function implemented in the host as static method\"},\"servers\":[{\"url\":\"tool://localhost\",\"description\":\"Create markdown from a Url\"}],\"paths\":{\"AntRunnerLib.AssistantRunner.RunThread\":{\"post\":{\"operationId\":\"ExtractContentFromUrl\",\"summary\":\"Reads a web page and returns markdown of the content\",\"requestBody\":{\"required\":true,\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\",\"properties\":{\"assistantName\":{\"type\":\"string\",\"description\":\"Required. Must be WebPageAgentAnt\",\"default\":\"WebPageAgentAnt\"},\"instructions\":{\"type\":\"string\",\"description\":\"Required. A url to a page and a question or statement that the assitant will use to extract relevant content. Example 'what is the timeout of the foo operation? http://www.example.com/doc'\"}},\"required\":[\"assistantName\",\"instructions\"]}}}}}}}}";

        public async static Task<string> Search(string query)
        {
            var domainAuth = JsonSerializer.Deserialize<DomainAuth>(_searchAuth);
            var validationResult = OpenApiHelper.ValidateAndParseOpenApiSpec(_searchApi);
            var spec = validationResult.Spec;
            var toolDefinitions = OpenApiHelper.GetToolDefinitionsFromSchema(spec!);
            var toolCallers = await ToolCallers.GetToolCallers(spec, toolDefinitions, domainAuth);

            var crawlTool = toolCallers["crawl"];

            crawlTool.Params = new()
            {
                {"q", query},
                {"textFormat", "HTML"}
            };

            var response = await crawlTool.ExecuteWebApiAsync();
            var responseContent = await response.Content.ReadAsStringAsync();

            string output;

            if (crawlTool.ResponseSchemas.TryGetValue("200", out var schemaJson))
            {
                try
                {
                    var contentJson = JsonDocument.Parse(responseContent).RootElement;

                    var filteredJson = ThreadUtility.FilterJsonBySchema(contentJson, schemaJson);
                    var pages = filteredJson.GetProperty("webPages");
                    var pagesValue = pages.GetProperty("value");

                    Dictionary<string, string> pageUrls = new();
                    foreach (var page in pagesValue.EnumerateArray())
                    {
                        var name = page.GetProperty("name").GetString();
                        var url = page.GetProperty("url").GetString();
                        var cachedPageUrl = page.GetProperty("cachedPageUrl").GetString();
                        pageUrls[url] = cachedPageUrl;

                        Console.WriteLine($"Name: {name}");
                        Console.WriteLine($"Url: {url}");
                        Console.WriteLine($"CachedPageUrl: {cachedPageUrl}");
                    }
                    output = JsonSerializer.Serialize(pageUrls);
                }
                catch
                {
                    // If filtering fails, use the original response content.
                    output = responseContent;
                }
            }
            else
            {
                // If no response schema, use the original response content.
                output = responseContent;
            }
            return output;
        }
    }
}
