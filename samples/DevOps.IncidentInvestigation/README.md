# DevOps Incident Investigation sample

This console sample demonstrates headless Agent Kit embedding for evidence-oriented incident investigation. It does not host ASP.NET Core, does not expose an inspector, and does not perform remediation.

The default scenario investigates a synthetic `checkout-api` latency increase beginning at 14:05.

## What it demonstrates

- Direct `AgentKitRuntime` usage from a console app.
- Mixed C# fixture tools and a ProtoScript classifier.
- Bounded file access under `Data/`.
- `CompositeAgentEventSink` with compact console trace plus JSONL trace.
- Conversation export to JSON.
- Uncertainty language in the final report.

## Prerequisites

- .NET SDK `9.0.300` for this repository build.
- No API keys, network access, or telemetry services are required.

## Run

```powershell
.\samples\run-sample.ps1 incident
```

or:

```bash
dotnet run --project samples/DevOps.IncidentInvestigation
```

## Console live trace

A successful run prints:

```text
[turn] started
[tool] get_service_snapshot
[tool] get_metric_window
[tool] get_recent_deployments
[tool] read_log_excerpt
[tool] classify_incident_evidence
[tool] search_runbooks
[turn] completed
```

## Output files

The sample writes:

- `output/incident-report.md`
- `output/events.jsonl`
- `output/conversation.json`

`output/` is ignored by git and regenerated on each run.

## Scripted flow

1. `get_service_snapshot("checkout-api")`
2. `get_metric_window("checkout-api", "latency_and_pool", ...)`
3. `get_recent_deployments("checkout-api")`
4. `read_log_excerpt("checkout-api", ...)`
5. `classify_incident_evidence(...)`
6. `search_runbooks("database pool saturation")`
7. final incident report

## Tools

C# tools from `Tools/IncidentFunctions.cs`:

- `get_service_snapshot(service_name)`
- `get_metric_window(service_name, metric_name, from_utc, to_utc)`
- `read_log_excerpt(service_name, from_utc, to_utc, maximum_lines)`
- `get_recent_deployments(service_name)`
- `search_runbooks(query)`

ProtoScript tool:

- `classify_incident_evidence(errorRateTenthsPercent, p95LatencyMs, databasePoolUtilizationPercent, minutesSinceDeployment)`

The classifier returns evidence classification, not a proven root cause.

## Synthetic data

- `Data/services.json`
- `Data/deployments.json`
- `Data/metrics/checkout-api.json`
- `Data/metrics/payment-api.json`
- `Data/logs/checkout-api.log`
- `Data/logs/payment-api.log`
- `Data/runbooks/*.md`

The fixtures intentionally show checkout degradation while payment remains a control.

## Replacing the scripted client

`Program.cs` creates the runtime with `SampleChatClientFactory.Create(IncidentScenarioFactory.Create())`. Replace that client with a provider-backed `IChatClient` and keep the tools/event sinks unchanged.

## File structure

```text
samples/DevOps.IncidentInvestigation/
  AgentTools/
  Data/
  Scenarios/
  Tools/
  Program.cs
  README.md
```

## Test

`tests/Buffaly.AgentKit.SampleTests/IncidentInvestigationScenarioTests.cs` verifies no ASP.NET package reference, classifier invocation, evidence references, uncertainty language, no remediation tool, and event completion.
