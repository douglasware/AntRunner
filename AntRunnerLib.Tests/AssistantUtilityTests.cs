namespace AntRunnerLib
{

    /// <summary>
    /// Contains tests for validating the functionality of the AssistantUtility class,
    /// which retrieves assistant creation options from either embedded resources or a blob storage.
    /// </summary>
    [TestClass]
    public class AssistantUtilityTests
    {
        /// <summary>
        /// Verifies that the GetAssistantCreateRequest method returns a non-null object
        /// when the assistant definition is successfully retrieved from an embedded resource.
        /// The test ensures that the method can correctly read and parse embedded resources
        /// based on a provided assistant name that matches an existing resource.
        /// </summary>
        [TestMethod]
        public async Task GetAssistantCreateRequest_ReturnsOptions_WhenResourceIsEmbedded()
        {
            // Arrange
            string assistantName = "ConversationUserProxy"; 

            // Act
            var options = await AssistantUtility.GetAssistantCreateRequest(assistantName);

            // Assert
            Assert.IsNotNull(options);
            // Additional asserts to verify the properties of options
        }

        /// <summary>
        /// Tests that the GetAssistantCreateRequest method returns a non-null object
        /// when the assistant definition is successfully retrieved from Azure Blob Storage.
        /// This test checks the ability of the method to fetch and deserialize the assistant data
        /// when the specified blob exists in the designated container.
        /// </summary>
        [TestMethod]
        public async Task GetAssistantCreateRequest_ReturnsOptions_WhenBlobExists()
        {
            // Arrange
            string assistantName = "Pirate"; // Ensure this blob exists in the container

            // Act
            var options = await AssistantUtility.GetAssistantCreateRequest(assistantName);

            // Assert
            Assert.IsNotNull(options);
            // Additional asserts to verify the properties of options
        }

        /// <summary>
        /// Ensures that the GetAssistantCreateRequest method returns null
        /// when neither an embedded resource nor a blob exists for the specified assistant name.
        /// This test confirms the method's correct handling of cases where the requested data is absent,
        /// simulating a failure to locate the assistant definition in both storage mediums.
        /// </summary>
        [TestMethod]
        public async Task GetAssistantCreateRequest_ReturnsNull_WhenNoResourceOrBlob()
        {
            // Arrange
            string assistantName = "NonexistentResourceAndBlob"; // Ensure this does not exist anywhere

            // Act
            var options = await AssistantUtility.GetAssistantCreateRequest(assistantName);

            // Assert
            Assert.IsNull(options);
        }
    }
}