using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using FunctionCalling;
using OpenAI.ObjectModels.RequestModels;

namespace AntRunnerLib
{
    [TestClass]
    public class OpenApiHelperTests
    {
        [TestMethod]
        public void ValidateAndParseOpenAPISpec_ValidJson_ShouldReturnTrue()
        {
            // Arrange
            var openApiHelper = new OpenApiHelper();
            string validJson = @"
            {
                ""openapi"": ""3.0.0"",
                ""info"": {
                    ""title"": ""Sample API"",
                    ""version"": ""1.0.0""
                },
                ""servers"": [
                    {
                        ""url"": ""https://api.example.com""
                    }
                ],
                ""paths"": {
                    ""/endpoint"": {
                        ""get"": {
                            ""summary"": ""Sample endpoint"",
                            ""operationId"": ""getSample"",
                            ""responses"": {
                                ""200"": {
                                    ""description"": ""Successful response""
                                }
                            }
                        }
                    }
                }
            }";

            // Act
            var result = openApiHelper.ValidateAndParseOpenAPISpec(validJson);

            // Assert
            Assert.IsTrue(result.Status);
            Assert.IsNotNull(result.Spec);
        }

        [TestMethod]
        public void ValidateAndParseOpenAPISpec_InvalidJson_ShouldReturnFalse()
        {
            // Arrange
            var openApiHelper = new OpenApiHelper();
            string invalidJson = @"{""invalid"":}";

            // Act
            var result = openApiHelper.ValidateAndParseOpenAPISpec(invalidJson);

            // Assert
            Assert.IsFalse(result.Status);
            Assert.AreEqual("Could not find a valid URL in `servers`", result.Message);
        }

        [TestMethod]
        public async Task OpenApiToFunction_ValidSpec_ShouldReturnToolDefinitions()
        {
            // Arrange
            var openApiHelper = new OpenApiHelper();
            string validJson = @"
            {
                ""openapi"": ""3.0.0"",
                ""info"": {
                    ""title"": ""Sample API"",
                    ""version"": ""1.0.0""
                },
                ""servers"": [
                    {
                        ""url"": ""https://api.example.com""
                    }
                ],
                ""paths"": {
                    ""/endpoint"": {
                        ""get"": {
                            ""summary"": ""Sample endpoint"",
                            ""operationId"": ""getSample"",
                            ""responses"": {
                                ""200"": {
                                    ""description"": ""Successful response""
                                }
                            }
                        }
                    }
                }
            }";
            var validationResult = openApiHelper.ValidateAndParseOpenAPISpec(validJson);
            var spec = validationResult.Spec;

            // Act
            var toolDefinitions = openApiHelper.GetToolDefinitions(spec!);
            var requestBuilders = await openApiHelper.GetRequestBuilders(spec!, toolDefinitions);

            // Assert
            Assert.AreEqual(1, toolDefinitions.Count);
            var functionDefinition = toolDefinitions[0].Function?.AsObject;
            Assert.IsNotNull(functionDefinition);
            Assert.AreEqual("getSample", functionDefinition.Name);
            Assert.AreEqual("Sample endpoint", functionDefinition.Description);
            Assert.AreEqual(0, functionDefinition.Parameters?.Properties!.Count);

            Assert.AreEqual(1, requestBuilders.Count);
            Assert.IsTrue(requestBuilders.ContainsKey("getSample"));
        }

        [TestMethod]
        public async Task OpenApiToFunction_EmptyPaths_ShouldReturnEmptyLists()
        {
            // Arrange
            var openApiHelper = new OpenApiHelper();
            string validJson = @"
            {
                ""openapi"": ""3.0.0"",
                ""info"": {
                    ""title"": ""Sample API"",
                    ""version"": ""1.0.0""
                },
                ""servers"": [
                    {
                        ""url"": ""https://api.example.com""
                    }
                ],
                ""paths"": {}
            }";
            var validationResult = openApiHelper.ValidateAndParseOpenAPISpec(validJson);
            var spec = validationResult.Spec;

            // Act
            var toolDefinitions = openApiHelper.GetToolDefinitions(spec!);
            var requestBuilders = await openApiHelper.GetRequestBuilders(spec!, toolDefinitions);

            // Assert
            Assert.AreEqual(0, toolDefinitions.Count);
            Assert.AreEqual(0, requestBuilders.Count);
        }

        private string ReadFileContent(string relativePath)
        {
            var projectDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var filePath = System.IO.Path.Combine(projectDirectory ?? string.Empty, relativePath);
            return System.IO.File.ReadAllText(filePath);
        }

        [TestMethod]
        public async Task OpenApiToFunction_FromFile_ShouldReturnToolDefinitions()
        {
            System.Environment.SetEnvironmentVariable("SEARCH_API_KEY", "YOUR_KEY_HERE++");
            // Arrange
            var openApiHelper = new OpenApiHelper();
            string filePath = @".\TestData\Assistants\Blob Pirate\OpenAPI\api.bing.microsoft.com.json";
            string fileContent = ReadFileContent(filePath);

            // Act
            var validationResult = openApiHelper.ValidateAndParseOpenAPISpec(fileContent);
            var spec = validationResult.Spec;

            if (spec == null)
            {
                Assert.Fail("Spec was null after validation.");
            }

            var toolDefinitions = openApiHelper.GetToolDefinitions(spec);
            var requestBuilders = await openApiHelper.GetRequestBuilders(spec, toolDefinitions, "Blob Pirate");

            // Assert
            Assert.IsTrue(toolDefinitions.Count > 0);
            Assert.IsTrue(requestBuilders.Count > 0);

            // LastMessage the results for reference
            foreach (var td in toolDefinitions)
            {
                var functionDefinition = td.Function?.AsObject;
                if (functionDefinition != null)
                {
                    Console.WriteLine($"Function: {functionDefinition.Name}, Description: {functionDefinition.Description}");
                }
            }

            foreach (var key in requestBuilders.Keys)
            {
                Console.WriteLine($"Request Builder: {key}");
            }
        }
    }
}