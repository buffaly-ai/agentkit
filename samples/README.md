# Buffaly Agent Kit samples

## What is Agent Kit?

Agent Kit is a .NET SDK that lets you embed AI agent functionality in your own applications. An "AI agent" is a program that can talk to an AI model (like GPT-4), call tools (functions you define), and produce answers — all running inside your app, with your data, under your control.

Agent Kit handles the conversation loop: sending messages to the AI model, executing tool calls when the model asks for them, feeding results back, and repeating until the model gives a final answer. You provide the tools and the AI model; Agent Kit does the rest.

## What are these samples?

The samples show you how to use Agent Kit in real applications. They range from a minimal "hello world" to full domain-specific workflows with safety boundaries. All samples run **without an API key** — they use a "scripted" fake AI model that follows a predetermined script, so you can test everything locally.

## Which sample should I start with?

**Start with `AgentKit.Console`** — it's the simplest possible Agent Kit program (about 30 lines of code). It shows the core concepts: an AI agent that calls a tool to add two numbers. If you understand that sample, you understand the core of how Agent Kit works.

Then try the domain samples in this order:
1. **DevOps Incident Investigation** — a console sample with multiple tools (no web server)
2. **Medical Referral Readiness** — an ASP.NET sample with safety boundaries
3. **Commerce Return Resolution** — an ASP.NET sample with human approval boundaries

## All samples at a glance

| Sample | Type | What it shows | Run command |
| --- | --- | --- | --- |
| [AgentKit.Console](AgentKit.Console/README.md) | Console | Simplest possible agent: one tool, one turn | `dotnet run --project samples/AgentKit.Console` |
| [AgentKit.AspNetCore](AgentKit.AspNetCore/README.md) | ASP.NET | Simplest web agent: chat endpoint + inspector | `dotnet run --project samples/AgentKit.AspNetCore` |
| [DevOps Incident Investigation](DevOps.IncidentInvestigation/README.md) | Console | Multi-tool investigation with evidence classification | `.\samples\run-sample.ps1 incident` |
| [Medical Referral Readiness](Medical.ReferralReadiness/README.md) | ASP.NET | Read-only administrative workflow with safety boundary | `.\samples\run-sample.ps1 medical` |
| [Commerce Return Resolution](Commerce.ReturnResolution/README.md) | ASP.NET | Controlled side-effect with human approval boundary | `.\samples\run-sample.ps1 returns` |

## What is "scripted mode"?

All samples use a **ScriptedChatClient** — a fake AI model that follows a predetermined script instead of calling a real language model. Think of it like a test double in unit testing: it lets you run and test the full Agent Kit pipeline without an API key, without internet, and without spending money.

The tool calling is still real. When the scripted model says "call the add_numbers tool," Agent Kit really does call that tool, gets the real result, and feeds it back. Only the "AI" part is fake. The tools, the event system, the conversation management — all of that is the real Agent Kit runtime.

When you're ready to use a real AI model, you replace the scripted client with a real one (like OpenAI). That's a one-line change. Everything else stays the same.

## How to run any sample

### Prerequisites
- .NET SDK 9.0.300 (run `dotnet --version` to check)
- A terminal (PowerShell, cmd, or bash)
- No API key, no internet, no database

### Option 1: Use the run scripts (recommended for domain samples)

PowerShell:
```powershell
.\samples\run-sample.ps1 incident
.\samples\run-sample.ps1 medical
.\samples\run-sample.ps1 returns
```

Bash:
```bash
./samples/run-sample.sh incident
./samples/run-sample.sh medical
./samples/run-sample.sh returns
```

The run scripts verify the SDK, restore packages, set environment variables, and start the sample.

### Option 2: Run directly with dotnet

```bash
dotnet run --project samples/AgentKit.Console
dotnet run --project samples/AgentKit.AspNetCore
dotnet run --project samples/DevOps.IncidentInvestigation
dotnet run --project samples/Medical.ReferralReadiness
dotnet run --project samples/Commerce.ReturnResolution
```

## Key concepts glossary

If you're new to Agent Kit, here are the key concepts you'll see in the samples:

| Concept | What it means |
| --- | --- |
| **IChatClient** | An interface representing "something you can chat with." Could be OpenAI, a local model, or a scripted test double. Agent Kit talks to whatever IChatClient you give it. |
| **AIFunction** | A function the AI agent can call. You define tools in C# or ProtoScript. The AI model decides when to call them. |
| **ProtoScript tool** | A tool written in ProtoScript (a simple language that runs inside Agent Kit). Defined in `.pts` files and exported via `agentkit.json`. Useful for business rules and logic you want to be explicit and inspectable. |
| **AgentKitRuntime** | The engine that runs the conversation loop: model → tool → model → answer. You give it a chat client, a list of tools, and optional event sinks. |
| **AgentConversation** | Holds the message history for one conversation. You can add system messages (instructions) and user messages. |
| **Turn** | One complete interaction: user asks, agent may call tools, agent answers. |
| **Round** | One model call within a turn. A turn may have multiple rounds if the model calls tools (each tool call triggers a new round). |
| **Event sink** | Receives events as they happen (turn started, tool called, etc.). Useful for logging, debugging, and audit trails. |
| **ScriptedChatClient** | A fake IChatClient that returns predetermined responses. Used for testing without an API key. |
| **Tool manifest** | A JSON file (`agentkit.json`) that lists which ProtoScript tools are visible to the AI. This is your security boundary — only tools in the manifest can be called by the agent. |
| **Inspector page** | A built-in web UI for interacting with the agent from a browser. Available in ASP.NET samples. |

## Shared infrastructure

All domain samples use shared infrastructure from `Buffaly.AgentKit.SampleSupport`:

| Component | What it does |
| --- | --- |
| `ScriptedChatClient` | Deterministic IChatClient that validates conversation state and emits scripted responses |
| `ScriptedChatResponse` | One scripted response (either a tool call or final text) |
| `ScenarioDefinition` | A scenario ID and ordered list of scripted responses |
| `JsonFixtureStore` | Bounded JSON fixture access for sample data |
| `ConsoleAgentEventSink` | Compact console trace (prints events as they happen) |
| `JsonlAgentEventSink` | Writes events to a JSONL file |
| `SampleChatClientFactory` | Creates scripted clients from scenario definitions |
| `DeterministicClock` | Fixed timestamp helper for deterministic behavior |

## Shared guarantees

- No network calls or API keys in default mode
- All domain data is synthetic (fake)
- Each domain sample mixes C# AIFunction tools with at least one ProtoScript tool
- Tool calls are real Agent Kit model-to-tool-to-model rounds
- Events are emitted during every run
- Web samples persist conversations under `.agentkit/`
- Generated output directories are ignored by git

## Replacing the scripted provider with a real AI

Every sample registers `IChatClient` with `SampleChatClientFactory.Create(...)`. To use a real provider, replace only that registration. For example, with OpenAI:

```csharp
// Before (scripted — no API key needed):
builder.Services.AddSingleton<IChatClient>(
    SampleChatClientFactory.Create(MyScenarioFactory.Create()));

// After (real provider — needs API key):
using OpenAI;
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var client = new OpenAIClient(builder.Configuration["OpenAI:ApiKey"]!);
    return client.AsChatClient("gpt-4.1-mini");
});
```

Keep the tool registration, ProtoScript manifests, and repository seams unchanged. The only thing that changes is the IChatClient. With a real model, the agent decides for itself which tools to call and in what order — you don't need to script the flow.

## How the samples relate to each other

```
AgentKit.Console (simplest — one tool, one turn, console only)
       │
       ▼
AgentKit.AspNetCore (simplest web — chat endpoint, inspector page)
       │
       ▼
DevOps.IncidentInvestigation (real-world console — 6 tools, ProtoScript classifier, output files)
       │
       ▼
Medical.ReferralReadiness (real-world web — safety boundary, read-only, repository pattern)
       │
       ▼
Commerce.ReturnResolution (most complex — human approval boundary, controlled side effects)
```

Each sample builds on the concepts of the previous one. Start at the top and work your way down.
