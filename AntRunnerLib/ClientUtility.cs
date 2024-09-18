using OpenAI.Managers;
using OpenAI;

namespace AntRunnerLib
{

    /// <summary>
    /// Utility class for managing the OpenAI client.
    /// </summary>
    public class ClientUtility
    {
        public static OpenAIService? _instance;
        public static AzureOpenAIConfig _lastConfig = new();

        /// <summary>
        /// Gets the OpenAI client with the specified configuration.
        /// </summary>
        /// <param name="azureOpenAIConfig">The Azure OpenAI configuration.</param>
        /// <returns>The OpenAI client.</returns>
        public static OpenAIService GetOpenAIClient(AzureOpenAIConfig? azureOpenAIConfig)
        {
            if(_lastConfig != azureOpenAIConfig)
            {
                _instance = null;
                _lastConfig = azureOpenAIConfig ?? new();
            }

            _instance ??= new OpenAIService(new OpenAiOptions()
                {
                    ProviderType = ProviderType.Azure,
                    ApiVersion = azureOpenAIConfig?.ApiVersion ?? "2024-05-01-preview",
                    ResourceName = azureOpenAIConfig?.ResourceName,
                    ApiKey = azureOpenAIConfig?.ApiKey ?? "",
                    DeploymentId = azureOpenAIConfig?.DeploymentId ?? ""
                });
            return _instance;
        }
    }
}
