using AntRunnerLib.AssistantDefinitions;

namespace AntRunnerLib.Tests
{
    /// <summary>
    /// ConversationUserProxyInstructions is built in to the library as an embedded resource. This makes sure the assistant definition embedded resources exist.
    /// </summary>
    [TestClass]
    public class ConversationUserProxyInstructionsTests
    {
        [TestMethod]
        public async Task GetManifest_ShouldReturnManifestForKnownAssistant()
        {
            // Arrange
            string assistantName = "ConversationUserProxy";

            // Act
            string? result = await AssistantDefinitionFiles.GetManifest(assistantName);

            // Assert
            Assert.IsNotNull(result, "The manifest should be returned for a known assistant name.");
        }

        [TestMethod]
        public async Task GetInstructions_ShouldReturnInstructionsForKnownAssistant()
        {
            // Arrange
            string assistantName = "ConversationUserProxy";

            // Act
            string? result = await AssistantDefinitionFiles.GetInstructions(assistantName);

            // Assert
            Assert.IsNotNull(result, "The instructions should be returned for a known assistant name.");
        }
    }
}