# AgentKit.AspNetCore — Simplest Web Sample

## What this sample does

This is the simplest ASP.NET Core sample for Agent Kit. It hosts Agent Kit in a web application with a chat endpoint and an inspector page. When you send a message to the chat endpoint, the agent responds. The "AI" is scripted (no API key needed), but the full Agent Kit pipeline runs: message in, agent processes, response out.

Think of it as a "hello world" for embedding Agent Kit in a web app. If you understand this sample, you understand how to add Agent Kit to any ASP.NET Core application.

## Prerequisites

- .NET SDK 9.0.300 (run `dotnet --version` to check)
- A terminal (PowerShell, cmd, or bash)
- Optional: `curl` or a web browser for testing endpoints
- No API key, no internet, no database — everything runs locally

## How to run it

From the repository root:

```bash
dotnet run --project samples/AgentKit.AspNetCore --urls http://127.0.0.1:5128
```

You should see:

```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://127.0.0.1:5128
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

The server is now running. Open a browser to `http://127.0.0.1:5128/agentkit/` to see the inspector page, or use the API endpoints below.

## Testing the endpoints

### 1. Health check

```bash
curl http://127.0.0.1:5128/agentkit/health
```

Expected response:

```json
{"status":"ok"}
```

This confirms Agent Kit is running and healthy.

### 2. Inspector page

Open `http://127.0.0.1:5128/agentkit/` in a browser. You'll see a minimal inspector page with conversation and message UI controls. This is a built-in debugging tool that lets you interact with the agent from a browser.

### 3. Create a conversation

```bash
curl -X POST http://127.0.0.1:5128/agentkit/api/conversations \
  -H "Content-Type: application/json" \
  -d '{"systemPrompt":"You are testing Agent Kit."}'
```

Expected response: a conversation ID (a UUID string). The conversation is now stored in the local JSONL store.

### 4. List conversations

```bash
curl http://127.0.0.1:5128/agentkit/api/conversations
```

Returns a JSON array of stored conversation IDs.

### 5. Run a turn

```bash
curl -X POST http://127.0.0.1:5128/agentkit/api/conversations/{id}/turns \
  -H "Content-Type: application/json" \
  -d '{"message":"Say hello and use any tools if needed."}'
```

Replace `{id}` with the conversation ID from step 3. Expected response:

```json
{
  "stopReason": 0,
  "finalAnswer": "Hello from Agent Kit.",
  "rounds": 1,
  "messages": [...]
}
```

The agent received your message, processed it through the scripted client, and returned "Hello from Agent Kit."

### 6. Get conversation history

```bash
curl http://127.0.0.1:5128/agentkit/api/conversations/{id}
```

Returns the full conversation state including all messages.

### 7. Get events

```bash
curl http://127.0.0.1:5128/agentkit/api/conversations/{id}/events
```

Returns events that were emitted during the conversation (turn started, model response, etc.).

## Understanding the code

Let's walk through `Program.cs` line by line.

### Step 1: Create the web app builder

```csharp
var builder = WebApplication.CreateBuilder(args);
```

This is standard ASP.NET Core. It creates a builder that you use to register services and configure the app.

### Step 2: Register the chat client

```csharp
builder.Services.AddSingleton<IChatClient>(new ScriptedChatClient(
    new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello from Agent Kit."))));
```

This registers a scripted `IChatClient` as a singleton service. Every time the agent needs to talk to the "AI model," it gets this client, which always responds with "Hello from Agent Kit."

**What is IChatClient?** It's an interface from `Microsoft.Extensions.AI` that represents "something you can chat with." It could be OpenAI, Anthropic, a local model, or — as here — a scripted test double. Agent Kit doesn't care which one you use.

**What is dependency injection (DI)?** ASP.NET Core uses DI to share services across your app. When you register `IChatClient` here, any part of the app that needs it (including Agent Kit) gets the same instance. This makes it easy to swap implementations.

### Step 3: Register Agent Kit services

```csharp
builder.Services.AddBuffalyAgentKit(agentKit =>
{
    string root = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", ".."));
    agentKit.UseJsonlStore(Path.Combine(root, "data"));
    agentKit.AddProtoScriptTools(Path.Combine(root, "Tools", "agentkit.json"));
});
```

`AddBuffalyAgentKit` registers all the Agent Kit services needed for ASP.NET Core hosting:
- **`UseJsonlStore`** — tells Agent Kit to save conversations as JSONL files in the specified directory. This is how conversations persist between requests.
- **`AddProtoScriptTools`** — loads ProtoScript tools from the manifest file. These become tools the AI agent can call.

### Step 4: Build the app and map endpoints

```csharp
var app = builder.Build();
app.MapBuffalyAgentKit("/agentkit");
app.Run();
```

`MapBuffalyAgentKit("/agentkit")` maps all Agent Kit endpoints under the `/agentkit` prefix. This includes:
- `GET /agentkit/health` — health check
- `GET /agentkit/` — inspector page
- `POST /agentkit/api/conversations` — create a conversation
- `GET /agentkit/api/conversations` — list conversations
- `GET /agentkit/api/conversations/{id}` — get a conversation
- `POST /agentkit/api/conversations/{id}/turns` — run a turn
- `GET /agentkit/api/conversations/{id}/events` — get events

## The scripted chat client

The `ScriptedChatClient` is defined inline in `Program.cs`:

```csharp
public sealed class ScriptedChatClient(params ChatResponse[] responses) : IChatClient
{
    private readonly Queue<ChatResponse> _responses = new(responses);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_responses.Count > 0
            ? _responses.Dequeue()
            : new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

    // ... other members
}
```

It takes a list of predetermined responses and returns them one at a time, in order. When it runs out of responses, it returns "ok." This lets you test the full Agent Kit pipeline without an API key.

## Try this: Add a C# tool

The current sample doesn't expose any tools (the tools list may be empty). Let's add one. Add this before `var app = builder.Build();`:

```csharp
using Microsoft.Extensions.AI;

builder.Services.AddSingleton<IReadOnlyList<AIFunction>>(new[]
{
    AIFunctionFactory.Create(
        (string name) => $"Hello, {name}!",
        "greet_user",
        "Greet a user by name.")
});
```

Now the agent has a `greet_user` tool it can call. When you ask it to greet someone, it will call this function and return the result.

## Connecting a real AI provider

Replace the scripted client with a real one. Install the OpenAI package (`dotnet add package OpenAI`), then change the registration:

```csharp
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var client = new OpenAIClient(
        builder.Configuration["OpenAI:ApiKey"]!);
    return client.AsChatClient("gpt-4.1-mini");
});
```

Add your API key to `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here"
  }
}
```

Everything else — the tools, the endpoints, the conversation storage — stays exactly the same. The only thing that changed is the `IChatClient` registration.

## Troubleshooting

### The tools endpoint returns an empty list

`GET /agentkit/api/tools` returns `[]`. This means no tools are loaded. Check that:
1. The `agentkit.json` manifest path is correct
2. The manifest file exists and has valid JSON
3. The `.pts` file referenced in the manifest exists

### The server won't start

Check that:
1. Port 5128 is not already in use (try a different port with `--urls http://127.0.0.1:PORT`)
2. You're running from the repository root
3. `dotnet build` succeeds first

### Conversations are not persisting

Check that the data directory exists and is writable. The `UseJsonlStore` path must be a valid directory.

## File structure

```text
samples/AgentKit.AspNetCore/
  Program.cs              # The entire program (~30 lines)
  AgentKit.AspNetCore.csproj
```

## Key concepts

| Concept | What it means |
| --- | --- |
| **ASP.NET Core** | A web framework for .NET. Agent Kit can be hosted inside it. |
| **Dependency injection (DI)** | A pattern where services are registered once and shared across the app. |
| **IChatClient** | An interface representing "something you can chat with." |
| **MapBuffalyAgentKit** | The method that maps all Agent Kit HTTP endpoints under a prefix. |
| **JSONL store** | A simple file-based conversation store. Each conversation is saved as a JSONL file. |
| **Inspector page** | A built-in web UI for interacting with the agent from a browser. |

## Where to go next

- **[AgentKit.Console sample](../AgentKit.Console/README.md)** — See the simplest console sample (start here if you haven't)
- **[DevOps Incident Investigation](../DevOps.IncidentInvestigation/README.md)** — A real-world console sample with multiple tools
- **[Medical Referral Readiness](../Medical.ReferralReadiness/README.md)** — An ASP.NET sample with safety boundaries
- **[Commerce Return Resolution](../Commerce.ReturnResolution/README.md)** — An ASP.NET sample with human approval boundaries
- **[Getting started guide](../../docs/getting-started.md)** — Build your own Agent Kit app from scratch
