# DevOps Incident Investigation sample

## What this sample does

Imagine you have a monitoring system that detects when a web service gets slow. This sample shows how an AI agent can investigate what went wrong by looking at metrics, logs, and deployment history — just like a human DevOps engineer would.

The agent investigates a synthetic incident: the "checkout-api" service started getting slow at 14:05. The agent doesn't just guess — it gathers evidence step by step, classifies what it found, looks up relevant runbooks, and writes a report. Importantly, the agent uses "uncertainty language" — it says what the evidence suggests, not what definitely caused the problem. It also does NOT try to fix anything (no remediation). It just investigates and reports.

This is a **console/headless sample** — it runs in your terminal, not in a web server. No ASP.NET, no browser, no web endpoints. Just a program that runs, does its work, and writes output files.

## Prerequisites

- .NET SDK 9.0.300 (run `dotnet --version` to check)
- A terminal (PowerShell, cmd, or bash)
- No API key, no internet, no database — everything runs locally with synthetic data

## How to run it

From the repository root:

```powershell
.\samples\run-sample.ps1 incident
```

Or directly:

```bash
dotnet run --project samples/DevOps.IncidentInvestigation
```

You should see output like this:

```text
[turn] started
[tool] get_service_snapshot
[tool] get_metric_window
[tool] get_recent_deployments
[tool] read_log_excerpt
[tool] classify_incident_evidence
[tool] search_runbooks
[turn] completed

Wrote:
.../output/incident-report.md
.../output/events.jsonl
.../output/conversation.json
```

## What just happened? — The 7-step investigation explained

The agent followed a 7-step investigation. Let's walk through each step and explain why the agent does it:

### Step 1: Get a service snapshot

The agent calls `get_service_snapshot("checkout-api")`. This returns basic information about the service: what it is, where it runs, its current status.

**Why start here?** The agent needs to know what service it's investigating. It's like a doctor first reading the patient's chart before examining them. The agent confirms: yes, checkout-api exists, here's what it does.

### Step 2: Get metrics

The agent calls `get_metric_window("checkout-api", "latency_and_pool", ...)` with a time range around the incident. This returns metrics showing p95 latency (how slow the slowest 5% of requests are) and database connection pool utilization.

**Why next?** The agent needs to see if the latency actually increased and by how much. The metrics show p95 latency jumping to 1850ms and database pool utilization at 94%. That's a strong signal.

### Step 3: Check recent deployments

The agent calls `get_recent_deployments("checkout-api")`. This returns a list of recent code deployments to the service.

**Why check deployments?** A common cause of incidents is a recent code change. The agent finds that checkout-api was deployed at 14:00 — just 5 minutes before the latency spike at 14:05. That's suspicious.

### Step 4: Read log excerpts

The agent calls `read_log_excerpt("checkout-api", ...)` with a time range starting at 14:05. This returns the most recent log lines from the service.

**Why read logs?** Logs show what errors actually occurred. The agent finds timeout messages in the checkout-api logs after 14:05. This confirms something went wrong at that time.

### Step 5: Classify the evidence

The agent calls `classify_incident_evidence(...)` — this is a **ProtoScript tool**, not a C# tool. It takes the key metrics (error rate, p95 latency, pool utilization, minutes since deployment) and classifies the incident.

**Why use a ProtoScript tool for this?** The classification logic is a business rule: "if pool utilization is above 90% and there was a recent deployment, classify as resource_saturation_with_recent_change." Writing this in ProtoScript makes it explicit, inspectable, and changeable without recompiling C# code. The classifier returns a JSON classification with severity "high" and signals including "database_pool_saturation" and "recent_deployment_correlation."

**Important:** The classifier returns a *classification*, not a *root cause*. It says "the evidence is consistent with database pool saturation correlated with a recent deployment" — not "the deployment caused the pool saturation." This distinction matters.

### Step 6: Search runbooks

The agent calls `search_runbooks("database pool saturation")`. Runbooks are documents that contain instructions for handling known problems. The agent searches for runbooks related to what it found.

**Why search runbooks?** The agent has identified a likely problem. Now it looks up the relevant runbook so a human operator can follow the steps to resolve it. It finds `database-pool-saturation.md`.

### Step 7: Write the incident report

The agent produces its final answer: an incident report in Markdown. The report includes:
- What the most plausible explanation is (database connection-pool saturation correlated with a recent deployment)
- The evidence that supports this (metrics, logs, deployment history)
- A control comparison (payment-api had no degradation, so it's not a global issue)
- The recommended runbook
- An explicit statement: "Do not perform remediation from this sample; have an operator review the evidence and choose any action."

**Why uncertainty language?** The agent says "the most plausible explanation supported by the synthetic fixtures is..." — not "the root cause is...". This is deliberate. The agent is being honest about what it knows vs. what it's guessing. In real incident investigation, jumping to conclusions can make things worse.

The report is written to `output/incident-report.md`. The agent also writes:
- `output/events.jsonl` — a machine-readable event trace
- `output/conversation.json` — the full conversation history
## Understanding the code — File by file

### `Program.cs` — The entry point

This is the main program. It does five things:

1. **Sets up paths** — finds the Data/ directory (with synthetic data) and creates an output/ directory
2. **Loads ProtoScript tools** — loads `AgentTools/agentkit.json` which exports the `classify_incident_evidence` tool
3. **Creates C# tools** — calls `IncidentFunctions.Create(dataRoot)` which creates 5 C# tools for reading data
4. **Combines all tools** — merges C# tools and ProtoScript tools into one list
5. **Creates the runtime and runs a turn** — creates an `AgentKitRuntime` with the scripted client and all tools, then runs one turn

Key code:

```csharp
// Load ProtoScript tools (the classifier)
await using ProtoScriptToolSet protoScriptTools =
    await ProtoScriptToolSet.LoadAsync(Path.Combine(sampleRoot, "AgentTools", "agentkit.json"));

// Create C# tools (data readers)
IReadOnlyList<AIFunction> tools =
    IncidentFunctions.Create(dataRoot).Concat(protoScriptTools.Tools).ToArray();

// Set up event sinks (console trace + JSONL file)
var events = new CompositeAgentEventSink(new IAgentEventSink[] {
    new ConsoleAgentEventSink(),
    new JsonlAgentEventSink(Path.Combine(outputRoot, "events.jsonl"))
});

// Create the runtime with the scripted client and all tools
var runtime = new AgentKitRuntime(
    SampleChatClientFactory.Create(IncidentScenarioFactory.Create()),
    tools,
    eventSink: events);

// Create a conversation with a system message
AgentConversation conversation = AgentConversation.Create();
conversation.AddSystemMessage("You are an incident investigation assistant. Use evidence, do not perform remediation, and describe uncertainty clearly.");

// Run the turn
AgentTurnResult result = await runtime.RunTurnAsync(conversation,
    "Investigate the checkout-api latency increase beginning at 14:05. ...");
```

**What is a system message?** A system message is an instruction that tells the agent how to behave. Here it says: use evidence, don't fix things, and be honest about uncertainty. This shapes the agent's behavior throughout the conversation.

**What is a CompositeAgentEventSink?** It's an event sink that forwards events to multiple other sinks. Here it sends events to both the console (so you see them in real time) and a JSONL file (so they're saved for later analysis).

### `Tools/IncidentFunctions.cs` — C# data tools

This file creates 5 C# tools using `AIFunctionFactory.Create`:

| Tool name | What it does | Parameters |
| --- | --- | --- |
| `get_service_snapshot` | Reads service info from `Data/services.json` | `service_name` |
| `get_metric_window` | Reads metrics from `Data/metrics/{service}.json` | `service_name`, `metric_name`, `from_utc`, `to_utc` |
| `read_log_excerpt` | Reads log lines from `Data/logs/{service}.log` | `service_name`, `from_utc`, `to_utc`, `maximum_lines` |
| `get_recent_deployments` | Reads deployment history from `Data/deployments.json` | `service_name` |
| `search_runbooks` | Searches `Data/runbooks/*.md` for matching runbooks | `query` |

Each tool reads from a local JSON or log file. The `BoundedPath` method ensures tools can't read files outside the Data/ directory — a security measure.

**What is AIFunctionFactory.Create?** It's a helper from `Microsoft.Extensions.AI` that wraps a C# lambda into an `AIFunction` — a tool the AI agent can call. You provide the function, a name, and a description. Agent Kit handles the rest: converting arguments, calling the function, and returning the result to the model.

### `AgentTools/IncidentRules.pts` — ProtoScript classifier

```protoscript
prototype ClassifyIncidentEvidence
{
    function Execute(int errorRateTenthsPercent, int p95LatencyMs,
        int databasePoolUtilizationPercent, int minutesSinceDeployment): string
    {
        return "{\"severity\":\"high\",\"signals\":[\"latency_threshold_exceeded\",
                \"database_pool_saturation\",\"recent_deployment_correlation\"],
                \"classification\":\"resource_saturation_with_recent_change\"}";
    }
}
```

This is the ProtoScript tool that classifies the incident. It takes four metrics and returns a JSON string with a severity level, signal flags, and a classification. In a real application, this logic could be much more complex — checking multiple thresholds, combining signals, and producing detailed classifications.

### `AgentTools/agentkit.json` — Tool manifest

This file tells Agent Kit which ProtoScript functions to expose as tools. Only functions listed here become visible to the AI model. This is your security boundary — you control exactly what the agent can do.

### `Scenarios/IncidentScenarioFactory.cs` — The script

This file defines what the scripted "AI" does. It creates a `ScenarioDefinition` with 7 responses — one for each step of the investigation. Each response is either a tool call (`ScriptedChatResponse.Call(...)`) or a final answer (`ScriptedChatResponse.Final(...)`).

The scripted client also validates that the previous tool call completed successfully before issuing the next one. This ensures the investigation follows the expected order.

### `Data/` — Synthetic data files

All data is synthetic (fake). The files include:
- `services.json` — service definitions
- `deployments.json` — deployment history
- `metrics/checkout-api.json` — metrics showing latency spike and pool saturation
- `metrics/payment-api.json` — metrics for a control service (no problems)
- `logs/checkout-api.log` — log file with timeout messages
- `logs/payment-api.log` — log file for the control service
- `runbooks/*.md` — runbook documents for known problems

The data is designed to tell a story: checkout-api had a deployment at 14:00, then latency spiked at 14:05 due to database pool saturation. payment-api is a control — it had no issues, so the problem is specific to checkout-api.

## How to adapt this sample

### Add a new C# tool

Want to add a tool that reads alert history? Add this to `IncidentFunctions.cs`:

```csharp
yield return AIFunctionFactory.Create(
    (string service_name) => ReadJson(root, "alerts.json", service_name),
    "get_recent_alerts",
    "Get recent synthetic alerts for a service.");
```

Then add a corresponding `Data/alerts.json` file and update the scenario factory to call the new tool at the appropriate point in the investigation.

### Change the scenario data

Edit the files in `Data/`. For example, to simulate a different incident:
1. Change `Data/metrics/checkout-api.json` to show different metrics
2. Change `Data/logs/checkout-api.log` to show different errors
3. Update `Scenarios/IncidentScenarioFactory.cs` to match the new investigation flow

### Change the classification logic

Edit `AgentTools/IncidentRules.pts` to change how evidence is classified. For example, you could add new signals or change the severity thresholds. No C# recompilation needed — ProtoScript is interpreted at runtime.

## Connecting a real AI provider

Replace the scripted client with a real one. In `Program.cs`:

```csharp
// Before (scripted):
var runtime = new AgentKitRuntime(
    SampleChatClientFactory.Create(IncidentScenarioFactory.Create()),
    tools, eventSink: events);

// After (real provider):
using OpenAI;
IChatClient chat = new OpenAIClient(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
    .AsChatClient("gpt-4.1-mini");
var runtime = new AgentKitRuntime(chat, tools, eventSink: events);
```

Install the OpenAI package (`dotnet add package OpenAI`) and set `OPENAI_API_KEY`. With a real model, the agent will decide for itself which tools to call and in what order, based on the system message and user prompt. The tools, data, and event sinks all stay the same.

## Output files

After a successful run, the `output/` directory contains:

| File | What it is |
| --- | --- |
| `incident-report.md` | The agent's final report (Markdown) |
| `events.jsonl` | Machine-readable event trace (one JSON object per line) |
| `conversation.json` | Full conversation history (all messages, tool calls, and results) |

The `output/` directory is ignored by git and regenerated on each run.

## Troubleshooting

### "No scripted response remains"
The scripted client ran out of responses. This means the agent made more tool calls than expected. Check that the scenario factory matches the actual data.

### "File not found" errors
The Data/ directory path may be wrong. The sample uses `AppContext.BaseDirectory` to find it. If you're running from a different directory, the path may not resolve correctly. Try running from the repository root.

### The ProtoScript tool doesn't load
Check that `AgentTools/agentkit.json` exists and references the correct `.pts` file. The `projectFile` field must match the actual filename.

## File structure

```text
samples/DevOps.IncidentInvestigation/
  AgentTools/
    IncidentRules.pts       # ProtoScript classifier tool
    agentkit.json           # Tool manifest (which tools are visible to the AI)
  Data/
    services.json           # Service definitions
    deployments.json        # Deployment history
    metrics/                # Metric files per service
    logs/                   # Log files per service
    runbooks/               # Runbook documents
  Scenarios/
    IncidentScenarioFactory.cs  # Scripted investigation flow
  Tools/
    IncidentFunctions.cs    # C# data-reading tools
  Program.cs               # Entry point
  README.md
```

## Key Agent Kit concepts demonstrated

| Concept | How this sample uses it |
| --- | --- |
| **AgentKitRuntime** | Created directly in a console app (no ASP.NET needed) |
| **Mixed tool sources** | 5 C# tools + 1 ProtoScript tool, combined into one list |
| **ProtoScript tool** | The evidence classifier is written in ProtoScript |
| **Event sinks** | Composite sink sends events to both console and JSONL file |
| **Conversation export** | The full conversation is exported to JSON |
| **System message** | Tells the agent to use evidence, not remediate, and express uncertainty |
| **Headless embedding** | No web server, no browser — just a console program |
| **Bounded file access** | Tools can only read files inside Data/ (security measure) |

## Test

`tests/Buffaly.AgentKit.SampleTests/IncidentInvestigationScenarioTests.cs` verifies:
- No ASP.NET package reference (it's truly headless)
- The ProtoScript classifier is invoked
- Evidence references appear in the report
- Uncertainty language is used
- No remediation tool exists in the tool set
- Events complete properly

## Where to go next

- **[Samples overview](../README.md)** — See all available samples
- **[Medical Referral Readiness](../Medical.ReferralReadiness/README.md)** — See an ASP.NET sample with safety boundaries
- **[Commerce Return Resolution](../Commerce.ReturnResolution/README.md)** — See an ASP.NET sample with human approval
- **[Architecture guide](../../docs/architecture.md)** — Learn how Agent Kit works internally
- **[Getting started guide](../../docs/getting-started.md)** — Build your own Agent Kit app from scratch
