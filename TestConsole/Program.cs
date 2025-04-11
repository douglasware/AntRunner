using AntRunnerLib;
using Microsoft.Extensions.Configuration;

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

            var result = await ChatRunner.RunThread(new() { AssistantName = "Diagram Ants Chat", DeploymentId = "o3-mini", Instructions = "Create an example of a JLaTeXMath Notation Diagram showing an acceleration integral of velocity and provide an explanation. Do not substitute an different type of diagram" }, config);

            Console.WriteLine(result.Dialog);
        }
    }
}
