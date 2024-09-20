using AntRunnerLib.AssistantDefinitions;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using static AntRunnerLib.ClientUtility;

namespace AntRunnerLib
{
    /// <summary>
    /// These are the same independent of the tool, ie. code_interpreter and file_search are the same.
    /// Use these methods for files added as attachments to messages.
    /// </summary>
    public class Files
    {
        /// <summary>
        /// Uploads a list of files to OpenAI storage and returns a list of uploaded file IDs.
        /// </summary>
        /// <param name="filePaths">The list of file paths to upload.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI. Can be null.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a list of uploaded file IDs.</returns>
        /// <exception cref="ArgumentNullException">Thrown when filePaths is null.</exception>
        /// <exception cref="Exception">Thrown when an error occurs during file upload.</exception>
        public static async Task<List<string>> UploadFiles(List<string> filePaths, AzureOpenAiConfig? azureOpenAiConfig)
        {
            if (filePaths == null)
            {
                throw new ArgumentNullException(nameof(filePaths), "File paths cannot be null.");
            }

            if (!filePaths.Any())
            {
                throw new Exception("No files provided for upload.");
            }

            var client = GetOpenAiClient(azureOpenAiConfig);

            var uploadedFileIds = new List<string>();

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                var destinationFileName = fileName;

                var content = await FileStorage.GetFile(filePath);

                if (content == null)
                {
                    throw new Exception($"Error reading file content for {filePath}");
                }

                var uploadedFile = await client.FileUpload(UploadFilePurposes.UploadFilePurpose.Assistants, content, destinationFileName);

                if (uploadedFile.Error != null)
                {
                    throw new Exception($"Error in UploadFiles. Unable to upload {destinationFileName}: {uploadedFile.Error.Message}");
                }
                else
                {
                    uploadedFileIds.Add(uploadedFile.Id);
                }
            }

            return uploadedFileIds;
        }

        /// <summary>
        /// Deletes a list of files from OpenAI storage.
        /// </summary>
        /// <param name="fileIds">The list of file IDs to delete.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI. Can be null.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when fileIds is null.</exception>
        /// <exception cref="Exception">Thrown when an error occurs during file deletion.</exception>
        public static async Task DeleteFiles(List<string> fileIds, AzureOpenAiConfig? azureOpenAiConfig)
        {
            if (fileIds == null)
            {
                throw new ArgumentNullException(nameof(fileIds), "File IDs cannot be null.");
            }

            if (!fileIds.Any())
            {
                throw new Exception("No file IDs provided for deletion.");
            }

            var client = GetOpenAiClient(azureOpenAiConfig);

            foreach (var fileId in fileIds)
            {
                var deleteResponse = await client.DeleteFile(fileId);

                if (deleteResponse.Error != null)
                {
                    throw new Exception($"Error in DeleteFiles. Unable to delete file with ID {fileId}: {deleteResponse.Error.Message}");
                }
            }
        }
    }
}