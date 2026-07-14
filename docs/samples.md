# Samples walkthrough

This guide walks through every sample in detail. If you're new to Agent Kit, start with the [samples overview](../samples/README.md) first, then come back here for the deep dive.

## What you'll learn

By the end of this guide, you'll understand:
- How to run each sample and what to expect
- What happens inside each sample step by step (every tool call explained)
- What each file does and why it exists
- How to adapt each sample for your own use case
- How to connect a real AI provider (with complete code examples)
- How to troubleshoot common issues

## Quick reference

| Sample | Type | Complexity | Start here? |
| --- | --- | --- | --- |
| AgentKit.Console | Console | Minimal | Yes — simplest possible agent |
| AgentKit.AspNetCore | ASP.NET | Minimal | Yes — simplest web agent |
| DevOps.IncidentInvestigation | Console | Intermediate | After the basics |
| Medical.ReferralReadiness | ASP.NET | Intermediate | After the basics |
| Commerce.ReturnResolution | ASP.NET | Advanced | Last — most complex patterns |

---

## AgentKit.Console — The simplest agent

### What this sample does

This is the "hello world" of Agent Kit. It creates an AI agent that can add two numbers using a tool. The "AI" is scripted (no API key needed), but the tool calling is real: Agent Kit receives the function call, executes the tool, feeds the result back, and produces a final answer.

### How to run it

```bash
dotnet run --project samples/AgentKit.Console
```

Expected output:

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

### What happens when you run it

1. The program loads a ProtoScript tool called `add_numbers` from `Tools/agentkit.json`
2. It creates a scripted chat client with two predetermined responses: first a function call (add 2 and 3), then a final text answer ("The answer is 5.")
3. It creates an `AgentKitRuntime` with the chat client and the tool
4. It runs a turn: "Add 2 and 3."
5. The scripted model responds with a function call to `add_numbers(a=2, b=3)`
6. Agent Kit calls the tool — the ProtoScript function runs and returns 5
7. Agent Kit feeds the result (5) back to the model
8. The model responds with text: "The answer is 5."
9. The turn completes. The program prints the answer and all events.

### Understanding the code

The entire program is in `Program.cs` (~30 lines). See the [sample README](../samples/AgentKit.Console/README.md) for a line-by-line walkthrough.

### Connecting a real AI provider

```csharp
using OpenAI;
IChatClient chat = new OpenAIClient(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
    .AsChatClient("gpt-4.1-mini");
var runtime = new AgentKitRuntime(chat, tools.Tools, eventSink: events);
```

Only the `IChatClient` changes. Tools, events, and conversation stay the same.

---

## AgentKit.AspNetCore — The simplest web agent

### What this sample does

This is the simplest ASP.NET Core sample. It hosts Agent Kit in a web app with a chat endpoint and an inspector page. Send a message, get a response. The "AI" is scripted (no API key needed).

### How to run it

```bash
dotnet run --project samples/AgentKit.AspNetCore --urls http://127.0.0.1:5128
```

### Testing the endpoints

```bash
# Health check
curl http://127.0.0.1:5128/agentkit/health
# → {"status":"ok"}

# Create a conversation
curl -X POST http://127.0.0.1:5128/agentkit/api/conversations \
  -H "Content-Type: application/json" \
  -d '{"systemPrompt":"You are testing Agent Kit."}'
# → returns a conversation ID

# Run a turn
curl -X POST http://127.0.0.1:5128/agentkit/api/conversations/{id}/turns \
  -H "Content-Type: application/json" \
  -d '{"message":"Say hello."}'
# → {"stopReason":0,"finalAnswer":"Hello from Agent Kit.","rounds":1,...}
```

### Understanding the code

The program registers a scripted `IChatClient`, adds Agent Kit services with JSONL storage, and maps endpoints at `/agentkit`. See the [sample README](../samples/AgentKit.AspNetCore/README.md) for a full walkthrough.

### Connecting a real AI provider

```csharp
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var client = new OpenAIClient(builder.Configuration["OpenAI:ApiKey"]!);
    return client.AsChatClient("gpt-4.1-mini");
});
```

Add to `appsettings.json`:
```json
{"OpenAI": {"ApiKey": "your-api-key-here"}}
```
---

## DevOps Incident Investigation — Evidence-oriented console agent

### What this sample does

Imagine a monitoring system detects that the "checkout-api" service is getting slow. This sample shows how an AI agent can investigate what went wrong by looking at metrics, logs, and deployment history — just like a human DevOps engineer would. The agent gathers evidence, classifies it, looks up relevant runbooks, and writes a report with uncertainty language. It does NOT try to fix anything.

### How to run it

```powershell
.\samples\run-sample.ps1 incident
```

Or directly:
```bash
dotnet run --project samples/DevOps.IncidentInvestigation
```

Expected output:
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

### What happens when you run it — step by step

The agent follows a 7-step investigation. Each step is a tool call:

1. **get_service_snapshot("checkout-api")** — Gets basic info about the service. The agent needs to know what it's investigating.
2. **get_metric_window("checkout-api", "latency_and_pool", ...)** — Gets metrics showing p95 latency at 1850ms and database pool at 94%. The agent confirms the latency spike is real.
3. **get_recent_deployments("checkout-api")** — Finds a deployment at 14:00, 5 minutes before the spike. A recent code change is a common cause.
4. **read_log_excerpt("checkout-api", ...)** — Reads logs showing timeout messages after 14:05. Confirms errors at the time of the incident.
5. **classify_incident_evidence(...)** — A ProtoScript tool that classifies the evidence: severity "high", signals "database_pool_saturation" and "recent_deployment_correlation". Returns a classification, NOT a root cause.
6. **search_runbooks("database pool saturation")** — Finds the relevant runbook for this type of problem.
7. **Final answer** — Writes an incident report with the evidence, the plausible explanation, the recommended runbook, and an explicit statement not to perform remediation.

### Understanding the code

See the [sample README](../samples/DevOps.IncidentInvestigation/README.md) for a complete file-by-file walkthrough.

Key files:
- `Program.cs` — Entry point. Loads tools, creates runtime, runs one turn.
- `Tools/IncidentFunctions.cs` — 5 C# tools for reading data (metrics, logs, deployments, runbooks).
- `AgentTools/IncidentRules.pts` — ProtoScript evidence classifier.
- `Scenarios/IncidentScenarioFactory.cs` — The scripted flow (7 responses).
- `Data/` — Synthetic data files (services, metrics, logs, deployments, runbooks).

### Connecting a real AI provider

```csharp
using OpenAI;
IChatClient chat = new OpenAIClient(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
    .AsChatClient("gpt-4.1-mini");
var runtime = new AgentKitRuntime(chat, tools, eventSink: events);
```

With a real model, the agent decides which tools to call and in what order based on the system message and user prompt. The tools, data, and event sinks stay the same.

---

## Medical Referral Readiness — Read-only administrative workflow

### What this sample does

Imagine you work in a medical office and need to check if a patient's referral packet is complete before scheduling. This sample shows how an AI agent can review referral paperwork and identify what's missing — without making any medical decisions. The agent checks a synthetic referral (REF-1003) for an Orthopedic consultation and reports which administrative items are missing.

**This is administrative only.** The agent does not diagnose, triage, treat, or provide medical advice. Read `SAFETY.md` before adapting.

### How to run it

```powershell
.\samples\run-sample.ps1 medical
```

Or directly:
```bash
dotnet run --project samples/Medical.ReferralReadiness
```

Open `http://127.0.0.1:5101/referrals.html` in a browser.

### What happens when you run it — step by step

1. **get_referral_facts("REF-1003")** — Gets the referral record: patient, service line, what's already in the packet.
2. **get_referral_requirements("Orthopedic consultation")** — Gets the list of required items for this type of consultation.
3. **assess_referral_readiness(...)** — A ProtoScript tool that compares what's present vs. what's required. Returns: missing "signed referral order" and "relevant imaging report."
4. **Final answer** — "REF-1003 is not administratively ready for scheduling. Missing items: signed referral order and relevant imaging report." Plus a neutral draft request and a safety qualifier.

### Understanding the code

See the [sample README](../samples/Medical.ReferralReadiness/README.md) for a complete file-by-file walkthrough.

Key files:
- `Program.cs` — ASP.NET Core setup with Agent Kit, DI, and endpoint mapping.
- `Tools/ReferralFunctions.cs` — 3 C# tools that delegate to IReferralRepository.
- `AgentTools/ReferralRules.pts` — ProtoScript readiness assessment rule.
- `Repositories/IReferralRepository.cs` — The adaptation seam (implement this for real data).
- `Scenarios/ReferralScenarioFactory.cs` — The scripted flow (4 responses).
- `Data/` — Synthetic data (patients, referrals, requirements, offices).
- `SAFETY.md` — Safety documentation. Read before adapting.

### Adapting the data source

Implement `IReferralRepository` with your real data access and register it:
```csharp
builder.Services.AddSingleton<IReferralRepository>(new EhrReferralRepository(connectionString));
```

Tools, ProtoScript rules, and runtime stay unchanged.

### Connecting a real AI provider

```csharp
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var client = new OpenAIClient(builder.Configuration["OpenAI:ApiKey"]!);
    return client.AsChatClient("gpt-4.1-mini");
});
```
---

## Commerce Return Resolution — Controlled side-effect with human approval

### What this sample does

Imagine a customer wants to return a damaged product. This sample shows how an AI agent can review the order, check the return policy, evaluate eligibility, calculate the refund amount, and create a proposal — but a HUMAN has to approve it before any money is actually refunded.

The key safety pattern: the agent can propose, but only a human can approve. The approval endpoint is NOT a tool — the AI model doesn't even know it exists.

### How to run it

```powershell
.\samples\run-sample.ps1 returns
```

Or directly:
```bash
dotnet run --project samples/Commerce.ReturnResolution
```

Open `http://127.0.0.1:5102/orders.html` in a browser.

### What happens when you run it — step by step

1. **get_order_facts("ORD-1042")** — Gets order details: what was ordered, when delivered, how much it cost.
2. **get_return_policy("STANDARD-30")** — Gets the return policy: 30-day window, damaged items eligible, no restocking fee.
3. **evaluate_return_eligibility(...)** — A ProtoScript tool that checks: 6 days since delivery (within window), reason "damaged", condition "damaged", not final sale → eligible.
4. **calculate_refund_amount(...)** — A ProtoScript tool that calculates: $84.95 merchandise, no restocking fee, no shipping refund → 8495 cents.
5. **create_refund_proposal(...)** — A C# tool that writes a pending proposal to the store. Status: "pending_human_approval." No money has moved.
6. **Final answer** — "Prepared refund proposal for ORD-1042. No refund has been approved, issued, transmitted, or settled; a human must approve."

### Testing the human approval flow

After the agent creates a proposal:

```bash
# List proposals
curl http://127.0.0.1:5102/api/refund-proposals

# Approve a proposal (human action, NOT an agent tool)
curl -X POST http://127.0.0.1:5102/api/refund-proposals/{proposalId}/approve
```

### Understanding the code

See the [sample README](../samples/Commerce.ReturnResolution/README.md) for a complete file-by-file walkthrough.

Key files:
- `Program.cs` — ASP.NET Core setup. Includes the approval endpoint (NOT a tool).
- `Tools/ReturnFunctions.cs` — 4 C# tools. Notice: NO approve/issue/refund tool.
- `AgentTools/ReturnPolicyRules.pts` — 2 ProtoScript tools (eligibility + refund calculation).
- `Repositories/IOrderRepository.cs` — Order data access seam.
- `Repositories/IRefundProposalStore.cs` — Proposal store seam (includes ApproveAsync, but only called from the endpoint, never from a tool).
- `Scenarios/ReturnScenarioFactory.cs` — The scripted flow (6 responses).
- `Data/` — Synthetic data (orders, customers, policies, messages).

### Adapting the data source

Implement `IOrderRepository` and `IRefundProposalStore` with your real systems:
```csharp
builder.Services.AddSingleton<IOrderRepository>(new ShopifyOrderRepository(apiKey));
builder.Services.AddSingleton<IRefundProposalStore>(new PaymentSystemProposalStore(config));
```

Keep the approval endpoint outside the tool set. The agent can propose; only the application can approve.

### Connecting a real AI provider

```csharp
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var client = new OpenAIClient(builder.Configuration["OpenAI:ApiKey"]!);
    return client.AsChatClient("gpt-4.1-mini");
});
```

The model still cannot approve refunds — no such tool exists.

---

## Shared sample infrastructure

Path: `samples/Buffaly.AgentKit.SampleSupport`

This project provides shared infrastructure used by all domain samples:

| Component | What it does |
| --- | --- |
| `ScriptedChatClient` | Deterministic IChatClient that validates conversation state and emits scripted responses. Think of it as a test double for a real AI model. |
| `ScriptedChatResponse` | One scripted response — either a tool call or final text. |
| `ScenarioDefinition` | A scenario ID and an ordered list of scripted responses. |
| `JsonFixtureStore` | Bounded JSON fixture access for sample data. |
| `ConsoleAgentEventSink` | Prints events to the console as they happen (compact format). |
| `JsonlAgentEventSink` | Writes events to a JSONL file (one JSON object per line). |
| `SampleChatClientFactory` | Creates scripted clients from scenario definitions. |
| `DeterministicClock` | Fixed timestamp helper for deterministic behavior. |

You don't need to modify this project to use the samples. It's shared infrastructure.

---

## General troubleshooting

### Build fails

```bash
dotnet build
```

Check the output for errors. Common issues:
- Wrong .NET SDK version (need 9.0.300)
- Missing project references (make sure you're building from the repo root)
- NuGet restore issues (try `dotnet restore` first)

### "No scripted response remains"

The scripted client ran out of responses. This means the agent made more tool calls than the script expected. Check that the scenario factory matches the actual data and tool behavior.

### ProtoScript tool doesn't load

Check that:
1. `agentkit.json` exists in the `AgentTools/` directory
2. The `projectFile` field references a `.pts` file that exists
3. The JSON is valid (no syntax errors)
4. The exported prototype name matches the one in the `.pts` file

### Port already in use

ASP.NET samples use ports 5101 (medical) and 5102 (returns). If these are in use, specify a different port:
```bash
dotnet run --project samples/Medical.ReferralReadiness --urls http://127.0.0.1:YOUR_PORT
```

### Conversations not persisting

Check that the `.agentkit/` directory exists and is writable. The `UseJsonlStore` path must be a valid directory.

---

## Replacing the scripted provider

Every sample registers `IChatClient` with `SampleChatClientFactory.Create(...)`. To use a real provider, replace only that registration. The tools, ProtoScript manifests, repository seams, and runtime all stay unchanged.

### With OpenAI

```csharp
using OpenAI;

// For console samples:
IChatClient chat = new OpenAIClient(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!)
    .AsChatClient("gpt-4.1-mini");

// For ASP.NET samples:
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var client = new OpenAIClient(builder.Configuration["OpenAI:ApiKey"]!);
    return client.AsChatClient("gpt-4.1-mini");
});
```

Install: `dotnet add package OpenAI`

### With Azure OpenAI

```csharp
using Azure.AI.OpenAI;

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var endpoint = new Uri(builder.Configuration["AzureOpenAI:Endpoint"]!);
    var credential = new AzureKeyCredential(builder.Configuration["AzureOpenAI:ApiKey"]!);
    var client = new AzureOpenAIClient(endpoint, credential);
    return client.AsChatClient(builder.Configuration["AzureOpenAI:DeploymentName"]!);
});
```

Install: `dotnet add package Azure.AI.OpenAI`

### With other providers

Agent Kit works with any `IChatClient` implementation. See the [providers guide](providers.md) for more options.

With a real model, the agent decides for itself which tools to call and in what order, based on the system message and user input. You don't need to script the flow — the model handles that. Your tools, data, safety boundaries, and event sinks all stay the same.
