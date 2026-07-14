# Medical Referral Readiness sample

## What this sample does

Imagine you work in a medical office and need to check if a patient's referral packet is complete before scheduling an appointment. A referral packet might need a signed order from the referring doctor, insurance authorization, a clinical summary, relevant imaging reports, and referring office contact information. This sample shows how an AI agent can review that paperwork and identify what's missing — without making any medical decisions.

The agent reviews a synthetic referral called REF-1003 for an "Orthopedic consultation." It checks what's in the packet, compares it against the requirements for that type of consultation, and reports which administrative items are missing. The agent then drafts a neutral request message asking for the missing items.

**This is an administrative sample, not a clinical one.** The agent does not diagnose, triage, treat, interpret imaging, prioritize scheduling, or provide medical advice. It only checks whether administrative paperwork is complete. Read [`SAFETY.md`](SAFETY.md) before adapting this sample.

This is an **ASP.NET Core sample** — it runs as a web server. You can interact with it through a browser or HTTP endpoints.

## Prerequisites

- .NET SDK 9.0.300 (run `dotnet --version` to check)
- A terminal (PowerShell, cmd, or bash)
- Optional: a web browser or `curl` for testing endpoints
- No API key, no internet, no database — everything runs locally with synthetic data

## How to run it

From the repository root:

```powershell
.\samples\run-sample.ps1 medical
```

Or directly:

```bash
dotnet run --project samples/Medical.ReferralReadiness
```

The server starts on `http://127.0.0.1:5101`. Open these URLs in a browser:

- `http://127.0.0.1:5101/referrals.html` — the referral review page
- `http://127.0.0.1:5101/_agentkit/` — the Agent Kit inspector page

You should see server startup output like:

```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://127.0.0.1:5101
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

## What just happened? — The 4-step flow explained

When you start the sample, the scripted agent automatically runs a review of referral REF-1003. Here's what happens step by step:

### Step 1: Get the referral facts

The agent calls `get_referral_facts("REF-1003")`. This returns the referral record: who is the patient, what service line is being requested, what items are already in the packet.

**Why start here?** The agent needs to know what referral it's reviewing and what's already been submitted. It's like opening a patient's file and seeing what paperwork is there.

### Step 2: Get the requirements

The agent calls `get_referral_requirements("Orthopedic consultation")`. This returns the list of items required for an orthopedic consultation referral: signed order, insurance authorization, clinical summary, relevant imaging, referring office info.

**Why next?** The agent needs to know what SHOULD be in the packet before it can check what's missing. Different types of consultations may require different items — an orthopedic consultation might need imaging, while a psychiatric consultation might not.

### Step 3: Assess readiness

The agent calls `assess_referral_readiness(...)` — this is a **ProtoScript tool**, not a C# tool. It takes the requirements and the current state of the packet (which items are present and which are missing) and produces a readiness assessment.

**Why use a ProtoScript tool for this?** The readiness rules are business logic: "a referral is ready when all required items are present." Writing this in ProtoScript makes the rules explicit, inspectable, and changeable without recompiling C# code. If your organization has different rules (e.g., some items are optional, or some have grace periods), you change the ProtoScript file.

For REF-1003, the assessment returns: status "needs_information," missing items: "signed referral order" and "relevant imaging report."

### Step 4: Final answer

The agent produces its final response: "REF-1003 is not administratively ready for scheduling. Missing administrative items: signed referral order and relevant imaging report." It also drafts a neutral request message: "Please send the signed referral order and relevant imaging report for REF-1003 so the scheduling packet can be completed."

The response ends with a safety qualifier: "This is an administrative completeness review only; it is not diagnosis, triage, treatment, or clinical advice."

**Why a safety qualifier?** Because this sample deals with medical-adjacent data, the agent explicitly states what it is NOT doing. This is important for real-world use where someone might mistake an administrative review for a clinical recommendation.
## Understanding the code — File by file

### `Program.cs` — The entry point

This is the main program. It sets up an ASP.NET Core web app with Agent Kit embedded in it. Here's what it does:

1. **Creates the web app builder** — standard ASP.NET Core setup
2. **Registers the referral repository** — a `JsonReferralRepository` that reads synthetic data from JSON files
3. **Registers the scripted chat client** — the fake "AI" that follows a predetermined script
4. **Registers the tools** — combines C# tools (data readers) and ProtoScript tools (readiness rules) into one list
5. **Adds Agent Kit services** — `AddBuffalyAgentKit` with JSONL conversation storage
6. **Maps endpoints** — maps Agent Kit at `/_agentkit` and adds a `/referrals` endpoint

Key code:

```csharp
// Register the data repository (reads from JSON files)
builder.Services.AddSingleton<IReferralRepository>(new JsonReferralRepository(dataRoot));

// Register the scripted "AI" (no API key needed)
builder.Services.AddSingleton<IChatClient>(
    SampleChatClientFactory.Create(ReferralScenarioFactory.Create()));

// Register tools (C# data tools + ProtoScript readiness rule)
builder.Services.AddSingleton<IReadOnlyList<AIFunction>>(sp =>
{
    var repo = sp.GetRequiredService<IReferralRepository>();
    ProtoScriptToolSet proto = ProtoScriptToolSet.LoadAsync(
        Path.Combine(contentRoot, "AgentTools", "agentkit.json")).GetAwaiter().GetResult();
    return ReferralFunctions.Create(repo).Concat(proto.Tools).ToArray();
});

// Add Agent Kit with JSONL conversation storage
builder.Services.AddBuffalyAgentKit(b => b.UseJsonlStore(storageRoot));

// Map endpoints
app.MapBuffalyAgentKit("/_agentkit");
app.MapGet("/referrals", async (IReferralRepository repo, CancellationToken ct) =>
    Results.Json(await repo.ListReferralsAsync(ct)));
```

**What is dependency injection (DI)?** ASP.NET Core uses DI to share services across your app. When you register `IReferralRepository` here, any part of the app that needs referral data gets the same instance. This makes it easy to swap implementations — for example, replacing the JSON repository with one that reads from a real EHR system.

**What is `AddBuffalyAgentKit`?** This method registers all the Agent Kit services needed for ASP.NET Core: conversation management, the HTTP endpoints, the inspector page, and conversation storage. You configure it with a lambda that sets up storage and optional ProtoScript tools.

**What is `MapBuffalyAgentKit`?** This maps all Agent Kit HTTP endpoints under a prefix. Here it's `/_agentkit`, so endpoints are at `/_agentkit/health`, `/_agentkit/api/conversations`, etc. The inspector page is at `/_agentkit/`.

### `Tools/ReferralFunctions.cs` — C# data tools

This file creates 3 C# tools:

| Tool name | What it does | Parameters |
| --- | --- | --- |
| `get_referral_facts` | Gets referral details from the repository | `referral_id` |
| `get_referral_requirements` | Gets requirements for a service line | `service_line` |
| `get_referring_office` | Gets referring office contact info | `referral_id` |

Each tool delegates to the `IReferralRepository` — it doesn't read files directly. This is the **repository pattern**: the tools depend on an interface, not a concrete implementation. You can swap the data source without changing the tools.

### `AgentTools/ReferralRules.pts` — ProtoScript readiness rule

```protoscript
prototype AssessReferralReadiness
{
    function Execute(string serviceLine, bool hasSignedOrder,
        bool hasInsuranceAuthorization, bool hasClinicalSummary,
        bool hasRelevantImaging): string
    {
        return "{\"status\":\"needs_information\",\"missingItems\":[...],\"administrativeOnly\":true}";
    }
}
```

This ProtoScript tool takes the presence/absence of each required item and returns a readiness assessment. The `administrativeOnly: true` field is a safety marker — it reminds consumers that this assessment is administrative, not clinical.

### `Repositories/IReferralRepository.cs` — The adaptation seam

```csharp
public interface IReferralRepository
{
    Task<ReferralFacts?> GetReferralAsync(string referralId, CancellationToken ct);
    Task<ReferralRequirements?> GetRequirementsAsync(string serviceLine, CancellationToken ct);
    Task<ReferringOffice?> GetReferringOfficeAsync(string referralId, CancellationToken ct);
}
```

This interface is the boundary between the sample and your real system. The sample provides `JsonReferralRepository` (reads from JSON files). To use real data, implement this interface with your EHR/workflow adapter and register it instead:

```csharp
// Replace this:
builder.Services.AddSingleton<IReferralRepository>(new JsonReferralRepository(dataRoot));

// With this:
builder.Services.AddSingleton<IReferralRepository>(new EhrReferralRepository(connectionString));
```

The tools, ProtoScript rules, and Agent Kit runtime stay unchanged. Only the data source changes.

### `Scenarios/ReferralScenarioFactory.cs` — The script

This defines what the scripted "AI" does: 4 responses (3 tool calls + 1 final answer). Each tool call includes validation that the previous tool completed successfully with the expected data.

### `Data/` — Synthetic data files

All data is synthetic (fake). The files include:
- `patients.json` — synthetic patient records
- `referrals.json` — synthetic referral records (REF-1001 through REF-1004)
- `referral-requirements.json` — requirements per service line
- `referring-offices.json` — referring office contact info

Seed referrals for testing:
- `REF-1001`: ready (all items present)
- `REF-1002`: missing insurance authorization
- `REF-1003`: missing signed order and relevant imaging (the default scenario)
- `REF-1004`: missing referring provider contact information

### `SAFETY.md` — Safety documentation

This file documents the safety boundaries of the sample. Read it before adapting the sample for real use. Key points: all records are synthetic, the sample is administrative only, and it must not be connected to an EHR without independent security, privacy, compliance, and clinical review.

### `wwwroot/referrals.html` — The domain page

A simple HTML page that lets you view referrals and trigger reviews from a browser. This is separate from the Agent Kit inspector page — it's a domain-specific UI for the referral workflow.

## How to adapt this sample

### Connect to a real data source

Implement `IReferralRepository` with your real data access:

```csharp
public class EhrReferralRepository : IReferralRepository
{
    private readonly string _connectionString;

    public EhrReferralRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ReferralFacts?> GetReferralAsync(string referralId, CancellationToken ct)
    {
        // Query your EHR or workflow system
        // Map the result to ReferralFacts
        return new ReferralFacts { /* ... */ };
    }

    // ... implement other methods
}
```

Register it in `Program.cs`:
```csharp
builder.Services.AddSingleton<IReferralRepository>(
    new EhrReferralRepository(builder.Configuration["Ehr:ConnectionString"]!));
```

### Change the readiness rules

Edit `AgentTools/ReferralRules.pts` to change what items are required or add conditional logic. For example, you could make imaging optional for certain service lines, or add a grace period for expired authorizations. No C# recompilation needed — ProtoScript is interpreted at runtime.

## Connecting a real AI provider

Replace the scripted client with a real one. In `Program.cs`:

```csharp
// Before (scripted):
builder.Services.AddSingleton<IChatClient>(
    SampleChatClientFactory.Create(ReferralScenarioFactory.Create()));

// After (real provider):
using OpenAI;
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
  "OpenAI": { "ApiKey": "your-api-key-here" }
}
```

With a real model, the agent will decide for itself which tools to call and in what order, based on the system message and user input. The tools, data, and safety rules all stay the same.

## Using the inspector page

Open `http://127.0.0.1:5101/_agentkit/` in a browser. The inspector page lets you:
- Create new conversations
- Send messages to the agent
- View tool calls and results
- See event traces

This is a debugging tool — use it to understand how the agent is behaving and what tools it's calling.

## Troubleshooting

### The server won't start
- Check that port 5101 is not already in use
- Make sure you're running from the repository root
- Run `dotnet build` first to check for build errors

### The agent doesn't call any tools
- Check that the tool manifest (`AgentTools/agentkit.json`) exists and is valid
- Check that the ProtoScript file referenced in the manifest exists
- Look at the server console for any error messages

### Conversations are not persisting
- Check that the `.agentkit/` directory exists and is writable
- The `UseJsonlStore` path must be a valid directory

## File structure

```text
samples/Medical.ReferralReadiness/
  AgentTools/
    ReferralRules.pts        # ProtoScript readiness rule
    agentkit.json            # Tool manifest
  Data/
    patients.json            # Synthetic patient records
    referrals.json           # Synthetic referral records
    referral-requirements.json
    referring-offices.json
  Domain/
    ReferralModels.cs        # Domain models (ReferralFacts, etc.)
  Repositories/
    IReferralRepository.cs   # The adaptation seam (implement this for real data)
    JsonReferralRepository.cs # JSON file implementation
  Scenarios/
    ReferralScenarioFactory.cs # Scripted flow
  Tools/
    ReferralFunctions.cs     # C# data tools
  wwwroot/
    referrals.html           # Domain page
  Program.cs                 # Entry point
  SAFETY.md                  # Safety documentation
  README.md
```

## Key Agent Kit concepts demonstrated

| Concept | How this sample uses it |
| --- | --- |
| **ASP.NET Core hosting** | Agent Kit is hosted inside an ASP.NET Core web app |
| **Dependency injection** | Repository, chat client, and tools are all registered via DI |
| **Repository pattern** | Tools depend on `IReferralRepository`, not a concrete data source |
| **Mixed tool sources** | 3 C# tools + 1 ProtoScript tool |
| **ProtoScript tool** | The readiness assessment rule is written in ProtoScript |
| **Safety boundary** | The agent is read-only — it can inspect but not modify records |
| **JSONL storage** | Conversations persist as JSONL files under `.agentkit/` |
| **Inspector page** | Built-in web UI for debugging agent behavior |
| **Domain page** | Custom HTML page for the referral workflow |

## Test

`tests/Buffaly.AgentKit.SampleTests/ReferralReadinessScenarioTests.cs` verifies:
- Three tool calls are made in the correct order
- The ProtoScript readiness rule is invoked
- Expected missing items are identified
- Event ordering is correct
- No diagnostic or treatment recommendations appear in the output

## Where to go next

- **[Samples overview](../README.md)** — See all available samples
- **[DevOps Incident Investigation](../DevOps.IncidentInvestigation/README.md)** — See a console/headless sample
- **[Commerce Return Resolution](../Commerce.ReturnResolution/README.md)** — See an ASP.NET sample with human approval
- **[ASP.NET guide](../../docs/aspnet.md)** — Learn more about hosting Agent Kit in ASP.NET Core
- **[Getting started guide](../../docs/getting-started.md)** — Build your own Agent Kit app from scratch
