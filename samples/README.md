# Buffaly Agent Kit samples

| Sample | Run command | What it proves |
| --- | --- | --- |
| Medical referral readiness | `run-sample medical` | Domain-specific administrative rules and auditable read-only use |
| Return resolution | `run-sample returns` | Controlled proposal creation with human approval outside the agent |
| Incident investigation | `run-sample incident` | Headless embedding and evidence-oriented tool orchestration |

All samples default to deterministic scripted mode. Scripted mode is demonstration infrastructure: it emits real provider-facing function calls through Agent Kit and validates observed tool results, but it is not a language model simulation. A real model is enabled by replacing the registered `IChatClient`; no Agent Kit runtime or tool code needs to change.

## Shared guarantees

- No network calls or API keys in default mode.
- All domain data is synthetic.
- Each sample mixes C# `AIFunction` tools with at least one ProtoScript tool.
- Tool calls are real Agent Kit model-to-tool-to-model rounds.
- Events are emitted during the run.
- Web samples persist under `.agentkit/`.
- Generated output directories are ignored by git.

## Shared infrastructure

`Buffaly.AgentKit.SampleSupport` contains:

- `ScriptedChatClient`: deterministic `IChatClient` that validates conversation state and emits the next scripted response.
- `ScriptedChatResponse`: function-call or final-text response.
- `ScenarioDefinition`: scenario ID and ordered responses.
- `JsonFixtureStore`: bounded JSON fixture access.
- `ConsoleAgentEventSink`: compact live trace.
- `JsonlAgentEventSink`: sample-local JSONL event writing.
- `SampleChatClientFactory`: creates scripted clients.
- `DeterministicClock`: fixed timestamp helper for deterministic behavior.

## One-command run experience

PowerShell:

```powershell
.\samples\run-sample.ps1 medical
.\samples\run-sample.ps1 returns
.\samples\run-sample.ps1 incident
```

Bash:

```bash
./samples/run-sample.sh medical
./samples/run-sample.sh returns
./samples/run-sample.sh incident
```

The scripts verify the pinned SDK, restore in locked mode, select scripted provider mode, set local sample storage, then start the app or run the console sample.

## Samples

### Medical referral readiness

Path: `samples/Medical.ReferralReadiness`

ASP.NET read-only administrative workflow for referral packet completeness. See its README and `SAFETY.md`.

### Commerce return resolution

Path: `samples/Commerce.ReturnResolution`

ASP.NET controlled side-effect workflow. The agent can create a pending proposal; approval is a normal app endpoint outside the model-visible tool set.

### DevOps incident investigation

Path: `samples/DevOps.IncidentInvestigation`

Console/headless sample that reads synthetic telemetry/log/runbook fixtures and writes an incident report, event trace, and conversation export.

## Replacing the scripted provider

Each sample registers `IChatClient` with `SampleChatClientFactory.Create(...)`. To use a real provider, replace only that registration:

```csharp
builder.Services.AddSingleton<IChatClient>(sp => CreateConfiguredChatClient(builder.Configuration));
```

Keep the tool registration, ProtoScript manifests, and repository seams unchanged unless you are changing domain behavior.
