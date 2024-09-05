namespace AntRunnerLib
{

    /// <summary>
    /// Contains tests to verify the correct setup of environment variables that are critical for other tests,
    /// particularly those that interact with Azure Blob Storage.
    /// This class ensures that the necessary environment variables for connecting to and referencing
    /// the correct Azure Blob Storage container are properly set.
    /// </summary>
    [TestClass]
    public class EnvironmentTests
    {
        /// <summary>
        /// Tests to ensure that the necessary environment variables for Azure Blob Storage connection
        /// and container reference are correctly set. This test checks if both 'ASSISTANTS_STORAGE_CONNECTION'
        /// and 'ASSISTANTS_STORAGE_CONTAINER' environment variables are not null or empty, which is
        /// essential for successful Azure operations in other tests.
        /// </summary>
        [TestMethod]
        public void EnvironmentVariables_AreCorrectlySet()
        {
            // Check ASSISTANTS_STORAGE_CONNECTION
            string? storageConnection = Environment.GetEnvironmentVariable("ASSISTANTS_STORAGE_CONNECTION");
            Assert.IsNotNull(storageConnection, "ASSISTANTS_STORAGE_CONNECTION should not be null.");

            // Check ASSISTANTS_STORAGE_CONTAINER
            string? storageContainer = Environment.GetEnvironmentVariable("ASSISTANTS_STORAGE_CONTAINER");
            Assert.IsNotNull(storageContainer, "ASSISTANTS_STORAGE_CONTAINER should not be null.");
        }
    }
}