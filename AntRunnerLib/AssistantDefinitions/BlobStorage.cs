using Azure.Storage.Blobs;

namespace AntRunnerLib.AssistantDefinitions
{
    internal class BlobStorage
    {
        private static BlobContainerClient? _blobContainerClient;

        static BlobStorage()
        {
            var connectionString = Environment.GetEnvironmentVariable("ASSISTANTS_STORAGE_CONNECTION");
            var containerName = Environment.GetEnvironmentVariable("ASSISTANTS_STORAGE_CONTAINER");

            if (string.IsNullOrEmpty(connectionString) || string.IsNullOrEmpty(containerName))
            {
                return;
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            _blobContainerClient =  blobServiceClient.GetBlobContainerClient(containerName);
        }

        private static async Task<string?> DownloadBlobAsStringAsync(BlobClient blobClient)
        {
            try
            {
                if (await blobClient.ExistsAsync())
                {
                    var response = await blobClient.DownloadAsync();
                    using var streamReader = new StreamReader(response.Value.Content);
                    return await streamReader.ReadToEndAsync();
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while accessing the blob storage.", ex);
            }
        }

        private static async Task<byte[]?> DownloadBlobAsByteArrayAsync(BlobClient blobClient)
        {
            try
            {
                if (await blobClient.ExistsAsync())
                {
                    var response = await blobClient.DownloadAsync();
                    using var memoryStream = new MemoryStream();
                    await response.Value.Content.CopyToAsync(memoryStream);
                    return memoryStream.ToArray();
                }
                return null;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while accessing the blob storage.", ex);
            }
        }

        private static async Task<IEnumerable<string>?> ListBlobsAsync(string prefix)
        {
            try
            {
                if (_blobContainerClient == null) return null;

                var blobs = _blobContainerClient.GetBlobsAsync(prefix: prefix);
                var fileList = new List<string>();

                await foreach (var blobItem in blobs)
                {
                    fileList.Add(blobItem.Name);
                }

                return fileList.Count > 0 ? fileList : null;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("An error occurred while accessing the blob storage.", ex);
            }
        }

        internal static async Task<string?> GetManifest(string assistantName)
        {
            var blobClient = _blobContainerClient?.GetBlobClient($"{assistantName}/manifest.json");
            return blobClient != null ? await DownloadBlobAsStringAsync(blobClient) : null;
        }

        internal static async Task<string?> GetInstructions(string assistantName)
        {
            var blobClient = _blobContainerClient?.GetBlobClient($"{assistantName}/instructions.md");
            return blobClient != null ? await DownloadBlobAsStringAsync(blobClient) : null;
        }

        internal static async Task<byte[]?> GetFile(string filePath)
        {
            var blobClient = _blobContainerClient?.GetBlobClient(filePath);
            return blobClient != null ? await DownloadBlobAsByteArrayAsync(blobClient) : null;
        }

        internal static Task<IEnumerable<string>?> GetFilesInVectorStoreFolder(string assistantName, string vectorStoreName)
        {
            return ListBlobsAsync($"{assistantName}/VectorStores/{vectorStoreName}/");
        }

        internal static Task<IEnumerable<string>?> GetFilesInCodeInterpreterFolder(string assistantName)
        {
            return ListBlobsAsync($"{assistantName}/CodeInterpreter");
        }

        internal static async Task<IEnumerable<string>?> GetFilesInOpenAPIFolder(string assistantName)
        {
            return (await ListBlobsAsync($"{assistantName}/OpenAPI"))?.Where(o => !o.Contains("auth.json", StringComparison.OrdinalIgnoreCase));
        }

        internal static async Task<string?> GetActionAuth(string assistantName)
        {
            var blobClient = _blobContainerClient?.GetBlobClient($"{assistantName}/OpenAPI/auth.json");
            return blobClient != null ? await DownloadBlobAsStringAsync(blobClient) : null;
        }
    }
}