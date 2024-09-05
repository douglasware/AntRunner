using OpenAI.ObjectModels.RequestModels;
using static AntRunnerLib.ClientUtility;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using AntRunnerLib.AssistantDefinitions;
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
        /// <param name="azureOpenAIConfig">The configuration for Azure OpenAI. Can be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of uploaded file IDs.</returns>
        public static async Task<List<string>> CreateCodeInterpreterFiles(AssistantCreateRequest assistant, AzureOpenAIConfig? azureOpenAIConfig)
        {
            // Get the list of file paths from the CodeInterpreter folder based on the assistant name
            var filePaths = await AssistantDefinitionFiles.GetFilesInCodeInterpreterFolder(assistant.Name!);

            // Check if any file paths were found
            if (filePaths == null || filePaths.Count() == 0)
            {
                throw new Exception($"Error in CreateCodeInterpreterFiles. {assistant.Name!} no files found");
            }

            // Get the OpenAI client using the provided Azure OpenAI configuration
            var client = GetOpenAIClient(azureOpenAIConfig);

            List<string> files = new List<string>();
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

        public async Task<FileContentResponse<byte[]?>> RetrieveFileContent(string fileId, AzureOpenAIConfig? azureOpenAIConfig)
        {
            var client = GetOpenAIClient(azureOpenAIConfig);
            return await client.RetrieveFileContent<byte[]>(fileId);
        }
    }
}