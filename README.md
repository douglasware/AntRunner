# AntRunner.Chat

AntRunner.Chat is a .NET library that lets you easily create and manage conversations with tool-based AI assistants. These AI agents can help you by answering questions, running tools, or performing tasks all through a simple chat interface.

## What can you do with AntRunner.Chat?

- Converse naturally with AI assistants powered by OpenAI models like GPT-5, gpt-4.1-mini, and reasoning models (o1, o3, o4).
- **Streaming responses** - receive assistant responses in real-time as they're generated via `StreamingMessageProgressEventHandler`.
- **Intelligent model parameters** - automatically adapts between `temperature`/`topP` for standard models and `reasoning_effort` for thinking models, allowing the same assistant definition to work with both model types.
- Manage multi-turn conversations with context.
- Switch between different AI assistants on the fly.
- Undo the last message if you want to change something.
- Save and load conversations to keep your chat history.
- Track token usage to monitor your API consumption.
- **External tool call support** - delegate tool execution to client applications for client-handled tools via `ExternalToolCallEventHandler`.
- **Multi-provider OAuth** - pass external auth tokens for multiple providers (e.g., Microsoft Graph, GitHub) via `ExternalAuthTokens`.

## Getting Started

### Install the NuGet Package

You can add AntRunner.Chat to your .NET project using the NuGet package manager:

```bash
dotnet add package AntRunner.Chat --version 0.9.4
```

Or, add this directive in your C# notebook or script:

```csharp
#r "nuget: AntRunner.Chat, 0.9.4"
```

## Basic Usage Example

Here is a simple example showing how to start a conversation with an AI assistant and send messages:

```csharp
using System.Threading.Tasks;
using AntRunner.Chat;

// Load your environment variables and AI service configuration (example method)
var envVariables = Settings.GetEnvironmentVariables();
foreach (var kvp in envVariables)
{
    Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
}

var config = AzureOpenAiConfigFactory.Get();

// Set up the chat options with assistant name and model deployment
static ChatRunOptions chatConfiguration = new()
{
    AssistantName = "Python Ants",
    DeploymentId = "gpt-4.1-mini",
};

// Create a new conversation instance
var conversation = await Conversation.Create(chatConfiguration, config);

// Convenience method to send a message and get a response
async Task<ChatRunOutput> Chat(string message)
{
    chatConfiguration.Instructions = message;
    var runnerOutput = await conversation.Chat(message);
    runnerOutput.LastMessage.DisplayAs("text/markdown");
    return runnerOutput;
}

// Example usage: send a message
var output = await Chat("Hello AI! What can you do?");
```

## More Features

### Streaming Responses

You can receive assistant responses in real-time using streaming:

```csharp
// Subscribe to streaming events
void OnStreamProgress(object? sender, StreamingMessageProgressEventArgs e)
{
    Console.Write(e.ContentDelta); // Write each chunk as it arrives
}

// Use ChatRunner.RunThread with streaming handler
var output = await ChatRunner.RunThread(
    chatConfiguration,
    serviceConfiguration,
    messages,
    httpClient,
    onMessage: null,
    onStream: OnStreamProgress);
```

### Reasoning Models Support

AntRunner.Chat automatically detects reasoning models (o1, o3, o4, gpt-5) and uses the appropriate parameters:
- For standard models: uses `temperature` and `topP` from assistant definition
- For reasoning models: uses `reasoning_effort` instead

This allows the same assistant definition to work seamlessly with both model types.

### Continue the Conversation

You can keep chatting by sending more messages:

```csharp
var output = await Chat("Tell me about the prerequisites for setting up .NET 8 SDK and Docker.");
```

### Undo the Last Message

If you want to remove the last message from the conversation:

```csharp
conversation.Undo();
```

### Switch Assistants

Change to a different AI assistant anytime:

```csharp
await conversation.ChangeAssistant("Web Ants");
var output = await Chat("What events are happening next week in my city?");
```

### Save and Load Conversations

Save your conversation to a file:

```csharp
conversation.Save("./savedConversation.json");
```

Load a conversation from a saved file:

```csharp
var conversation2 = Conversation.Create(@"./savedConversation.json", AzureOpenAiConfigFactory.Get());
```

### View Conversation and Usage Stats

See the last message or the full dialog:

```csharp
var lastMessage = conversation.LastResponse.LastMessage;
var fullDialog = conversation.LastResponse.Dialog;
```

Check token usage for the entire conversation or a single turn:

```csharp
var usage = conversation.Usage;
var lastTurnUsage = output.Usage;
```

---

## Multi-Agent Orchestration (Crew Bridge)

AntRunner.Chat supports multi-agent workflows where a lead assistant can delegate tasks to specialized crew member assistants. This is powered by the `CrewBridgeSchemaGenerator` from AntRunner.ToolCalling.

### How It Works

1. Define a set of specialized assistants (e.g., ResearchAgent, WriterAgent, ReviewerAgent)
2. Generate a crew bridge schema that exposes each assistant as a callable tool
3. The lead assistant can invoke crew members via `Agent.Invoke` to delegate subtasks

```csharp
using AntRunner.ToolCalling.Functions;

// Generate schema for crew members
var crewSchema = CrewBridgeSchemaGenerator.GetSchema(new[]
{
    "ResearchAgent",
    "WriterAgent",
    "ReviewerAgent"
});

// The lead assistant can now call these as tools during conversation
```

This enables complex workflows like:
- A project manager assistant coordinating research, writing, and review tasks
- A coding assistant delegating to specialized testing or documentation assistants
- A customer service lead routing requests to domain-specific experts

---

## Tool Calling with AntRunner.ToolCalling

AntRunner.Chat uses **AntRunner.ToolCalling** for all tool execution. Tools can be defined in multiple ways:

### OpenAPI-Based Tools
Define tools using OpenAPI specifications for web APIs. The library handles request building, authentication, and response processing automatically.

### Declarative Tool Registration
Use attributes to register local functions as tools without writing JSON schemas:

```csharp
using AntRunner.ToolCalling.Attributes;

[Tool(OperationId = "search_files", Summary = "Search for files by pattern")]
public static async Task<string> SearchFiles(
    [Parameter(Description = "The search pattern (e.g., *.cs)")] string pattern,
    [Parameter(Description = "Directory to search in")] string directory = ".")
{
    // Implementation
}
```

### Available Tool Attributes

| Attribute | Description |
|-----------|-------------|
| `[Tool]` | Marks a method as a callable tool with OperationId and Summary |
| `[Parameter]` | Provides description and metadata for tool parameters |
| `[RequiresNotebookContext]` | Indicates the tool needs notebook context injection |
| `[OAuth]` | Specifies OAuth token handling (None, Forward, Required) |

The `ToolContractRegistry` automatically discovers annotated methods and generates OpenAPI schemas at runtime.

---

## Container Sandboxes

A major feature of AntRunner.Chat is the use of container sandboxes to provide isolated environments for different AI assistants and tools. These sandboxes are implemented as Docker containers, enabling you to run services such as the .NET server, Python environments with or without CUDA support, PlantUML, and more.

### Available Containers

- **dotnet-server**: The main .NET service container.
- **python-app**: Python 3.11 environment with .NET 9 SDK and optional CUDA support.
- **plantuml**: Container for PlantUML diagram rendering.
- **qdrant**: Vector search engine container.
- **kernel-memory**: Custom service container for kernel memory management.

### Setup Guide and Build Scripts

To help you get started with these containers, we provide a comprehensive [Setup Guide](./AntRunner.Chat/setup_guide.md) that covers all prerequisites and detailed instructions for building the local Docker images.

You can use the provided build scripts:

- `build_local_images.sh` for Linux/macOS
- `build_local_images.ps1` for Windows PowerShell

These scripts will prompt you to select whether to build the CPU-only or CUDA-enabled Python images and will build all necessary images in the correct order.

Make sure to follow the setup guide to prepare your environment and build the containers before running the solution.

### Sample Notebooks

After setup, you run [new-chat-project](AntRunner.Chat/new-chat-project.ps1) to create a new project with starter assistant definitions and [Project Template Sample Notebooks](AntRunner.Chat/ProjectTemplate/Notebooks/1-Template.ipynb)

If you have questions or want to contribute, feel free to open an issue or pull request!
