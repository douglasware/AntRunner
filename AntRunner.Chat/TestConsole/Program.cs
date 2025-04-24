using Microsoft.Extensions.Configuration;
using AntRunner.Chat;

namespace TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            foreach (var setting in configuration.AsEnumerable())
            {
                if (!string.IsNullOrEmpty(setting.Value))
                {
                    Environment.SetEnvironmentVariable(setting.Key, setting.Value);
                }
            }

            var config = AzureOpenAiConfigFactory.Get();

            var result = await ChatRunner.RunThread(new() { AssistantName = "Python Ants", DeploymentId = "gpt-4.1-mini", Instructions = "What version of Python are you using" }, config);

            Console.WriteLine(result?.Dialog);
        }
    }
}
