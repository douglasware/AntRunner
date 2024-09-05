using OpenAI.Managers;
using OpenAI;

namespace AntRunnerLib
{
    internal class ClientUtility
    {
        internal static OpenAIService GetOpenAIClient(AzureOpenAIConfig? azureOpenAIConfig)
        {
            return new OpenAIService(new OpenAiOptions()
            {
                ProviderType = ProviderType.Azure,
                ApiVersion = azureOpenAIConfig?.ApiVersion ?? "2024-05-01-preview",
                ResourceName = azureOpenAIConfig?.ResourceName,
                ApiKey = azureOpenAIConfig?.ApiKey ?? "",
                DeploymentId = azureOpenAIConfig?.DeploymentId ?? ""
            });
        }
    }
}
