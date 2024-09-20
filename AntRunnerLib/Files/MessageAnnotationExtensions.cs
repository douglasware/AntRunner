using OpenAI.ObjectModels.SharedModels;
using static AntRunnerLib.ClientUtility;

namespace AntRunnerLib
{
    /// <summary>
    /// Provides extension methods for the MessageAnnotation class.
    /// </summary>
    public static class MessageAnnotationExtensions
    {
        /// <summary>
        /// Downloads a file from OpenAI storage and saves it to the specified path.
        /// </summary>
        /// <param name="messageAnnotation">The MessageAnnotation object.</param>
        /// <param name="path">The path where the file should be saved.</param>
        /// <param name="azureOpenAiConfig">The configuration for Azure OpenAI.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public static async Task DownloadFile(this MessageAnnotation messageAnnotation, string path, AzureOpenAiConfig azureOpenAiConfig)
        {
            if (messageAnnotation.FilePathAnnotation == null || messageAnnotation.Type != "file_path")
            {
                return;
            }
            await CodeInterpreterFiles.DownloadFile(messageAnnotation.FilePathAnnotation.FileId, path, azureOpenAiConfig);
        }

        /// <summary>
        /// Returns the file name from the MessageAnnotation object without 'sandbox:/mnt/data/'
        /// </summary>
        /// <param name="messageAnnotation">The MessageAnnotation object.</param>
        /// <param name="azureOpenAiConfig"></param>
        /// <returns>The file name without the prefix.</returns>
        public static async Task<string> GetFileName(this MessageAnnotation messageAnnotation, AzureOpenAiConfig azureOpenAiConfig)
        {
            if (messageAnnotation is { FileCitation: not null, Type: "file_citation" })
            {
                var client = GetOpenAiClient(azureOpenAiConfig);
                return await client.RetrieveFile(messageAnnotation.FileCitation.FileId).ContinueWith(t => t.Result.FileName);
            }
            return messageAnnotation.Text?.Replace("sandbox:/mnt/data/", "") ?? string.Empty;
        }
    }
}
