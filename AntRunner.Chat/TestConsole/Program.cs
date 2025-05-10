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

            var httpClient = new HttpClient() { Timeout = new TimeSpan(0, 5, 0) };

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

            var conversation = await Conversation.Create(chatRunOptions, config, httpClient);
            Console.WriteLine(DateTime.Now.ToLongTimeString());
            var result = await conversation.Chat("Extract the audio from https://youtu.be/t16LK7gk9SY and save it to ../content\nYou will need to install one or more packages to complete the task. Do not give up if you get an error. Instead, correct yourself and proceed.");
            //https://youtu.be/WT8t3i8CkMQ
            Console.WriteLine(DateTime.Now.ToLongTimeString());
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

            //Console.WriteLine(conversation2.Usage.TotalTokens);
            Console.WriteLine(conversation2.LastResponse?.LastMessage);

            Console.WriteLine(conversation2.LastResponse?.Dialog);
        }
    }
}
