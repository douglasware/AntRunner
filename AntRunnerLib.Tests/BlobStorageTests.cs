using Azure.Storage.Blobs;


namespace AntRunnerLib
{

    /// <summary>
    /// Contains tests for validating the integrity and completeness of data stored in an Azure Blob Storage container.
    /// This class ensures that the contents of the blob container exactly mirror those of a specified local directory.
    /// </summary>
    [TestClass]
    public class BlobStorageTests
    {
        private static BlobServiceClient? _blobServiceClient;
        private static BlobContainerClient? _containerClient;
        private static readonly string? ContainerName = Environment.GetEnvironmentVariable("ASSISTANTS_STORAGE_CONTAINER");

        /// <summary>
        /// Initializes resources for the test class, establishing a connection to Azure Blob Storage.
        /// This method is called once before any tests are run in this test class and sets up the
        /// necessary blob service and container clients using environment variables.
        /// </summary>
        /// <param name="context">Provides information about and functionality for the current test run.</param>
        [ClassInitialize]
        public static void TestInit(TestContext context)
        {
            // Least ugly way to avoid compiler warning as MSTest absolutely requires the right method signature
            context.GetType();

            string? connectionString = Environment.GetEnvironmentVariable("ASSISTANTS_STORAGE_CONNECTION");
            _blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        }

        /// <summary>
        /// Verifies that the content of the Azure Blob Storage container is an exact match with the local TestData directory.
        /// This test checks that all files in the local 'TestData/Assistants' directory are present in the blob container
        /// with no missing or additional files, ensuring data integrity and consistency between the local file system and blob storage.
        /// </summary>
        [TestMethod]
        public async Task Container_MirrorsTestDataFolderContents()
        {
            string localTestDataPath = Path.Combine(Environment.CurrentDirectory, @"TestData\Assistants");
            var localFiles = Directory.GetFiles(localTestDataPath, "*", SearchOption.AllDirectories)
                .Select(path => path[localTestDataPath.Length..].TrimStart(Path.DirectorySeparatorChar).Replace("\\", "/"))
                .ToList();

            Assert.IsTrue(localFiles.Count > 0, "TestData folder should not be empty.");

            var blobItems = new List<string>();
            await foreach (var blobItem in _containerClient!.GetBlobsAsync())
            {
                blobItems.Add(blobItem.Name);
            }

            Assert.IsTrue(blobItems.Count > 0, "Blob container should not be empty.");

            var missingInBlob = localFiles.Except(blobItems).ToList();
            var extraInBlob = blobItems.Except(localFiles).ToList();

            Assert.IsTrue(missingInBlob.Count == 0, $"There are missing files in the Blob: {string.Join(", ", missingInBlob)}");
            Assert.IsTrue(extraInBlob.Count == 0, $"There are extra files in the Blob not found in TestData: {string.Join(", ", extraInBlob)}");
        }
    }
}