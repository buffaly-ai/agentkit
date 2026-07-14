# Getting started with Buffaly Agent Kit

This guide builds a minimal console application that runs a real Agent Kit turn with a deterministic scripted provider and a ProtoScript tool. It requires no network access and no API keys. After that, it shows where to replace the scripted provider with a real `IChatClient`.

## Prerequisites

- .NET SDK `9.0.300` for this repository build. The intended release target is `net10.0`, but the current checked-in build targets `net9.0` because .NET 10 is not installed in the build environment.
- A shell that can run `dotnet`.
- Optional: a model provider package that exposes `Microsoft.Extensions.AI.IChatClient` when you replace the scripted client.

Verify the SDK:

```bash
dotnet --version
```

## Create a console project

```bash
mkdir AgentKitQuickStart
cd AgentKitQuickStart
dotnet new console --framework net9.0
```

When packages are published, install:

```bash
dotnet add package Buffaly.AgentKit --version 1.0.0
dotnet add package Buffaly.AgentKit.ProtoScript --version 1.0.0
```

Inside this repository, samples use project references instead of package references.

## Create a ProtoScript tool

Create `AgentTools/Project.pts`:

```protoscript
prototype AddNumbers
{
    function Execute(int a, int b): int
    {
        return a + b;
    }
}
```

Create `AgentTools/agentkit.json`:

```json
{
  "schemaVersion": 1,
  "projectFile": "Project.pts",
  "exports": [
    {
      "name": "add_numbers",
      "prototype": "AddNumbers",
      "method": "Execute",
      "description": "Add two integers.",
      "parameters": [
        { "name": "a", "type": "int", "required": true },
        { "name": "b", "type": "int", "required": true }
      ],
      "returnType": "int"
    }
  ]
}
```

The manifest is an explicit allowlist. Only exports in `agentkit.json` become model-visible tools.

## Add a no-network scripted chat client

The sample support project contains a reusable `ScriptedChatClient`, but a minimal local version looks like this:

```csharp
using Microsoft.Extensions.AI;

public sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_responses.Count == 0)
            throw new InvalidOperationException("No scripted response remains.");
        return Task.FromResult(_responses.Dequeue());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null)
        => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose() { }
}
```

## Run an Agent Kit turn

`Program.cs`:

```csharp
using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;

await using ProtoScriptToolSet tools =
    await ProtoScriptToolSet.LoadAsync("AgentTools/agentkit.json");

var chat = new ScriptedChatClient(
    new ChatResponse(new ChatMessage(ChatRole.Assistant,
    [
        new FunctionCallContent(
            "call-1",
            "add_numbers",
            new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 })
    ])),
    new ChatResponse(new ChatMessage(ChatRole.Assistant, "The answer is 5.")));

var events = new InMemoryAgentEventSink();
var runtime = new AgentKitRuntime(chat, tools.Tools, eventSink: events);

AgentConversation conversation = AgentConversation.Create();
conversation.AddSystemMessage("Use tools when needed.");

AgentTurnResult result = await runtime.RunTurnAsync(conversation, "Add 2 and 3.");

Console.WriteLine(result.FinalAnswer);

foreach (AgentEvent item in events.Events)
    Console.WriteLine($"{item.Sequence}: {item.Kind} {item.ToolName} {item.Message}");
```

Expected output includes `The answer is 5.` and events for turn start, model response, tool call, tool completion, and turn completion.

## Export and import conversation state

```csharp
string json = conversation.ExportState();
AgentConversation restored = AgentConversation.ImportState(json);
```

Use this when your application owns persistence. In ASP.NET Core, `JsonlAgentConversationStore` can persist conversations under `.agentkit/`.

## Replace the scripted client with a real provider

The only code that changes is the `IChatClient` registration/creation:

```csharp
using Microsoft.Extensions.AI;
using OpenAI;

IChatClient chat = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
    .AsChatClient("gpt-4.1-mini");
```

Provider package APIs differ by version; Agent Kit only requires `IChatClient`.

## Full repository examples

- Minimal console sample: `samples/AgentKit.Console`
- Headless domain sample: `samples/DevOps.IncidentInvestigation`
- Shared deterministic test client: `samples/Buffaly.AgentKit.SampleSupport`
