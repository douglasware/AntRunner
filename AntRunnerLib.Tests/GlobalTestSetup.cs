using Azure.Storage.Blobs;

namespace AntRunnerLib
{

    /// <summary>
    /// Provides assembly-wide setup and cleanup methods to manage resources for integration tests.
    /// This class handles the setup of Azure Blob Storage by creating a container and populating it with test data,
    /// and it cleans up by removing all created resources after tests are complete.
    /// </summary>
    [TestClass]
    public class GlobalTestSetup
    {
        private static BlobServiceClient? _blobServiceClient;
        private static BlobContainerClient? _containerClient;
        private static readonly string ContainerName = "assistants";

        /// <summary>
        /// Initializes Azure Blob Storage for use in testing by setting necessary environment variables,
        /// creating a storage container if it does not already exist, and uploading test data.
        /// This method runs once before any test methods in the assembly are executed.
        /// </summary>
        /// <param name="context">Provides contextual information about the test run, unused in this method.</param>
        [AssemblyInitialize]
        public static async Task AssemblyInit(TestContext context)
        {
            // Least ugly way to avoid compiler warning as MSTest absolutely requires the right method signature
            context.GetType();

            Environment.SetEnvironmentVariable("ASSISTANTS_STORAGE_CONNECTION", "UseDevelopmentStorage=true");
            Environment.SetEnvironmentVariable("ASSISTANTS_STORAGE_CONTAINER", ContainerName);

            _blobServiceClient = new BlobServiceClient(Environment.GetEnvironmentVariable("ASSISTANTS_STORAGE_CONNECTION"));
            _containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);

            await _containerClient.CreateIfNotExistsAsync();

            string localPath = Path.Combine(Environment.CurrentDirectory, "TestData", "Assistants");
            await UploadDirectoryAsync(localPath, string.Empty);
        }

        /// <summary>
        /// Cleans up the resources used for testing by deleting the Azure Blob Storage container
        /// and clearing set environment variables. This method runs once after all test methods in the assembly have completed.
        /// </summary>
        [AssemblyCleanup]
        public static async Task AssemblyCleanUp()
        {
            if (await _containerClient!.ExistsAsync())
            {
                //await containerClient.DeleteAsync();
            }

            Environment.SetEnvironmentVariable("ASSISTANTS_STORAGE_CONNECTION", null);
            Environment.SetEnvironmentVariable("ASSISTANTS_STORAGE_CONTAINER", null);
        }

        /// <summary>
        /// Recursively uploads files from a specified local directory to the Azure Blob Storage container,
        /// preserving the directory structure.
        /// </summary>
        /// <param name="sourcePath">The path of the local directory to upload from.</param>
        /// <param name="destPath">The path within the Blob container where the files will be uploaded.</param>
        private static async Task UploadDirectoryAsync(string sourcePath, string destPath)
        {
            foreach (var directory in Directory.GetDirectories(sourcePath))
            {
                string subDirName = Path.GetFileName(directory);
                await UploadDirectoryAsync(directory, Path.Combine(destPath, subDirName));
            }

            foreach (var filePath in Directory.GetFiles(sourcePath))
            {
                string blobName = Path.Combine(destPath, Path.GetFileName(filePath));
                BlobClient blobClient = _containerClient!.GetBlobClient(blobName.Replace("\\", "/"));
                await blobClient.UploadAsync(filePath, overwrite: true);
            }
        }
    }
}