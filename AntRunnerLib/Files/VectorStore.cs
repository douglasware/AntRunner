using OpenAI.ObjectModels.RequestModels;
using static AntRunnerLib.ClientUtility;
using static AntRunnerLib.AssistantUtility;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using System.Diagnostics;
using AntRunnerLib.AssistantDefinitions;

namespace AntRunnerLib
{
    /// <summary>
    /// Provides methods to ensure and manage vector stores for an assistant.
    /// </summary>
    public class VectorStore
    {
        /// <summary>
        /// Ensures that a vector store exists for the given assistant, creating it if necessary.
        /// </summary>
        /// <param name="assistant">The AssistantCreateRequest object containing assistant details.</param>
        /// <param name="vectorStoreName">The name of the vector store.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI. Can be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the vector store ID.</returns>
        public static async Task<string> EnsureVectorStore(AssistantCreateRequest assistant, string vectorStoreName, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Get the OpenAI client using the provided Azure OpenAI configuration
            var client = GetOpenAiClient(azureOpenAiConfig);

            // List existing vector stores
            var stores = await client.ListVectorStores(new PaginationRequest() { Limit = 100 });
            if (stores.Error != null)
            {
                throw new Exception($"Error in EnsureVectorStore. Failed loading stores {stores.Error.Message}");
            }
            Trace.TraceInformation($"Got existing vector stores");

            // Check if a store with the specified name already exists
            var existingStore = stores.Data?.FirstOrDefault(o => o.Name == vectorStoreName);
            if (existingStore != null)
            {
                Trace.TraceInformation($"Found existingStore.Id {existingStore.Id}");
                return existingStore.Id;
            }

            // Create a new vector store if it does not exist
            var newStore = await client.CreateVectorStore(new CreateVectorStoreRequest() { Name = vectorStoreName });
            if (newStore.Error != null)
            {
                throw new Exception($"Error in EnsureVectorStore. Failed to create store {newStore.Error.Message}");
            }
            
            Trace.TraceInformation($"Created vector store {newStore.Id}");
            return newStore.Id;
        }

        /// <summary>
        /// Creates vector files for the given assistant and vector store.
        /// Ensures that the files are uploaded and associated with the vector store.
        /// </summary>
        /// <param name="assistant">The AssistantCreateRequest object containing assistant details.</param>
        /// <param name="vectorStoreName">The name of the vector store.</param>
        /// <param name="vectorStoreId">The ID of the vector store.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI. Can be null.</param>
        public static async Task CreateVectorFiles(AssistantCreateRequest assistant, string vectorStoreName, string vectorStoreId, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Determine if the files are stored in the file system
            bool fileSystem = false;
            if (await FileStorage.GetManifest(assistant.Name!) != null)
            {
                fileSystem = true;
            }
            else if (await BlobStorage.GetManifest(assistant.Name!) == null)
            {
                throw new Exception($"Error in CreateVectorFiles. {assistant.Name!} not found in file system or blob storage");
            }

            // Get the list of file paths in the vector store folder
            var filePaths = fileSystem
                ? FileStorage.GetFilesInVectorStoreFolder(assistant.Name!, vectorStoreName)
                : await BlobStorage.GetFilesInVectorStoreFolder(assistant.Name!, vectorStoreName);

            // Check if any file paths were found
            if (filePaths == null || !filePaths.Any())
            {
                throw new Exception($"Error in CreateVectorFiles. {assistant.Name!} no files found for {vectorStoreName}");
            }

            // Get the OpenAI client using the provided Azure OpenAI configuration
            var client = GetOpenAiClient(azureOpenAiConfig);

            // List all files to check for existing files
            var allFiles = await client.Files.ListFile();
            if (allFiles.Error != null || allFiles.Data == null)
            {
                throw new Exception($"Error in CreateVectorFiles. {assistant.Name!} Unable to get files from service. {allFiles.Error?.Message}");
            }

            // Dictionary to store existing files
            var existingFiles = new Dictionary<string, string>();
            string assistantFilePrefix = GetFilePrefixFromName(assistant.Name!);

            // Add existing files with the same prefix to the dictionary
            foreach (var file in allFiles.Data)
            {
                if (file.FileName.StartsWith(assistantFilePrefix)) existingFiles[file.FileName] = file.Id!;
            }

            // Upload files if they do not already exist
            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                var destinationFileName = $"{assistantFilePrefix}{fileName}";

                if (!existingFiles.ContainsKey(destinationFileName))
                {
                    var content = await AssistantDefinitionFiles.GetFile(filePath);
                    var uploadedFile = await client.FileUpload(UploadFilePurposes.UploadFilePurpose.Assistants, content!, destinationFileName);

                    if (uploadedFile.Error != null)
                    {
                        throw new Exception($"Error in CreateVectorFiles. {assistant.Name!} Unable to upload {destinationFileName}. {allFiles.Error?.Message}");
                    }
                    else
                    {
                        existingFiles[destinationFileName] = uploadedFile.Id;
                    }
                }
            }

            // Create a batch for the uploaded files and associate them with the vector store
            var batch = await client.VectorStoreFiles.CreateVectorStoreFileBatch(vectorStoreId, new CreateVectorStoreFileBatchRequest() { FileIds = existingFiles.Values.ToList() });
            if (batch.Error != null)
            {
                throw new Exception($"Error in CreateVectorFiles. Failed to create file batch.");
            }

            // Wait for the batch process to complete
            while (batch.FileCounts is { InProgress: > 0 })
            {
                Trace.TraceInformation($"Running batch {batch.FileCounts.InProgress} in progress");
                await Task.Delay(10000);
                batch = await client.VectorStoreFiles.GetVectorStoreFileBatch(vectorStoreId, batch.Id);
            }

            // If any files failed, attempt to create the batch again
            if (batch.FileCounts is { Failed: > 0 })
            {
                batch = await client.VectorStoreFiles.CreateVectorStoreFileBatch(vectorStoreId, new CreateVectorStoreFileBatchRequest() { FileIds = existingFiles.Values.ToList() });
            }

            Trace.TraceInformation($"{batch.Id}");
        }

        /// <summary>
        /// Checks if the vector stores have completed their processing.
        /// </summary>
        /// <param name="vectorStores">A dictionary of vector store names and their IDs.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI. Can be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether all vector stores have completed processing.</returns>
        public static async Task<bool> CheckForVectorStoreCompletion(Dictionary<string, string?> vectorStores, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Get the OpenAI client using the provided Azure OpenAI configuration
            var client = GetOpenAiClient(azureOpenAiConfig);
            var stores = await client.ListVectorStores(new PaginationRequest { Limit = 25 });

            // Check if listing the stores resulted in an error
            if (stores.Error != null || stores.Data == null)
            {
                throw new Exception("Can't get stores");
            }

            // Check the status of each store to see if it's "completed"
            foreach (var storeId in vectorStores.Values)
            {
                var store = stores.Data.FirstOrDefault(o => o.Id == storeId);
                if (store == null || store.Status != "completed")
                {
                    System.Diagnostics.Trace.TraceInformation($"{storeId} not found or {store?.Status}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Generates a file prefix from the assistant name, replacing invalid filename characters.
        /// </summary>
        /// <param name="name">The name to be used as a prefix.</param>
        /// <returns>A safe filename prefix derived from the assistant name.</returns>
        static string GetFilePrefixFromName(string name)
        {
            // Get a list of invalid filename characters
            char[] invalidChars = Path.GetInvalidFileNameChars();

            // Remove or replace invalid characters
            string safeFilename = new string(
                name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()
            );

            return $"{safeFilename}_";
        }
    }
}