using System.Text.Json;

namespace AntRunnerLib.Tests
{
    [TestClass]
    public class FilterJsonBySchemaTests
    {
        [TestMethod]
        public void TestFilterJsonBySchema_WithValidInput_ProducesExpectedOutput()
        {
            // Arrange
            var content = @"
        {
          ""_type"": ""SearchResponse"",
          ""queryContext"": {
            ""originalQuery"": ""events in Atlanta on September 28 2024"",
            ""askUserForLocation"": true
          },
          ""webPages"": {
            ""webSearchUrl"": ""https:\/\/www.bing.com\/search?q=events+in+Atlanta+on+September+28+2024"",
            ""totalEstimatedMatches"": 1500000,
            ""value"": [
              {
                ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#WebPages.0"",
                ""name"": ""Atlanta Events September 2024: Concerts, Shows, Sports..."",
                ""url"": ""https:\/\/atlanta-ga.events\/september\/"",
                ""datePublished"": ""2024-09-19T00:00:00.0000000"",
                ""datePublishedFreshnessText"": ""3 days ago"",
                ""isFamilyFriendly"": true,
                ""displayUrl"": ""https:\/\/atlanta-ga.events\/september"",
                ""snippet"": ""Check Out The Atlanta Events Calendar for September 2024. See the List Of All Current & Upcoming Events at the Lowest Possible Price. ... Prices from $28 Avg. price ~ $64. 18 tickets remaining! Tickets; Sep. 21. 2024. 8:00 PM Sat. Mat Kearney. The Tabernacle - GA | Capacity: 2600. 30303, 152 Luckie St, Atlanta, GA, US Prices from $37 Avg. price ..."",
                ""dateLastCrawled"": ""2024-09-19T19:10:00.0000000Z"",
                ""cachedPageUrl"": ""http:\/\/cc.bingj.com\/cache.aspx?q=events+in+Atlanta+on+September+28+2024&d=4854189675467386&mkt=en-US&setlang=en-US&w=ShlkdiIwTMefbkmdL1lCvH4mS1bjztOY"",
                ""language"": ""en"",
                ""isNavigational"": true,
                ""noCache"": false,
                ""siteName"": ""Atlanta Events""
              },
              {
                ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#WebPages.1"",
                ""name"": ""Atlanta Events - September 2024 - Go South Atlanta"",
                ""url"": ""https:\/\/gosouthatlanta.com\/things-to-do\/events\/atlanta-in-september.html"",
                ""thumbnailUrl"": ""https:\/\/www.bing.com\/th?id=OIP.xm-3dn3hYJZHsVVU9WO5qwHaD4&w=80&h=80&c=1&pid=5.1"",
                ""isFamilyFriendly"": true,
                ""displayUrl"": ""https:\/\/gosouthatlanta.com\/things-to-do\/events\/atlanta-in-september.html"",
                ""snippet"": ""More details. – Atlanta Black Pride Weekend, August 28 - September 2 2024. Annual LGBTQ+ festival with male, female and youth events across Atlanta, including parties, block parties, food events, performances, film festival, and more. More details. – Dragon Con, August 29 - September 2 2024."",
                ""dateLastCrawled"": ""2024-09-21T20:12:00.0000000Z"",
                ""primaryImageOfPage"": {
                  ""thumbnailUrl"": ""https:\/\/www.bing.com\/th?id=OIP.xm-3dn3hYJZHsVVU9WO5qwHaD4&w=80&h=80&c=1&pid=5.1"",
                  ""width"": 80,
                  ""height"": 80,
                  ""sourceWidth"": 474,
                  ""sourceHeight"": 248,
                  ""imageId"": ""OIP.xm-3dn3hYJZHsVVU9WO5qwHaD4""
                },
                ""cachedPageUrl"": ""http:\/\/cc.bingj.com\/cache.aspx?q=events+in+Atlanta+on+September+28+2024&d=4712253893115949&mkt=en-US&setlang=en-US&w=D6V2DS_RjZRe4oOvzq1dGpeg_GE4sHjt"",
                ""language"": ""en"",
                ""isNavigational"": false,
                ""noCache"": false,
                ""siteName"": ""Go South Atlanta""
              }
            ],
            ""someResultsRemoved"": true
          },
          ""relatedSearches"": {
            ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#RelatedSearches"",
            ""value"": [
              {
                ""text"": ""atlanta events september 2024"",
                ""displayText"": ""atlanta events september 2024"",
                ""webSearchUrl"": ""https:\/\/www.bing.com\/search?q=atlanta+events+september+2024""
              },
              {
                ""text"": ""atlanta in september 2023"",
                ""displayText"": ""atlanta in september 2023"",
                ""webSearchUrl"": ""https:\/\/www.bing.com\/search?q=atlanta+in+september+2023""
              }
            ]
          },
          ""rankingResponse"": {
            ""mainline"": {
              ""items"": [
                {
                  ""answerType"": ""WebPages"",
                  ""resultIndex"": 0,
                  ""value"": { ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#WebPages.0"" }
                },
                {
                  ""answerType"": ""WebPages"",
                  ""resultIndex"": 1,
                  ""value"": { ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#WebPages.1"" }
                },
                {
                  ""answerType"": ""WebPages"",
                  ""resultIndex"": 2,
                  ""value"": { ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#WebPages.2"" }
                },
                {
                  ""answerType"": ""WebPages"",
                  ""resultIndex"": 3,
                  ""value"": { ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#WebPages.3"" }
                },
                {
                  ""answerType"": ""WebPages"",
                  ""resultIndex"": 4,
                  ""value"": { ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#WebPages.4"" }
                },
                {
                  ""answerType"": ""RelatedSearches"",
                  ""value"": { ""id"": ""https:\/\/api.bing.microsoft.com\/api\/v7\/#RelatedSearches"" }
                }
              ]
            }
          }
        }";

            var schema = @"
        {
          ""type"": ""object"",
          ""properties"": {
            ""queryContext"": {
              ""type"": ""object"",
              ""properties"": {
                ""originalQuery"": {
                  ""type"": ""string""
                }
              }
            },
            ""webPages"": {
              ""type"": ""object"",
              ""properties"": {
                ""value"": {
                  ""type"": ""array"",
                  ""items"": {
                    ""type"": ""object"",
                    ""properties"": {
                      ""name"": {
                        ""type"": ""string""
                      },
                      ""url"": {
                        ""type"": ""string""
                      },
                      ""displayUrl"": {
                        ""type"": ""string""
                      },
                      ""snippet"": {
                        ""type"": ""string""
                      },
                      ""cachedPageUrl"": {
                        ""type"": ""string""
                      },
                      ""thumbnailUrl"": {
                        ""type"": ""string""
                      }
                    },
                    ""required"": [
                      ""name"",
                      ""url"",
                      ""displayUrl"",
                      ""snippet"",
                      ""cachedPageUrl""
                    ]
                  }
                }
              }
            },
            ""images"": {
              ""type"": ""object"",
              ""properties"": {
                ""value"": {
                  ""type"": ""array"",
                  ""items"": {
                    ""type"": ""object"",
                    ""properties"": {
                      ""thumbnailUrl"": {
                        ""type"": ""string""
                      },
                      ""contentUrl"": {
                        ""type"": ""string""
                      },
                      ""hostPageUrl"": {
                        ""type"": ""string""
                      }
                    },
                    ""required"": [
                      ""thumbnailUrl"",
                      ""contentUrl"",
                      ""hostPageUrl""
                    ]
                  }
                }
              }
            }
          },
          ""required"": [
            ""queryContext"",
            ""webPages"",
            ""images""
          ]
        }";

            var contentJson = JsonDocument.Parse(content).RootElement;
            var schemaJson = JsonDocument.Parse(schema).RootElement;

            // Act
            var filteredContent = ThreadUtility.FilterJsonBySchema(contentJson, schemaJson);

            // Assert
            var expectedJson = @"
        {
          ""queryContext"": {
            ""originalQuery"": ""events in Atlanta on September 28 2024""
          },
          ""webPages"": {
            ""value"": [
              {
                ""name"": ""Atlanta Events September 2024: Concerts, Shows, Sports..."",
                ""url"": ""https:\/\/atlanta-ga.events\/september\/"",
                ""displayUrl"": ""https:\/\/atlanta-ga.events\/september"",
                ""snippet"": ""Check Out The Atlanta Events Calendar for September 2024. See the List Of All Current & Upcoming Events at the Lowest Possible Price. ... Prices from $28 Avg. price ~ $64. 18 tickets remaining! Tickets; Sep. 21. 2024. 8:00 PM Sat. Mat Kearney. The Tabernacle - GA | Capacity: 2600. 30303, 152 Luckie St, Atlanta, GA, US Prices from $37 Avg. price ..."",
                ""cachedPageUrl"": ""http:\/\/cc.bingj.com\/cache.aspx?q=events+in+Atlanta+on+September+28+2024&d=4854189675467386&mkt=en-US&setlang=en-US&w=ShlkdiIwTMefbkmdL1lCvH4mS1bjztOY""
              },
              {
                ""name"": ""Atlanta Events - September 2024 - Go South Atlanta"",
                ""url"": ""https:\/\/gosouthatlanta.com\/things-to-do\/events\/atlanta-in-september.html"",
                ""displayUrl"": ""https:\/\/gosouthatlanta.com\/things-to-do\/events\/atlanta-in-september.html"",
                ""snippet"": ""More details. – Atlanta Black Pride Weekend, August 28 - September 2 2024. Annual LGBTQ+ festival with male, female and youth events across Atlanta, including parties, block parties, food events, performances, film festival, and more. More details. – Dragon Con, August 29 - September 2 2024."",
                ""cachedPageUrl"": ""http:\/\/cc.bingj.com\/cache.aspx?q=events+in+Atlanta+on+September+28+2024&d=4712253893115949&mkt=en-US&setlang=en-US&w=D6V2DS_RjZRe4oOvzq1dGpeg_GE4sHjt"",
                ""thumbnailUrl"": ""https:\/\/www.bing.com\/th?id=OIP.xm-3dn3hYJZHsVVU9WO5qwHaD4&w=80&h=80&c=1&pid=5.1""
              }
            ]
          }
        }";

            var expectedJsonElement = JsonDocument.Parse(expectedJson).RootElement;

            Assert.IsTrue(JsonElementsAreEqual(expectedJsonElement, filteredContent));
        }

        private bool JsonElementsAreEqual(JsonElement e1, JsonElement e2)
        {
            if (e1.ValueKind != e2.ValueKind)
            {
                return false;
            }

            switch (e1.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var properties1 = new Dictionary<string, JsonElement>();
                    foreach (var property in e1.EnumerateObject())
                    {
                        properties1[property.Name] = property.Value;
                    }

                    var properties2 = new Dictionary<string, JsonElement>();
                    foreach (var property in e2.EnumerateObject())
                    {
                        properties2[property.Name] = property.Value;
                    }

                    if (properties1.Count != properties2.Count)
                    {
                        return false;
                    }

                    foreach (var kvp in properties1)
                    {
                        if (!properties2.TryGetValue(kvp.Key, out var value) || !JsonElementsAreEqual(kvp.Value, value))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                case JsonValueKind.Array:
                {
                    var array1 = e1.EnumerateArray();
                    var array2 = e2.EnumerateArray();

                    var list1 = new List<JsonElement>(array1);
                    var list2 = new List<JsonElement>(array2);

                    if (list1.Count != list2.Count)
                    {
                        return false;
                    }

                    for (int i = 0; i < list1.Count; i++)
                    {
                        if (!JsonElementsAreEqual(list1[i], list2[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                case JsonValueKind.String:
                    return e1.GetString() == e2.GetString();
                case JsonValueKind.Number:
                    return e1.GetDecimal() == e2.GetDecimal();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return e1.GetBoolean() == e2.GetBoolean();
                case JsonValueKind.Null:
                    return true;
                default:
                    throw new NotImplementedException($"Unknown JsonValueKind: {e1.ValueKind}");
            }
        }
    }
}