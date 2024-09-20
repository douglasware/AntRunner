namespace AntRunnerLib
{

    /// <summary>
    /// Gets the configuration settings for connecting to the Azure OpenAI service.
    /// </summary>
    public class AzureOpenAiConfigFactory
    {
        private AzureOpenAiConfigFactory() { }

        private static readonly AzureOpenAiConfig? AzureOpenAiConfig;

        /// <summary>
        /// Private constructor that initializes the configuration by reading environment variables.
        /// </summary>
        static AzureOpenAiConfigFactory()
        {
            AzureOpenAiConfig = new AzureOpenAiConfig
            {
                ResourceName = Environment.GetEnvironmentVariable("AZURE_OPENAI_RESOURCE"),
                ApiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY"),
                ApiVersion = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_VERSION"),
                DeploymentId = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"),
            };
        }

        /// <summary>
        /// Gets an instance of the <see cref="AntRunnerLib.AzureOpenAiConfig"/> class.
        /// </summary>
        /// <returns>A new instance of <see cref="AntRunnerLib.AzureOpenAiConfig"/>.</returns>
#pragma warning disable CS8603 // Possible null reference return.
        public static AzureOpenAiConfig Get() { return AzureOpenAiConfig; } // Clearly set in constructor!
#pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>
    /// Represents the configuration settings for connecting to the Azure OpenAI service.
    /// </summary>
    public record AzureOpenAiConfig
    {
        /// <summary>
        /// The name of an Azure OpenAI Service
        /// </summary>
        public string? ResourceName { set; get; }

        /// <summary>
        /// The API key for the Azure OpenAI service.
        /// </summary>
        public string? ApiKey { set; get; }

        /// <summary>
        /// A valid API Version
        /// See https://learn.microsoft.com/en-us/azure/ai-services/openai/reference
        /// </summary>
        public string? ApiVersion { get; set; }

        /// <summary>
        /// The model e.g. "GPT-4o"
        /// </summary>
        public string? DeploymentId { get; set; }
    }
}