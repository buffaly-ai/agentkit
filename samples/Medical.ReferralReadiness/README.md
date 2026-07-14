# Medical Referral Readiness sample

This ASP.NET Core sample embeds Buffaly Agent Kit in a medical-administration workflow. It reviews synthetic referral packets for administrative completeness: signed order, insurance authorization, clinical summary, imaging, and referring-office information.

It does not make clinical decisions. It does not diagnose, triage, treat, interpret imaging, prioritize scheduling, or provide medical advice. Read [`SAFETY.md`](SAFETY.md) before adapting the sample.

## What it demonstrates

- ASP.NET Core hosting with `Buffaly.AgentKit.AspNetCore`.
- A read-only domain workflow: the agent can inspect referral data but cannot modify records.
- Mixed tool sources: C# repository tools plus a ProtoScript readiness rule.
- JSONL conversation persistence under `.agentkit/`.
- Static Agent Kit inspector at `/_agentkit`.
- A domain page at `/referrals.html`.

## Prerequisites

- .NET SDK `9.0.300` for this repository build.
- No API keys or network access are required in scripted mode.

## Run

From the repository root:

```powershell
.\samples\run-sample.ps1 medical
```

or directly:

```bash
dotnet run --project samples/Medical.ReferralReadiness
```

Open:

- `http://127.0.0.1:5101/referrals.html` when using the run script
- `/_agentkit` on the selected host for the Agent Kit inspector

## Scripted flow for REF-1003

The deterministic provider emits these real model-to-tool calls through Agent Kit:

1. `get_referral_facts("REF-1003")`
2. `get_referral_requirements("Orthopedic consultation")`
3. `assess_referral_readiness(serviceLine, hasSignedOrder=false, hasInsuranceAuthorization=true, hasClinicalSummary=true, hasRelevantImaging=false)`
4. final response with readiness status, missing administrative items, a neutral draft request, and an administrative-use qualifier

## Tools

C# tools from `Tools/ReferralFunctions.cs`:

- `get_referral_facts(referral_id)`
- `get_referral_requirements(service_line)`
- `get_referring_office(referral_id)`

ProtoScript tool from `AgentTools/ReferralRules.pts` and `AgentTools/agentkit.json`:

- `assess_referral_readiness(...)`

## Data fixtures

All records contain synthetic values and `SyntheticData: true` fields.

- `Data/patients.json`
- `Data/referrals.json`
- `Data/referral-requirements.json`
- `Data/referring-offices.json`

Seed referrals:

- `REF-1001`: ready
- `REF-1002`: missing insurance authorization
- `REF-1003`: missing signed order and relevant imaging
- `REF-1004`: missing referring provider contact information

## Adaptation seam

`Repositories/IReferralRepository.cs` defines the domain boundary:

```csharp
Task<ReferralFacts?> GetReferralAsync(string referralId, CancellationToken cancellationToken);
Task<ReferralRequirements?> GetRequirementsAsync(string serviceLine, CancellationToken cancellationToken);
Task<ReferringOffice?> GetReferringOfficeAsync(string referralId, CancellationToken cancellationToken);
```

A real application should replace `JsonReferralRepository` with an EHR/workflow adapter only after independent privacy, security, compliance, and clinical review. The Agent Kit runtime and ProtoScript rule can remain unchanged.

## Replacing the scripted client

`Program.cs` registers:

```csharp
builder.Services.AddSingleton<IChatClient>(SampleChatClientFactory.Create(ReferralScenarioFactory.Create()));
```

Replace that registration with your provider-backed `IChatClient`. Keep the tool registrations unchanged.

## File structure

```text
samples/Medical.ReferralReadiness/
  AgentTools/
  Data/
  Domain/
  Repositories/
  Scenarios/
  Tools/
  wwwroot/referrals.html
  Program.cs
  SAFETY.md
  README.md
```

## Test

`tests/Buffaly.AgentKit.SampleTests/ReferralReadinessScenarioTests.cs` verifies the deterministic REF-1003 path.
