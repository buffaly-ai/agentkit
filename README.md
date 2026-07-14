# Buffaly Agent Kit

Buffaly Agent Kit is a provider-neutral .NET agent runtime for applications that need deterministic tool execution, conversation state, event tracing, and optional ProtoScript rule tools without adopting Buffaly's production agent host. It is intended for developers who want to embed an agent/tool loop inside their own console apps, services, ASP.NET Core applications, or product prototypes while keeping the language-model provider at a clean boundary.

The kit is deliberately small. The core package owns the turn loop, tool dispatch, event emission, conversation import/export, and tool policy decisions. Provider-specific chat clients stay outside the core and plug in through `Microsoft.Extensions.AI.IChatClient`. Tools are standard `AIFunction` instances, so C# functions and ProtoScript-exported rules can be mixed in the same run.

This repository is the Agent Kit 1.0 reference implementation. It includes the core packages, a frozen local ProtoScript/Ontology runtime, ASP.NET Core hosting helpers, deterministic scripted samples, and three domain-specific sample applications that demonstrate read-only administrative workflows, controlled side effects, and headless incident investigation.

> Target framework: Agent Kit 1.0 targets `net10.0` and is validated with the SDK pinned in [`global.json`](global.json). See [`FREEZE.md`](FREEZE.md) for the frozen release line.

## Quick start

### 1. Install packages

When published, a minimal console application that uses ProtoScript tools references:

```bash
dotnet add package Buffaly.AgentKit --version 1.0.0
dotnet add package Buffaly.AgentKit.ProtoScript --version 1.0.0
```

For ASP.NET Core hosting and the built-in inspector/API endpoints:

```bash
dotnet add package Buffaly.AgentKit.AspNetCore --version 1.0.0
```

Inside this repository and samples, projects use `ProjectReference` instead of NuGet packages because the packages are built locally and not published yet.

### 2. Create an `IChatClient`

Agent Kit does not depend on a specific model provider. It accepts any `Microsoft.Extensions.AI.IChatClient`.

A real OpenAI-style setup looks like this conceptually:

```csharp
using Microsoft.Extensions.AI;
using OpenAI;

IChatClient chatClient = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
    .AsChatClient("gpt-4.1-mini");
```

Provider adapter package names and extension methods may vary by provider/version. The important Agent Kit contract is `IChatClient`.

For no-network deterministic tests, use a scripted client like the one in [`samples/Buffaly.AgentKit.SampleSupport`](samples/Buffaly.AgentKit.SampleSupport).

### 3. Load ProtoScript tools from a manifest

```csharp
using Buffaly.AgentKit.ProtoScript;

await using ProtoScriptToolSet protoTools =
    await ProtoScriptToolSet.LoadAsync("AgentTools/agentkit.json");
```

A manifest explicitly allowlists the functions that become model-visible tools:

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

### 4. Create a runtime and run a turn

```csharp
using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;

IChatClient chatClient = CreateYourChatClient();

await using ProtoScriptToolSet protoTools =
    await ProtoScriptToolSet.LoadAsync("AgentTools/agentkit.json");

var events = new InMemoryAgentEventSink();
var runtime = new AgentKitRuntime(
    chatClient,
    protoTools.Tools,
    eventSink: events);

AgentConversation conversation = AgentConversation.Create();
conversation.AddSystemMessage("You are a concise assistant. Use tools when needed.");

AgentTurnResult result = await runtime.RunTurnAsync(
    conversation,
    "Add 2 and 3.");

Console.WriteLine(result.FinalAnswer);

foreach (AgentEvent item in events.Events)
    Console.WriteLine($"{item.Sequence}: {item.Kind} {item.ToolName}");
```

The runtime sends provider-facing tool definitions through `ChatOptions.Tools`, receives `FunctionCallContent`, invokes the matching `AIFunction`, adds `FunctionResultContent` back to the conversation, and continues until the model returns a final message or `MaxRounds` is reached.

## Package overview

| Package | Purpose | Important dependencies |
| --- | --- | --- |
| `Buffaly.AgentKit` | Headless runtime: turn loop, conversations, messages, events, tool policy, options. | `Microsoft.Extensions.AI.Abstractions` |
| `Buffaly.AgentKit.ProtoScript` | Loads a manifest-allowlisted ProtoScript project and exposes exported methods as `AIFunction` tools. | `Buffaly.AgentKit`, frozen `ProtoScript.*`, frozen `Ontology.*` runtime packages |
| `Buffaly.AgentKit.AspNetCore` | ASP.NET Core DI registration, in-memory/JSONL stores, API endpoints, static inspector. | `Buffaly.AgentKit`, `Buffaly.AgentKit.ProtoScript`, ASP.NET Core |
| `ProtoScript.Runtime`, `ProtoScript.Parsers`, `ProtoScript.Interpretter` | Frozen local ProtoScript runtime copied into this repository for packaging. | Frozen runtime support packages |
| `Ontology.Runtime`, `Ontology.Parsers`, `Ontology.Simulation` | Frozen local ontology runtime used by ProtoScript. | `Buffaly.Foundation.Runtime` |
| `Buffaly.Foundation.Runtime` | Redistribution package for the remaining closed foundation dependency retained by the frozen runtime. | `BasicUtilities.dll` |

## Architecture overview

The dependency graph is intentionally shallow:

```text
Consumer application
  ├─ Microsoft.Extensions.AI.IChatClient implementation
  ├─ Buffaly.AgentKit
  │    └─ Microsoft.Extensions.AI.Abstractions
  ├─ Buffaly.AgentKit.ProtoScript
  │    ├─ Buffaly.AgentKit
  │    └─ Frozen ProtoScript/Ontology runtime packages
  └─ Buffaly.AgentKit.AspNetCore (optional)
       ├─ Buffaly.AgentKit
       └─ ASP.NET Core
```

The production Buffaly hosted agent stack, session host, SQL persistence, worker infrastructure, and service-specific runtime are intentionally not exported. Agent Kit is a clean embedding library, not a copy of the production Buffaly host.

See [`docs/architecture.md`](docs/architecture.md) for design details.

## Core concepts

### `AgentKitRuntime`

`AgentKitRuntime` is the headless turn loop. It appends the user's message, emits events, calls the configured `IChatClient`, dispatches requested tools, appends tool results, and repeats until a final answer or `MaxRounds`.

```csharp
var runtime = new AgentKitRuntime(
    chatClient,
    tools,
    options: new AgentKitOptions { MaxRounds = 8 },
    eventSink: new InMemoryAgentEventSink(),
    toolPolicy: AllowAllAgentToolPolicy.Instance);
```

### `AgentConversation`

`AgentConversation` stores ordered `AgentMessage` values. It supports `Create`, `AddSystemMessage`, `ExportState`, and `ImportState`. Use export/import for application-controlled persistence or use the ASP.NET stores.

### `AIFunction` tools

Agent Kit uses `Microsoft.Extensions.AI.AIFunction` as the model-visible tool shape. You can mix C# tools created with `AIFunctionFactory.Create`, ProtoScript tools loaded by `ProtoScriptToolSet.LoadAsync`, and custom `AIFunction` implementations.

### `AgentEvent` and `IAgentEventSink`

Every runtime turn emits ordered events. The current `AgentEvent` contains `SchemaVersion`, `Sequence`, `Timestamp`, `Kind`, optional `Message`, optional `ToolName`, and optional `ToolCallId`.

Built-in core sinks are `NullAgentEventSink`, `InMemoryAgentEventSink`, and `CompositeAgentEventSink`. The ASP.NET package adds `FileAgentEventSink`, which writes JSONL.

### `IAgentToolPolicy`

`IAgentToolPolicy` evaluates each model-requested tool call before invocation. Use it to deny tools, restrict arguments, or enforce application-level safety rules.

### `AgentKitOptions`

| Option | Default |
| --- | --- |
| `MaxRounds` | `8` |
| `MaxToolCallsPerRound` | `8` |
| `ToolTimeout` | `2 minutes` |
| `MaxToolResultCharacters` | `100000` |

## Provider integration

The provider boundary is `IChatClient`. Agent Kit does not know whether the model is OpenAI, Azure OpenAI, Ollama, a local test double, or an application-specific relay. The runtime only requires that the client understands `ChatMessage`, `ChatOptions.Tools`, `FunctionCallContent`, and normal assistant messages.

See [`docs/providers.md`](docs/providers.md) for OpenAI, Azure OpenAI, Ollama, custom client, and scripted-client examples.

## ProtoScript tool integration

`Buffaly.AgentKit.ProtoScript` loads tools from an explicit JSON manifest. It does not auto-discover every prototype in a project. The manifest's `projectFile` is resolved relative to the manifest, absolute paths are rejected, and path escapes are rejected. Interpreter invocations are serialized with a `SemaphoreSlim`.

Supported manifest types are `string`, `int`, `long`, `decimal`, `double`, `float`, `bool`, `JsonObject`, and `JsonArray`. The frozen ProtoScript compiler itself may support a smaller set of script-language type names than the manifest converter; the included samples use compiler-compatible signatures.

See [`docs/protoscript-tools.md`](docs/protoscript-tools.md).

## ASP.NET Core integration

The ASP.NET package provides `services.AddBuffalyAgentKit(...)`, `builder.AddProtoScriptTools(...)`, `builder.UseJsonlStore(...)`, `app.MapBuffalyAgentKit("/_agentkit")`, stores, `FileAgentEventSink`, and a plain HTML inspector.

See [`docs/aspnet.md`](docs/aspnet.md) for endpoints and a full `Program.cs` example.

## Samples

| Sample | Path | What it demonstrates |
| --- | --- | --- |
| Medical referral readiness | [`samples/Medical.ReferralReadiness`](samples/Medical.ReferralReadiness) | ASP.NET, read-only administrative workflow, synthetic referral data, ProtoScript completeness rule |
| Commerce return resolution | [`samples/Commerce.ReturnResolution`](samples/Commerce.ReturnResolution) | ASP.NET, controlled proposal creation, human approval outside model-visible tools |
| DevOps incident investigation | [`samples/DevOps.IncidentInvestigation`](samples/DevOps.IncidentInvestigation) | Console/headless embedding, evidence gathering, JSONL event trace, no ASP.NET dependency |

Run with:

```powershell
.\samples\run-sample.ps1 medical
.\samples\run-sample.ps1 returns
.\samples\run-sample.ps1 incident
```

or:

```bash
./samples/run-sample.sh medical
./samples/run-sample.sh returns
./samples/run-sample.sh incident
```

See [`samples/README.md`](samples/README.md) and [`docs/samples.md`](docs/samples.md).

## Documentation index

- [`docs/getting-started.md`](docs/getting-started.md) — step-by-step console quick start.
- [`docs/architecture.md`](docs/architecture.md) — package architecture and design rationale.
- [`docs/providers.md`](docs/providers.md) — provider integration guide.
- [`docs/protoscript-tools.md`](docs/protoscript-tools.md) — ProtoScript tool manifests and loading.
- [`docs/aspnet.md`](docs/aspnet.md) — ASP.NET Core hosting, endpoints, and inspector.
- [`docs/events.md`](docs/events.md) — event sinks, ordering, and JSONL replay.
- [`docs/samples.md`](docs/samples.md) — detailed sample walkthroughs.

## Build and test this repository

```bash
dotnet restore Buffaly.AgentKit.sln --locked-mode
dotnet build Buffaly.AgentKit.sln -c Release --no-restore
dotnet test Buffaly.AgentKit.sln -c Release --no-build
```

Expected current test counts:

- core/package tests: 28/28
- sample scenario tests: 3/3

## License

Buffaly Agent Kit is licensed under GPL-3.0-only. See [`LICENSE`](LICENSE).

Commercial licensing is available from Buffaly for organizations that need non-GPL terms.

