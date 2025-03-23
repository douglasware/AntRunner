using AntRunnerLib;
using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text;
using static AntRunnerLib.ClientUtility;

await BashTest();

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

string command;
string arguments;

if (Environment.OSVersion.Platform == PlatformID.Win32NT)
{
    command = "cmd.exe";
    arguments = "/c set";
}
else
{
    command = "printenv";
    arguments = string.Empty;
}

command = "powershell.exe";
arguments = "-Command \"$profile\nGet-ChildItem Env: | Format-List\"";

var stdOutBuffer = new StringBuilder();
var stdErrBuffer = new StringBuilder();

var result = await Cli.Wrap(command)
    .WithArguments(arguments)
    .WithStandardOutputPipe(PipeTarget.ToDelegate(line => stdOutBuffer.AppendLine(line)))
    .WithStandardErrorPipe(PipeTarget.ToDelegate(line => stdErrBuffer.AppendLine(line)))
    .WithValidation(CommandResultValidation.None)
    .ExecuteAsync();

Console.WriteLine(stdOutBuffer.ToString());
if (result.ExitCode != 0)
{
    Console.WriteLine("Error:");
    Console.WriteLine(stdErrBuffer.ToString());
}

var config = AzureOpenAiConfigFactory.Get();
var client = GetOpenAiClient(config);

var assistantId = await AssistantUtility.Create("LocalFileAnts", config);

//var assistantRunOptions = new AssistantRunOptions()
//{
//    AssistantName = "LocalFileAnts",
//    Instructions = "Display the contents of C:\\Users\\dougl\\OneDrive\\Pictures\\Screenshots\\2022-04-09.png as a base64 url",
//};
//var output = await AntRunnerLib.AssistantRunner.RunThread(assistantRunOptions, config);

//Console.WriteLine(output.Dialog);

var assistantRunOptions = new AssistantRunOptions()
{
    AssistantName = "LocalFileAnts",
    Instructions = "Read this file 'D:\\repos\\AntRunner\\TestConsole\\TestConsole.csproj' and add a package reference to AntRunnerLib, 0.9.6, and save a copy to e:\\temp\\",
};
var output = await AntRunnerLib.AssistantRunner.RunThread(assistantRunOptions, config);

//var assistantRunOptions = new AssistantRunOptions()
//{
//    AssistantName = "LocalFileAnts",
//    //Instructions = "Your working folder is e:\\temp\\. Write an execute a python script to write an image of a sign wave to the working folder.",
//    Instructions = "Use the console and ping google.com",
//};
//var output = await AntRunnerLib.AssistantRunner.RunThread(assistantRunOptions, config);


if (output?.Status != "completed")
{
    Console.WriteLine($"Something went wrong. Status is {output?.Status}\n{output?.LastMessage}");
}
else
{
    Console.WriteLine(output.Dialog);
}

static async Task BashTest()
{
    var script = @"
printenv
uname -a
cat /etc/os-release
dpkg -l
lsb_release -a
";

    //await ExecuteBashScript(script);
    await ExecuteDockerScript(script, "fromappconfigcontainer_container");
}

static async Task ExecuteBashScript(string script)
{
    // Ensure the script uses Unix-style line endings
    script = script.Replace("\r\n", "\n").Replace("\r", "\n");

    var stdOutBuffer = new StringBuilder();
    var stdErrBuffer = new StringBuilder();

    try
    {
        stdOutBuffer.AppendLine("Running combined bash script...");

        var result = await Cli.Wrap("bash")
            .WithArguments($"-c \"{script}\"")
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => stdOutBuffer.AppendLine(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => stdErrBuffer.AppendLine(line)))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();
    }
    catch (Exception ex)
    {
        stdErrBuffer.AppendLine("Error running combined bash script:");
        stdErrBuffer.AppendLine(ex.ToString());
    }

    // Display the output
    Console.WriteLine("Environment Information:");
    Console.WriteLine(stdOutBuffer.ToString());
    if (stdErrBuffer.Length > 0)
    {
        Console.WriteLine("Errors:");
        Console.WriteLine(stdErrBuffer.ToString());
    }
}

static async Task ExecuteDockerScript(string script, string containerName)
{
    // Ensure the script uses Unix-style line endings
    script = script.Replace("\r\n", "\n").Replace("\r", "\n");

    var stdOutBuffer = new StringBuilder();
    var stdErrBuffer = new StringBuilder();

    try
    {
        stdOutBuffer.AppendLine($"Running script in Docker container {containerName}...");

        var result = await Cli.Wrap("docker")
            .WithArguments($"exec {containerName} bash -c \"{script}\"")
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => stdOutBuffer.AppendLine(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => stdErrBuffer.AppendLine(line)))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();
    }
    catch (Exception ex)
    {
        stdErrBuffer.AppendLine($"Error running script in Docker container {containerName}:");
        stdErrBuffer.AppendLine(ex.ToString());
    }

    // Display the output
    Console.WriteLine($"Environment Information for Docker container {containerName}:");
    Console.WriteLine(stdOutBuffer.ToString());
    if (stdErrBuffer.Length > 0)
    {
        Console.WriteLine("Errors:");
        Console.WriteLine(stdErrBuffer.ToString());
    }
}