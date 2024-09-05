namespace AntRunnerLib.AssistantDefinitions
{
    /// <summary>
    /// Provides methods for reading assistant definition files from the file system or storage.
    /// Files from storage by default must be located under ./Assistants where the "./" represents the execution folder.
    /// Set your files to "Copy Always" to copy them to the correct location(s) at build time.
    /// Set the "ASSISTANTS_BASE_FOLDER_PATH" environment variable to override the default location "./Assistants""
    /// </summary>
    public class AssistantDefinitionFiles
    {
        /// <summary>
        /// Reads the assistant definition JSON from the file system or storage.
        /// </summary>
        /// <param name="assistantName">The name of the assistant.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the assistant definition JSON.</returns>
        public static async Task<string?> GetManifest(string assistantName)
        {
            return EmbeddedResourceStorage.GetManifest(assistantName) ?? await FileStorage.GetManifest(assistantName) ?? await BlobStorage.GetManifest(assistantName);
        }

        /// <summary>
        /// Reads the assistant instructions from the file system or storage.
        /// </summary>
        /// <param name="assistantName">The name of the assistant.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the assistant instructions.</returns>
        public static async Task<string?> GetInstructions(string assistantName)
        {
            return EmbeddedResourceStorage.GetInstructions(assistantName) ?? await FileStorage.GetInstructions(assistantName) ?? await BlobStorage.GetInstructions(assistantName);
        }

        /// <summary>
        /// Reads the assistant action authorization from the file system or storage.
        /// </summary>
        /// <param name="assistantName">The name of the assistant.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the assistant action authorization.</returns>
        public static async Task<string?> GetActionAuth(string assistantName)
        {
            return await FileStorage.GetActionAuth(assistantName) ?? await BlobStorage.GetActionAuth(assistantName);
        }

        /// <summary>
        /// Retrieves the list of files in the OpenAPI folder for the specified assistant from the file system or storage.
        /// </summary>
        /// <param name="assistantName">The name of the assistant.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the list of files in the OpenAPI folder.</returns>
        public static async Task<IEnumerable<string>?> GetFilesInOpenAPIFolder(string assistantName)
        {
            return FileStorage.GetFilesInOpenAPIFolder(assistantName) ?? await BlobStorage.GetFilesInOpenAPIFolder(assistantName);
        }

        /// <summary>
        /// Retrieves a file from the file system or storage.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the byte array of the file content.</returns>
        public static async Task<byte[]?> GetFile(string filePath)
        {
            return await FileStorage.GetFile(filePath) ?? await BlobStorage.GetFile(filePath);
        }

        /// <summary>
        /// Retrieves the list of files in the vector store folder for the specified assistant and vector store name from the file system or storage.
        /// </summary>
        /// <param name="assistantName">The name of the assistant.</param>
        /// <param name="vectorStoreName">The name of the vector store.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the list of files in the vector store folder.</returns>
        public static async Task<IEnumerable<string>?> GetFilesInVectorStoreFolder(string assistantName, string vectorStoreName)
        {
            return FileStorage.GetFilesInVectorStoreFolder(assistantName, vectorStoreName) ?? await BlobStorage.GetFilesInVectorStoreFolder(assistantName, vectorStoreName);
        }

        /// <summary>
        /// Retrieves the list of files in the code interpreter folder for the specified assistant from the file system or storage.
        /// </summary>
        /// <param name="assistantName">The name of the assistant.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the list of files in the code interpreter folder.</returns>
        public static async Task<IEnumerable<string>?> GetFilesInCodeInterpreterFolder(string assistantName)
        {
            return FileStorage.GetFilesInCodeInterpreterFolder(assistantName) ?? await BlobStorage.GetFilesInCodeInterpreterFolder(assistantName);
        }
    }
}