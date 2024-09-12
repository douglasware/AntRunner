# AntRunner
A full set of .NET tools for creating and using Open AI and Azure Open AI Assistants

## Features
- Create and manage Open AI Assistants (ants) from files stored in a file system and in Azure Blob Storage
- Easy setup of file search with vector stores from files bundled with ant definitions
- Easy setup of code interpreter and code interpreter files bundled with ant definitions
- Download files from code interpreter outputs
- Define and call REST API endpoints automatically with auth headers and oauth tokens
- Define and call local .NET methods automatically as tool calls

## AntRunner Projects and Tools
- AntRunnerLib - The core library for creating and running ants, published on nuget.org
- AntRunnerFunctions - Azure Functions for running ants including a REST API and Durable Functions
- AntRunnerPowershell - A PowerShell module for managing and running ants

## Notebooks
[Get started with the sample notebooks](./Notebooks)

## Example
```csharp
var assistantRunOptions = new AssistantRunOptions() {
    AssistantName = "MsGraphUserProfile",
    Instructions = "What is my name?",
    OauthUserAccessToken = oAuthToken
};
var output = await AntRunnerLib.AssistantRunner.RunThread(assistantRunOptions, config);
Console.WriteLine(output.LastMessage)
```

```text
Your name is Doug Ware.
```
