# Architecture

Buffaly Agent Kit is organized as a small set of embedding packages plus frozen runtime packages. The design goal is to expose a clean, provider-neutral agent/tool loop without exporting Buffaly's production host, session services, SQL persistence, workers, or private operational infrastructure.

## Three-package model

| Package | Responsibility |
| --- | --- |
| `Buffaly.AgentKit` | Headless runtime, conversation state, messages, events, tool policy, options. |
| `Buffaly.AgentKit.ProtoScript` | Converts manifest-allowlisted ProtoScript methods into `AIFunction` tools. |
| `Buffaly.AgentKit.AspNetCore` | DI registration, JSONL/in-memory stores, API routes, JSONL event sink, static inspector. |

## Dependency graph

```text
Consumer application
  ├─ IChatClient provider implementation
  ├─ Buffaly.AgentKit
  │    └─ Microsoft.Extensions.AI.Abstractions
  ├─ Buffaly.AgentKit.ProtoScript
  │    ├─ Buffaly.AgentKit
  │    ├─ ProtoScript.Runtime / Parsers / Interpretter
  │    └─ Ontology.Runtime / Parsers / Simulation
  └─ Buffaly.AgentKit.AspNetCore
       ├─ Buffaly.AgentKit
       ├─ Buffaly.AgentKit.ProtoScript
       └─ ASP.NET Core
```

## Why `IChatClient`

Provider integrations change quickly. Agent Kit uses `Microsoft.Extensions.AI.IChatClient` so applications can choose OpenAI, Azure OpenAI, Ollama, a relay service, or a deterministic test client without changing the runtime. The runtime only needs provider-facing messages, tools in `ChatOptions.Tools`, and `FunctionCallContent`/`FunctionResultContent` round trips.

## Why explicit manifests

ProtoScript projects can contain many prototypes and helper methods. Agent Kit requires an explicit `agentkit.json` export manifest so model-visible tools are intentional. This avoids accidental exposure of internal functions and makes review/audit straightforward.

## Why the production hosted agent stack was not exported

Buffaly's production agent host includes session routing, service calls, production persistence, worker orchestration, and operational assumptions that do not belong in a portable SDK. Agent Kit is an embedding library. It intentionally excludes production hosted-agent classes, production service APIs, production session objects, SQL, workers, extension loaders, and production session flows.

## Frozen runtime strategy

The ProtoScript/Ontology runtime was copied into `src/runtime` and adjusted for package use. Direct dependencies on the legacy private common/cache/web utility packages were removed where practical. `BasicUtilities.dll` remains redistributed through `Buffaly.Foundation.Runtime` because removing it is larger than the 1.0 freeze scope.

## Event system design

Events are monotonic, lightweight records emitted by the runtime. They are designed for logs, inspectors, tests, and audit trails rather than for provider-specific telemetry. The core sinks are in-memory/null/composite; ASP.NET adds a file JSONL sink.

## Exclusions

Agent Kit does not include:

- provider-specific model clients,
- production Buffaly session services,
- SQL persistence,
- remote extension loading,
- automatic ProtoScript prototype discovery,
- payment/EHR/telemetry integrations in samples.

These boundaries keep the SDK reviewable and host-application-owned.


