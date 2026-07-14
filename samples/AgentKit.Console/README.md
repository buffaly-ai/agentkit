# AgentKit.Console — Simplest Agent Kit Sample

## What this sample does

This is the simplest possible Agent Kit program. It creates an AI agent that can add two numbers using a tool. The "AI" is scripted — it follows a predetermined script instead of calling a real language model — so you don't need an API key or internet access to run it. But the tool calling is real: Agent Kit receives the function call, executes the tool, feeds the result back, and produces a final answer.

Think of it as a "hello world" for AI agents. If you understand this sample, you understand the core of how Agent Kit works.

## Prerequisites

- .NET SDK 9.0.300 (run `dotnet --version` to check)
- A terminal (PowerShell, cmd, or bash)
- No API key, no internet, no database — everything runs locally

## How to run it

From the repository root:

```bash
dotnet run --project samples/AgentKit.Console
```

You should see output like this:

```text
The answer is 5.
1: TurnStarted  Turn started
2: RoundStarted  Round 1 started
3: ModelResponseReceived  Model response received
4: ToolCallStarted add_numbers Tool call started
5: ToolCallCompleted add_numbers Tool call completed
6: RoundStarted  Round 2 started
7: ModelResponseReceived  Model response received
8: TurnCompleted  Turn completed
```

## What just happened?

Let's walk through that output line by line:

1. **`The answer is 5.`** — This is the agent's final answer. The agent was asked "Add 2 and 3" and it answered "The answer is 5."
2. **`TurnStarted`** — A "turn" is one complete interaction: you ask the agent something, it may call tools, and it gives you an answer. This event marks the beginning of that process.
3. **`RoundStarted  Round 1`** — A turn is made up of one or more "rounds." In each round, the agent talks to the AI model once. In round 1, the model decides it needs to call a tool.
4. **`ModelResponseReceived`** — The model responded. In this case, the response was a function call (not text), asking to call `add_numbers` with arguments `a=2` and `b=3`.
5. **`ToolCallStarted add_numbers`** — Agent Kit is now calling the `add_numbers` tool. This is a real function call — the ProtoScript code runs and returns the result.
6. **`ToolCallCompleted add_numbers`** — The tool finished. It returned `5` (because 2 + 3 = 5). Agent Kit feeds this result back to the model.
7. **`RoundStarted  Round 2`** — Now we're in round 2. Agent Kit sends the tool result back to the model, and the model responds again.
8. **`ModelResponseReceived`** — The model responded with text this time: "The answer is 5."
9. **`TurnCompleted`** — The turn is done. The agent has its final answer.

So the flow is: **ask → model says "call add_numbers" → tool runs → result goes back to model → model says "The answer is 5."**

## Understanding the code

Let's walk through `Program.cs` line by line. The full file is about 30 lines — that's all you need for a working AI agent.

### Step 1: Load ProtoScript tools

```csharp
string manifest = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "Tools", "agentkit.json"));
await using ProtoScriptToolSet tools =
    await ProtoScriptToolSet.LoadAsync(manifest);
```

This loads a ProtoScript tool set from a manifest file called `agentkit.json`. The manifest tells Agent Kit which tools are available. In this sample, there's one tool: `add_numbers`.

**What is a ProtoScript tool?** ProtoScript is a simple programming language that runs inside Agent Kit. You write functions in `.pts` files, and they become tools the AI agent can call. The alternative is writing tools in C# (which the other samples show). ProtoScript tools are useful for business rules, calculations, and logic that you want to be explicit and inspectable.

The `agentkit.json` manifest is an allowlist — only the tools listed in it become visible to the AI model. This is a security feature: you control exactly what the agent can do.

### Step 2: Create a scripted chat client

```csharp
var chat = new ScriptedChatClient(
    new ChatResponse(new ChatMessage(ChatRole.Assistant,
    [
        new FunctionCallContent("call-1", "add_numbers",
            new Dictionary<string, object?> { ["a"] = 2, ["b"] = 3 })
    ])),
    new ChatResponse(new ChatMessage(ChatRole.Assistant, "The answer is 5.")));
```

This creates a fake AI model. Instead of calling OpenAI or Anthropic, it returns predetermined responses. The first response is a function call (asking to add 2 and 3), and the second response is the final text answer.

**What is IChatClient?** `IChatClient` is an interface from `Microsoft.Extensions.AI` that represents "something you can chat with." It could be OpenAI, Anthropic, a local model, or — as in this case — a scripted test double. Agent Kit doesn't care which one you use. It just talks to whatever `IChatClient` you give it.

**Why use a scripted client?** So you can run and test the sample without an API key, without internet, and without spending money. The tool calling is still real — Agent Kit really does call the tool and feed the result back. Only the "AI" part is fake.

### Step 3: Create an event sink

```csharp
var events = new InMemoryAgentEventSink();
```

An event sink is where Agent Kit sends events as they happen (turn started, tool called, etc.). `InMemoryAgentEventSink` just stores them in memory. After the turn, we can read them back and print them. Other event sinks can write to JSONL files, the console, or anywhere else.

### Step 4: Create the runtime

```csharp
var runtime = new AgentKitRuntime(chat, tools.Tools, eventSink: events);
```

The `AgentKitRuntime` is the heart of Agent Kit. It takes three things:
1. **A chat client** — the "AI" (real or scripted)
2. **A list of tools** — functions the agent can call
3. **An event sink** — where to send events (optional)

The runtime manages the entire conversation loop: send messages to the model, execute tool calls, feed results back, and repeat until the model gives a final answer.

### Step 5: Create a conversation and run a turn

```csharp
AgentConversation conversation = AgentConversation.Create();
conversation.AddSystemMessage("Use tools when needed.");
AgentTurnResult result = await runtime.RunTurnAsync(conversation, "Add 2 and 3.");
```

An `AgentConversation` holds the message history. You can add a system message (instructions for the agent) and then run a turn with a user message.

`RunTurnAsync` does everything: sends the conversation to the model, handles tool calls, and returns when the model gives a final answer. The `AgentTurnResult` contains the final answer and other metadata.

### Step 6: Print the results

```csharp
Console.WriteLine(result.FinalAnswer);
foreach (AgentEvent e in events.Events)
    Console.WriteLine($"{e.Sequence}: {e.Kind} {e.ToolName} {e.Message}".Trim());
```

First we print the agent's answer, then we print all the events that were recorded during the turn. This is the output you saw above.

## The ProtoScript tool

The `add_numbers` tool is defined in `Tools/Project.pts`:

```protoscript
prototype AddNumbers
{
    function Execute(int a, int b): int
    {
        return a + b;
    }
}
```

And exported in `Tools/agentkit.json`:

```json
{
  "schemaVersion": 1,
  "projectFile": "Project.pts",
  "exports": [{
    "name": "add_numbers",
    "prototype": "AddNumbers",
    "method": "Execute",
    "description": "Add two integers.",
    "parameters": [
      { "name": "a", "type": "int", "required": true },
      { "name": "b", "type": "int", "required": true }
    ],
    "returnType": "int"
  }]
}
```

The manifest maps the ProtoScript function to a tool name the AI model can call. The `name` field is what the model sees. The `parameters` list tells Agent Kit how to validate and convert the model's arguments.

## Try this: Change the tool

Want to try multiplication instead? Edit `Tools/Project.pts`:

```protoscript
prototype MultiplyNumbers
{
    function Execute(int a, int b): int
    {
        return a * b;
    }
}
```

Update `Tools/agentkit.json` to match (change `name` to `multiply_numbers`, `prototype` to `MultiplyNumbers`, `description` to `Multiply two integers`).

Then update the scripted client in `Program.cs` to call `multiply_numbers` with the arguments you want, and update the final answer text.

Run it again and see the difference.

## Connecting a real AI provider

When you're ready to use a real AI model instead of the scripted client, you only change one thing — the `IChatClient`. Everything else (tools, events, conversation) stays the same.

Here's a complete example using OpenAI:

```csharp
using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;
using OpenAI;

// 1. Load tools (same as before)
await using ProtoScriptToolSet tools =
    await ProtoScriptToolSet.LoadAsync("Tools/agentkit.json");

// 2. Create a REAL chat client instead of the scripted one
IChatClient chat = new OpenAIClient(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
    .AsChatClient("gpt-4.1-mini");

// 3. Create runtime (same as before)
var events = new InMemoryAgentEventSink();
var runtime = new AgentKitRuntime(chat, tools.Tools, eventSink: events);

// 4. Run a turn (same as before)
AgentConversation conversation = AgentConversation.Create();
conversation.AddSystemMessage("Use tools when needed.");
AgentTurnResult result = await runtime.RunTurnAsync(conversation, "Add 2 and 3.");

Console.WriteLine(result.FinalAnswer);
```

You need to install the OpenAI NuGet package (`dotnet add package OpenAI`) and set the `OPENAI_API_KEY` environment variable. But notice: the only line that changed is step 2. The tools, the runtime, the conversation — all unchanged.

## File structure

```text
samples/AgentKit.Console/
  Tools/
    Project.pts          # ProtoScript tool definition (AddNumbers)
    agentkit.json        # Tool manifest (which tools are visible to the AI)
  Program.cs             # The entire program (~30 lines)
  AgentKit.Console.csproj
```

## Key concepts

| Concept | What it means |
| --- | --- |
| **IChatClient** | An interface representing "something you can chat with." Could be OpenAI, a local model, or a scripted test double. |
| **AIFunction** | A function the AI agent can call. Defined in C# or ProtoScript. |
| **ProtoScript tool** | A tool written in ProtoScript (a simple language that runs inside Agent Kit). Defined in `.pts` files and exported via `agentkit.json`. |
| **AgentKitRuntime** | The engine that runs the conversation loop: model → tool → model → answer. |
| **AgentConversation** | Holds the message history for one conversation. |
| **Turn** | One complete interaction: user asks, agent may call tools, agent answers. |
| **Round** | One model call within a turn. A turn may have multiple rounds if the model calls tools. |
| **Event sink** | Receives events as they happen (turn started, tool called, etc.). Useful for logging and debugging. |
| **ScriptedChatClient** | A fake `IChatClient` that returns predetermined responses. Used for testing without an API key. |

## Where to go next

- **[AgentKit.AspNetCore sample](../AgentKit.AspNetCore/README.md)** — See how to host Agent Kit in a web app
- **[DevOps Incident Investigation](../DevOps.IncidentInvestigation/README.md)** — See a real-world console sample with multiple tools
- **[Medical Referral Readiness](../Medical.ReferralReadiness/README.md)** — See an ASP.NET sample with safety boundaries
- **[Commerce Return Resolution](../Commerce.ReturnResolution/README.md)** — See an ASP.NET sample with human approval boundaries
- **[Getting started guide](../../docs/getting-started.md)** — Build your own Agent Kit app from scratch
