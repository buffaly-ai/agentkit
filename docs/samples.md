# Samples walkthrough

The repository includes three domain-specific samples plus shared sample infrastructure. They use synthetic data, deterministic scripted providers, real Agent Kit tool rounds, and mixed C# plus ProtoScript tools.

## Shared guarantees

- No network calls or API keys in default mode.
- All domain data is synthetic.
- Each sample loads at least one C# `AIFunction` and at least one ProtoScript tool.
- The scripted provider emits real `FunctionCallContent`; Agent Kit performs normal tool dispatch.
- Web samples persist under local `.agentkit/` directories.
- Tests exercise deterministic end-to-end scenarios.

## Run commands

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

Direct commands also work:

```bash
dotnet run --project samples/Medical.ReferralReadiness
dotnet run --project samples/Commerce.ReturnResolution
dotnet run --project samples/DevOps.IncidentInvestigation
```

## Sample support

Path: `samples/Buffaly.AgentKit.SampleSupport`

Important files:

- `ScriptedChatClient.cs`: validates observed conversation state and emits scripted tool calls/final messages.
- `ScriptedChatResponse.cs`: one scripted provider response.
- `ScenarioDefinition.cs`: ordered scenario responses.
- `JsonFixtureStore.cs`: bounded JSON fixture access.
- `ConsoleAgentEventSink.cs`: compact console trace.
- `JsonlAgentEventSink.cs`: JSONL event writer without ASP.NET dependency.

## Medical referral readiness

Path: `samples/Medical.ReferralReadiness`

Demonstrates an ASP.NET read-only administrative workflow. The assistant reviews synthetic referral packet completeness for `REF-1003`.

Run:

```powershell
.\samples\run-sample.ps1 medical
```

Default URL: `http://127.0.0.1:5101/referrals.html`.

Scripted flow:

1. `get_referral_facts("REF-1003")`
2. `get_referral_requirements("Orthopedic consultation")`
3. `assess_referral_readiness(...)`
4. final answer with missing administrative items and safety qualifier

Tools:

- C#: `get_referral_facts`, `get_referral_requirements`, `get_referring_office`
- ProtoScript: `assess_referral_readiness`

Fixtures:

- `Data/patients.json`
- `Data/referrals.json`
- `Data/referral-requirements.json`
- `Data/referring-offices.json`

Adaptation seam:

- `Repositories/IReferralRepository.cs`
- Replace `JsonReferralRepository` with an application repository while keeping tools and rules stable.

Safety:

This sample is administrative only. It is not diagnosis, triage, treatment, medical advice, or scheduling priority. See `SAFETY.md`.

Acceptance test:

`tests/Buffaly.AgentKit.SampleTests/ReferralReadinessScenarioTests.cs` verifies three tool calls, ProtoScript invocation, expected missing items, event ordering, and no diagnostic/treatment recommendation.

## Commerce return resolution

Path: `samples/Commerce.ReturnResolution`

Demonstrates a controlled side effect. The agent may create a refund proposal, but cannot approve or issue a refund.

Run:

```powershell
.\samples\run-sample.ps1 returns
```

Default URL: `http://127.0.0.1:5102/orders.html`.

Scripted flow for `ORD-1042`:

1. `get_order_facts("ORD-1042")`
2. `get_return_policy("STANDARD-30")`
3. `evaluate_return_eligibility(...)`
4. `calculate_refund_amount(...)`
5. `create_refund_proposal(...)`
6. final answer stating approval is still required

Tools:

- C#: `get_order_facts`, `get_return_policy`, `get_customer_message`, `create_refund_proposal`
- ProtoScript: `evaluate_return_eligibility`, `calculate_refund_amount`

Human approval boundary:

`POST /api/refund-proposals/{proposalId}/approve` is a normal application endpoint. It is not an `AIFunction`, not in `AgentTools/agentkit.json`, and not model-visible.

Adaptation seams:

- `Repositories/IOrderRepository.cs`
- `Repositories/IRefundProposalStore.cs`

Acceptance test:

`ReturnResolutionScenarioTests.cs` verifies ProtoScript eligibility/refund tools run, one pending proposal is written, the order fixture remains unchanged, no approve/issue tool exists, and the final answer does not claim a refund occurred.

## DevOps incident investigation

Path: `samples/DevOps.IncidentInvestigation`

Demonstrates headless embedding without ASP.NET. It investigates a synthetic `checkout-api` latency incident and writes report/event/conversation artifacts.

Run:

```powershell
.\samples\run-sample.ps1 incident
```

Output:

- `output/incident-report.md`
- `output/events.jsonl`
- `output/conversation.json`

Scripted flow:

1. `get_service_snapshot("checkout-api")`
2. `get_metric_window("checkout-api", ...)`
3. `get_recent_deployments("checkout-api")`
4. `read_log_excerpt("checkout-api", ...)`
5. `classify_incident_evidence(...)`
6. `search_runbooks("database pool saturation")`
7. final incident report with uncertainty language

Tools:

- C#: `get_service_snapshot`, `get_metric_window`, `read_log_excerpt`, `get_recent_deployments`, `search_runbooks`
- ProtoScript: `classify_incident_evidence`

Fixtures:

- `Data/services.json`
- `Data/deployments.json`
- `Data/metrics/*.json`
- `Data/logs/*.log`
- `Data/runbooks/*.md`

Acceptance test:

`IncidentInvestigationScenarioTests.cs` verifies no ASP.NET reference, bounded tool set, ProtoScript classifier invocation, evidence references, uncertainty language, and no remediation tool.

## Replacing scripted clients

Each sample registers `IChatClient` with `SampleChatClientFactory.Create(...)`. Replace that registration with a real provider factory. Do not change the tools, repositories, ProtoScript manifests, or runtime wiring unless your application needs different domain behavior.
