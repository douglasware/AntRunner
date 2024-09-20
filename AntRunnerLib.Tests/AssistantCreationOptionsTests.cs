using OpenAI.Builders;
using OpenAI.ObjectModels.RequestModels;

namespace AntRunnerLib
{
    [TestClass]
    public class AssistantCreateRequestTests
    {
        [TestMethod]
        public async Task GetAssistantCreateRequest_ReturnsCorrectValues()
        {
            var func = new FunctionDefinitionBuilder("get_current_weather", "Gets the current weather at the user's location").Validate().Build();
            var expected = new AssistantCreateRequest()
            {
                Name = "AllProps",
                Description = "Description",
                Instructions = "Instructions",
                Metadata = new Dictionary<string, string> { { "A", "B" } },
                ToolResources = new ToolResources()
                {
                    CodeInterpreter = new CodeInterpreter() { FileIds = ["A", "B"] },
                    FileSearch = new FileSearch() { VectorStoreIds = ["1", "2"] }
                },
                ResponseFormat = new ResponseFormatOneOfType("auto"),
                Tools =
                [
                    //new FunctionToolDefinition("get_current_weather", "Gets the current weather at the user's location"),
                //    ToolDefinition.DefineFunction(func),
                    ToolDefinition.DefineCodeInterpreter()
                ],
                TopP = 1,
                Temperature = 1
            };

            // Act: Simulate the method that retrieves the object populated from a JSON file.
            var actual = await AssistantUtility.GetAssistantCreateRequest("AllProps");

            // Assert: Ensure every field matches the expected setup.
            Assert.AreEqual(expected.Name, actual!.Name);
            Assert.AreEqual(expected.Description, actual.Description);
            Assert.AreEqual(expected.Instructions, actual.Instructions);
            Assert.AreEqual(expected.Metadata["A"], actual.Metadata!["A"]);
            Assert.AreEqual(expected.ToolResources.CodeInterpreter.FileIds[0], actual.ToolResources!.CodeInterpreter?.FileIds?[0]);
            Assert.AreEqual(expected.ToolResources.FileSearch.VectorStoreIds[0], actual.ToolResources.FileSearch?.VectorStoreIds?[0]);
            Assert.AreEqual(expected.ResponseFormat.AsString, actual.ResponseFormat!.AsString);
            Assert.AreEqual(expected.Temperature, actual.Temperature);
            Assert.AreEqual(expected.TopP, actual.TopP);
        }
    }
}