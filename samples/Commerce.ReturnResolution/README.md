# Commerce Return Resolution sample

This ASP.NET Core sample embeds Buffaly Agent Kit in a customer-support return workflow. It demonstrates a controlled side effect: the agent may create a refund proposal, but human approval remains outside the model-visible tool set.

## What it demonstrates

- ASP.NET Core hosting and Agent Kit inspector.
- Mixed C# and ProtoScript tools.
- Local JSON persistence under `.agentkit/`.
- Proposal creation as a model-visible tool.
- Approval as a normal application endpoint, not a tool.

## Human approval boundary

The route:

```text
POST /api/refund-proposals/{proposalId}/approve
```

is not an `AIFunction`, is not in `AgentTools/agentkit.json`, and is never included in `ChatOptions.Tools`. The model can investigate, evaluate, calculate, and propose. A human user approves through the application.

## Run

```powershell
.\samples\run-sample.ps1 returns
```

or:

```bash
dotnet run --project samples/Commerce.ReturnResolution
```

Open `http://127.0.0.1:5102/orders.html` when using the run script.

## Scripted flow for ORD-1042

1. `get_order_facts("ORD-1042")`
2. `get_return_policy("STANDARD-30")`
3. `evaluate_return_eligibility(daysSinceDelivery=6, reason="damaged", itemCondition="damaged", isFinalSale=false)`
4. `calculate_refund_amount(merchandiseAmountCents=8495, shippingAmountCents=795, restockingPercent=0, refundShipping=false)`
5. `create_refund_proposal(...)`
6. final answer with proposal status and an explicit statement that approval is still required

## Tools

C# tools from `Tools/ReturnFunctions.cs`:

- `get_order_facts(order_id)`
- `get_return_policy(policy_id)`
- `get_customer_message(order_id)`
- `create_refund_proposal(order_id, amount, reason, evidence)`

ProtoScript tools:

- `evaluate_return_eligibility(...)`
- `calculate_refund_amount(...)`

## Data fixtures

- `Data/orders.json`
- `Data/customers.json`
- `Data/return-policies.json`
- `Data/customer-messages.json`

Seed orders:

- `ORD-1041`: eligible unopened item
- `ORD-1042`: damaged item within return window
- `ORD-1043`: final-sale item, ineligible
- `ORD-1044`: late/partial delivery, manual review

## Adaptation seams

- `Repositories/IOrderRepository.cs`
- `Repositories/IRefundProposalStore.cs`

Replace the JSON implementations with application services. Keep the approve endpoint outside the model-visible tool set.

## Replacing the scripted client

`Program.cs` registers `SampleChatClientFactory.Create(ReturnScenarioFactory.Create())`. Replace that singleton with a real provider-backed `IChatClient`; keep tool and repository registrations intact.

## File structure

```text
samples/Commerce.ReturnResolution/
  AgentTools/
  Data/
  Domain/
  Repositories/
  Scenarios/
  Tools/
  wwwroot/orders.html
  Program.cs
  README.md
```

## Test

`tests/Buffaly.AgentKit.SampleTests/ReturnResolutionScenarioTests.cs` verifies proposal creation, ProtoScript invocation, order immutability, and the absence of approve/issue tools.
