using AntRunner.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            // Set environment variables
            foreach (var setting in configuration.AsEnumerable())
            {
                if (!string.IsNullOrEmpty(setting.Value))
                {
                    Environment.SetEnvironmentVariable(setting.Key, setting.Value);
                }
            }

            // Set up the dependency injection container
            var serviceProvider = new ServiceCollection()
                .AddSingleton<HttpClient>(sp =>
                {
                    return new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
                })
                .AddSingleton<ConversationWorker>()
                .BuildServiceProvider();

            // Resolve the worker and execute the conversation logic
            var worker = serviceProvider.GetService<ConversationWorker>();
            await worker!.RunConversationAsync();
        }
    }

    public class ConversationWorker
    {
        private readonly HttpClient _httpClient;

        public ConversationWorker(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task RunConversationAsync()
        {
            var config = AzureOpenAiConfigFactory.Get();
            var chatRunOptions = new ChatRunOptions
            {
                AssistantName = "LocalCodeTool",
                DeploymentId = "gpt-4.1-mini",
                Instructions = "Use both of your tools to answer these two questions: What is the date and what is eleven plus negative 42?"
            };

            var result = await ChatRunner.RunThread(
                chatRunOptions,
                config,
                previousMessages: null,
                httpClient: _httpClient,
                messageAdded: Conversation_MessageAdded);

            if (result != null)
            {
                Console.WriteLine($"Last Message: {result.LastMessage}");
                Console.WriteLine($"Total Tokens: {result.Usage?.TotalTokens}");
            }
        }

        private void Conversation_MessageAdded(object? sender, MessageAddedEventArgs e)
        {
            Console.WriteLine($"{e.Role}: {e.Message}");
            Console.WriteLine();
        }
    }
}
