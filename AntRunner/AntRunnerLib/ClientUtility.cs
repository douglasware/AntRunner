using OpenAI.Managers;
using OpenAI;

namespace AntRunnerLib
{

    /// <summary>
    /// Utility class for managing the OpenAI client.
    /// </summary>
    public class ClientUtility
    {
        public static OpenAiService? Instance;
        public static AzureOpenAiConfig LastConfig = new();

        /// <summary>
        /// Gets the OpenAI client with the specified configuration.
        /// </summary>
        /// <param name="azureOpenAiConfig">The Azure OpenAI configuration.</param>
        /// <returns>The OpenAI client.</returns>
        public static OpenAiService GetOpenAiClient(AzureOpenAiConfig? azureOpenAiConfig)
        {
            if(LastConfig != azureOpenAiConfig)
            {
                Instance = null;
                LastConfig = azureOpenAiConfig ?? new();
            }

            Instance ??= new OpenAiService(new OpenAiOptions()
                {
                    ProviderType = ProviderType.Azure,
                    ApiVersion = azureOpenAiConfig?.ApiVersion ?? "2024-05-01-preview",
                    ResourceName = azureOpenAiConfig?.ResourceName,
                    ApiKey = azureOpenAiConfig?.ApiKey ?? "",
                    DeploymentId = azureOpenAiConfig?.DeploymentId ?? ""
                });
            return Instance;
        }
    }
}
