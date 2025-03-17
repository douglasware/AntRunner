using AntRunnerLib;
using Microsoft.Extensions.Configuration;
using System.IO;
using static AntRunnerLib.ClientUtility;

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

Console.WriteLine(configuration["AZURE_OPENAI_RESOURCE"]);

var config = AzureOpenAiConfigFactory.Get();
var client = GetOpenAiClient(config);

var assistantId = await AssistantUtility.Create("LocalFileAnts", config);

//var assistantRunOptions = new AssistantRunOptions()
//{
//    AssistantName = "LocalFileAnts",
//    Instructions = "Display the contents of D:\\repos\\AntRunner\\TestConsole\\Program.cs",
//};
//var output = await AntRunnerLib.AssistantRunner.RunThread(assistantRunOptions, config);

//Console.WriteLine(output.Dialog);

var assistantRunOptions = new AssistantRunOptions()
{
    AssistantName = "LocalFileAnts",
    Instructions = "Read this file 'D:\\repos\\AntRunner\\TestConsole\\TestConsole.csproj' and add a package reference to AntRunnerLib, 0.9.6, and save a copy to e:\\temp\\",
};
var output = await AntRunnerLib.AssistantRunner.RunThread(assistantRunOptions, config);

if (output?.Status != "completed")
{
    Console.WriteLine($"Something went wrong. Status is {output?.Status}\n{output?.LastMessage}");
}
else
{
    Console.WriteLine(output.Dialog);
}

