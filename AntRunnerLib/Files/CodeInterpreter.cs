using AntRunnerLib.AssistantDefinitions;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.ResponseModels.FileResponseModels;

namespace AntRunnerLib
{
    /// <summary>
    /// Provides methods to create code interpreter files for an assistant.
    /// </summary>
    public class CodeInterpreterFiles
    {
        /// <summary>
        /// Creates code interpreter files for a given assistant by uploading them to an OpenAI storage.
        /// </summary>
        /// <param name="assistant">The AssistantCreateRequest object containing assistant details.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI. Can be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of uploaded file IDs.</returns>
        public static async Task<List<string>?> CreateCodeInterpreterFiles(AssistantCreateRequest assistant, AzureOpenAiConfig? azureOpenAiConfig)
        {
            // Get the list of file paths from the CodeInterpreter folder based on the assistant name
            var filePaths = await AssistantDefinitionFiles.GetFilesInCodeInterpreterFolder(assistant.Name!);

            // Check if any file paths were found
            if (filePaths != null)
            {
                if (filePaths == null || !filePaths.Any())
                {
                    throw new Exception($"Error in CreateCodeInterpreterFiles. {assistant.Name!} no files found");
                }

                // Get the OpenAI client using the provided Azure OpenAI configuration
                var client = GetOpenAiClient(azureOpenAiConfig);

                List<string> files = [];
                foreach (var filePath in filePaths)
                {
                    var fileName = Path.GetFileName(filePath);
                    var destinationFileName = fileName;

                    // Read the content of the file
                    var content = await AssistantDefinitionFiles.GetFile(filePath);

                    // Upload the file content to OpenAI storage
                    var uploadedFile = await client.FileUpload(UploadFilePurposes.UploadFilePurpose.Assistants, content!, destinationFileName);

                    // Check if there was an error during upload
                    if (uploadedFile.Error != null)
                    {
                        throw new Exception($"Error in CreateCodeInterpreterFiles. {assistant.Name!} Unable to upload {destinationFileName}");
                    }
                    else
                    {
                        // Add the uploaded file ID to the list
                        files.Add(uploadedFile.Id);
                    }
                }
                // Return the list of uploaded file IDs
                return files;
            }
            return null;
        }

        /// <summary>
        /// Retrieves the content of a file from OpenAI storage.
        /// </summary>
        /// <param name="fileId">The ID of the file to retrieve.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the file content.</returns>
        public static async Task<FileContentResponse<byte[]?>> RetrieveFileContent(string fileId, AzureOpenAiConfig azureOpenAiConfig)
        {
            var client = GetOpenAiClient(azureOpenAiConfig);
            return await client.RetrieveFileContent<byte[]>(fileId);
        }

        /// <summary>
        /// Downloads a file from OpenAI storage and saves it to the specified path.
        /// </summary>
        /// <param name="fileId">The ID of the file to download.</param>
        /// <param name="path">The path where the file should be saved.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task DownloadFile(string fileId, string path, AzureOpenAiConfig azureOpenAiConfig)
        {
            var client = GetOpenAiClient(azureOpenAiConfig);
            var fileContent = await client.RetrieveFileContent<byte[]>(fileId);
            if (fileContent is { Successful: true, Content: not null })
            {
                await File.WriteAllBytesAsync(path, fileContent.Content);
            }
            else
            {
                throw new Exception($"Error downloading file with ID: {fileId}");
            }
        }
    }
}
