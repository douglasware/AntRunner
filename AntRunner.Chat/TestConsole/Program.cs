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


            var chatRunOptions = new ChatRunOptions() { AssistantName = "Python Ants", DeploymentId = "gpt-4.1-mini", Instructions = "What version of Python are you using" };

            //var result = await ChatRunner.RunThread(chatRunOptions, config);

            var conversation = await Conversation.Create(chatRunOptions, config);
            var result = await conversation.Chat("What is today's date?");
            Console.WriteLine(conversation.LastResponse?.LastMessage);
            Console.WriteLine(conversation.Usage.TotalTokens);
            await conversation.ChangeAssistant("Web Ants");
            result = await conversation.Chat("What is there to do next week in Johns Creek, GA?");
            Console.WriteLine(conversation.LastResponse?.LastMessage);
            Console.WriteLine(conversation.Usage.TotalTokens);
            conversation.Undo();
            Console.WriteLine(conversation.Usage.TotalTokens);
            Console.WriteLine(conversation.LastResponse?.LastMessage);
            result = await conversation.Chat("What is there to do next week in Suwanee, GA?");
            Console.WriteLine(conversation.Usage.TotalTokens);
            Console.WriteLine(conversation.LastResponse?.LastMessage);

            Console.WriteLine(conversation.LastResponse?.Dialog);

            conversation.Save(@"e:\suwaneeactivities.json");
            var conversation2 = Conversation.Create(@"e:\suwaneeactivities.json", config);
            result = await conversation2.Chat("What is there to do the week after that?");

            Console.WriteLine(conversation2.Usage.TotalTokens);
            Console.WriteLine(conversation2.LastResponse?.LastMessage);

            Console.WriteLine(conversation2.LastResponse?.Dialog);
        }
    }
}
