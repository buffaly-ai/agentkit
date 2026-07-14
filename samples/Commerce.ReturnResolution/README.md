# Commerce Return Resolution sample

## What this sample does

Imagine a customer wants to return a damaged product. This sample shows how an AI agent can review the order, check the return policy, evaluate eligibility, calculate the refund amount, and create a proposal — but a HUMAN has to approve it before any money is actually refunded.

The agent processes a return for order ORD-1042: a customer received a damaged item and wants a refund. The agent checks the order details, looks up the return policy, evaluates whether the return is eligible, calculates how much money should be refunded, and creates a pending refund proposal. But the agent cannot approve the proposal — that's a human decision. The agent can propose; only a person can approve.

This is an **ASP.NET Core sample** — it runs as a web server. You can interact with it through a browser or HTTP endpoints.

## The human approval boundary — why it matters

This sample demonstrates a critical safety pattern for AI agents: **the agent can recommend actions, but humans make the final call on anything with real consequences.**

The agent has access to tools that let it:
- Read order data (what was ordered, when it was delivered, how much it cost)
- Read return policies (what are the rules for returns)
- Evaluate eligibility (does this return meet the policy requirements)
- Calculate refund amounts (how much money should be refunded)
- Create a refund proposal (write a pending proposal to the store)

But the agent does NOT have access to:
- Approving a proposal
- Issuing a refund
- Sending money
- Modifying an order

The approval endpoint (`POST /api/refund-proposals/{id}/approve`) is a normal application endpoint — it's NOT a tool, NOT in the tool manifest, and NOT visible to the AI model. The model doesn't even know it exists. A human has to call that endpoint through the application UI or API.

This pattern is important because it prevents the AI from taking irreversible financial actions on its own. The agent does the research and preparation; the human makes the decision.

## Prerequisites

- .NET SDK 9.0.300 (run `dotnet --version` to check)
- A terminal (PowerShell, cmd, or bash)
- Optional: a web browser or `curl` for testing endpoints
- No API key, no internet, no database — everything runs locally with synthetic data

## How to run it

From the repository root:

```powershell
.\samples\run-sample.ps1 returns
```

Or directly:

```bash
dotnet run --project samples/Commerce.ReturnResolution
```

The server starts on `http://127.0.0.1:5102`. Open these URLs in a browser:

- `http://127.0.0.1:5102/orders.html` — the order review page
- `http://127.0.0.1:5102/_agentkit/` — the Agent Kit inspector page

You should see server startup output like:

```text
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://127.0.0.1:5102
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

## What just happened? — The 6-step flow explained

When you start the sample, the scripted agent automatically processes a return for order ORD-1042. Here's what happens step by step:

### Step 1: Get the order facts

The agent calls `get_order_facts("ORD-1042")`. This returns the order details: what was ordered, when it was delivered, how much it cost, the item condition.

**Why start here?** The agent needs to know what order we're dealing with. It's like a customer service rep first pulling up the order in the system before processing a return request.

### Step 2: Get the return policy

The agent calls `get_return_policy("STANDARD-30")`. This returns the return policy rules: 30-day return window, what conditions are eligible, whether there's a restocking fee.

**Why next?** The agent needs to know the rules before it can evaluate the return. Different products or order types might have different policies. The "STANDARD-30" policy allows returns within 30 days for damaged items with no restocking fee.

### Step 3: Evaluate eligibility

The agent calls `evaluate_return_eligibility(...)` — this is a **ProtoScript tool**. It takes the return details (days since delivery, reason, item condition, whether it's a final sale) and checks them against the policy rules.

**Why use a ProtoScript tool?** The eligibility rules are business logic: "if the item is damaged and within the return window and not a final sale, it's eligible." Writing this in ProtoScript makes the rules explicit and changeable without recompiling C# code. If your return policy changes, you edit the `.pts` file.

For ORD-1042, the evaluation returns: "damaged_item_within_window" — the item is damaged, it's within the 30-day window, and it's not a final sale, so the return is eligible.

### Step 4: Calculate the refund amount

The agent calls `calculate_refund_amount(...)` — another **ProtoScript tool**. It takes the merchandise amount, shipping amount, restocking percentage, and whether shipping should be refunded, then calculates the total refund.

**Why a separate step?** Calculating the refund is a distinct business operation with its own rules. For this order: merchandise was $84.95, shipping was $7.95, no restocking fee (damaged item), and shipping is not refunded. The calculation returns 8495 cents ($84.95).

### Step 5: Create the refund proposal

The agent calls `create_refund_proposal(...)` — this is a **C# tool** that writes a pending proposal to the proposal store. The proposal includes the order ID, refund amount, reason, and evidence from the eligibility evaluation.

**Why can the agent create a proposal but not approve it?** Creating a proposal is a reversible action — it just writes a pending record that a human can review, approve, or reject. No money has moved. The proposal status is "pending_human_approval." This is the key safety boundary: the agent can prepare everything, but the final decision belongs to a human.

### Step 6: Final answer

The agent produces its final response: "Prepared refund proposal for ORD-1042. Proposal ID will be shown in the application record and remains pending_human_approval. No refund has been approved, issued, transmitted, or settled; a human must approve the proposal through the normal application endpoint."

The response is very explicit about what has NOT happened: no refund has been approved, issued, or sent. This is deliberate — the agent is being transparent about the limits of what it did.
## Understanding the code — File by file

### `Program.cs` — The entry point

This sets up an ASP.NET Core web app with Agent Kit. Here's what it does:

1. **Creates the web app builder** — standard ASP.NET Core setup
2. **Registers repositories** — `IOrderRepository` for order data, `IRefundProposalStore` for proposals
3. **Registers the scripted chat client** — the fake "AI" that follows a predetermined script
4. **Registers the tools** — combines C# tools and ProtoScript tools into one list
5. **Adds Agent Kit services** — `AddBuffalyAgentKit` with JSONL conversation storage
6. **Maps endpoints** — maps Agent Kit at `/_agentkit`, adds order/proposal endpoints, and the approval endpoint

Key code:

```csharp
// Register repositories (data access)
builder.Services.AddSingleton<IOrderRepository>(new JsonOrderRepository(dataRoot));
builder.Services.AddSingleton<IRefundProposalStore>(new JsonRefundProposalStore(storageRoot));

// Register the scripted "AI" (no API key needed)
builder.Services.AddSingleton<IChatClient>(
    SampleChatClientFactory.Create(ReturnScenarioFactory.Create()));

// Register tools (C# data tools + ProtoScript rules)
builder.Services.AddSingleton<IReadOnlyList<AIFunction>>(sp =>
{
    var orders = sp.GetRequiredService<IOrderRepository>();
    var proposals = sp.GetRequiredService<IRefundProposalStore>();
    ProtoScriptToolSet proto = ProtoScriptToolSet.LoadAsync(
        Path.Combine(contentRoot, "AgentTools", "agentkit.json")).GetAwaiter().GetResult();
    return ReturnFunctions.Create(orders, proposals).Concat(proto.Tools).ToArray();
});

// Add Agent Kit
builder.Services.AddBuffalyAgentKit(b => b.UseJsonlStore(storageRoot));

// Map endpoints
app.MapBuffalyAgentKit("/_agentkit");
app.MapGet("/orders", ...);
app.MapGet("/api/refund-proposals", ...);

// THE APPROVAL ENDPOINT — this is NOT a tool, NOT visible to the AI
app.MapPost("/api/refund-proposals/{proposalId}/approve", ...);
```

**Notice the approval endpoint.** It's mapped as a normal ASP.NET Core endpoint — `app.MapPost(...)`. It is NOT registered as an `AIFunction`, NOT in `AgentTools/agentkit.json`, and NOT in the tool list. The AI model has no idea this endpoint exists. A human calls it through the application UI or API to approve a proposal.

### `Tools/ReturnFunctions.cs` — C# tools

This file creates 4 C# tools:

| Tool name | What it does | Parameters |
| --- | --- | --- |
| `get_order_facts` | Gets order details from the repository | `order_id` |
| `get_return_policy` | Gets return policy rules | `policy_id` |
| `get_customer_message` | Gets the customer's return request message | `order_id` |
| `create_refund_proposal` | Creates a pending refund proposal | `order_id`, `amount`, `reason`, `evidence` |

Notice what's NOT in this list: there is no `approve_refund`, `issue_refund`, `send_refund`, or `modify_order` tool. The agent physically cannot approve or issue a refund because no such tool exists in its tool set.

### `AgentTools/ReturnPolicyRules.pts` — ProtoScript rules

This file contains two ProtoScript tools:

**`evaluate_return_eligibility`** — checks if a return is eligible based on the policy:
- How many days since delivery?
- What's the reason for the return?
- What's the item condition?
- Is it a final sale item?

**`calculate_refund_amount`** — calculates how much money to refund:
- Merchandise amount (in cents)
- Shipping amount (in cents)
- Restocking percentage
- Whether shipping should be refunded

Both are business rules written in ProtoScript so they can be changed without recompiling C# code.
### `AgentTools/agentkit.json` — Tool manifest

Lists the two ProtoScript tools that are visible to the AI model. Only these tools (plus the C# tools registered in DI) are available to the agent. The approval endpoint is NOT here.

### `Repositories/IOrderRepository.cs` and `IRefundProposalStore.cs` — Adaptation seams

These interfaces are the boundary between the sample and your real system:

```csharp
public interface IOrderRepository
{
    Task<OrderFacts?> GetOrderAsync(string orderId, CancellationToken ct);
    Task<ReturnPolicy?> GetPolicyAsync(string policyId, CancellationToken ct);
    Task<CustomerMessage?> GetCustomerMessageAsync(string orderId, CancellationToken ct);
}

public interface IRefundProposalStore
{
    Task<RefundProposal> CreateAsync(CreateRefundProposalRequest request, CancellationToken ct);
    Task<RefundProposal?> GetAsync(string proposalId, CancellationToken ct);
    Task<IReadOnlyList<RefundProposal>> ListAsync(CancellationToken ct);
    Task<RefundProposal> ApproveAsync(string proposalId, CancellationToken ct);
}
```

The sample provides JSON file implementations. To use real data, implement these interfaces with your order management / payment system and register them instead. The tools, ProtoScript rules, and Agent Kit runtime stay unchanged.

**Important:** `ApproveAsync` is on the `IRefundProposalStore` interface, but it's only called from the approval endpoint — never from a tool. The agent never calls `ApproveAsync` because no tool wraps it.

### `Scenarios/ReturnScenarioFactory.cs` — The script

This defines what the scripted "AI" does: 6 responses (5 tool calls + 1 final answer). Each tool call includes validation that the previous tool completed successfully with the expected data.

### `Data/` — Synthetic data files

All data is synthetic (fake). The files include:
- `orders.json` — synthetic order records (ORD-1041 through ORD-1044)
- `customers.json` — synthetic customer records
- `return-policies.json` — return policy definitions
- `customer-messages.json` — customer return request messages

Seed orders for testing:
- `ORD-1041`: eligible unopened item
- `ORD-1042`: damaged item within return window (the default scenario)
- `ORD-1043`: final-sale item, ineligible
- `ORD-1044`: late/partial delivery, requires manual review

## Testing the approval flow manually

After the agent creates a proposal, you can test the human approval step:

### 1. List pending proposals

```bash
curl http://127.0.0.1:5102/api/refund-proposals
```

You'll see the proposal created by the agent, with status `pending_human_approval`.

### 2. Approve the proposal

```bash
curl -X POST http://127.0.0.1:5102/api/refund-proposals/{proposalId}/approve
```

Replace `{proposalId}` with the actual proposal ID from step 1. The proposal status changes to `approved`.

**This is the human approval boundary in action.** The agent created the proposal; a human approved it. The agent was never involved in the approval step.
## How to adapt this sample

### Connect to a real order system

Implement `IOrderRepository` with your real data access:

```csharp
public class ShopifyOrderRepository : IOrderRepository
{
    public async Task<OrderFacts?> GetOrderAsync(string orderId, CancellationToken ct)
    {
        // Query your order management system
        // Map the result to OrderFacts
        return new OrderFacts { /* ... */ };
    }
    // ... implement other methods
}
```

Register it in `Program.cs`:
```csharp
builder.Services.AddSingleton<IOrderRepository>(
    new ShopifyOrderRepository(builder.Configuration["Shopify:ApiKey"]!));
```

### Change the return policy rules

Edit `AgentTools/ReturnPolicyRules.pts` to change eligibility or refund calculation logic. For example, you could add a rule for holiday returns with an extended window, or change the restocking fee percentage. No C# recompilation needed.

### Add a new tool

Want to add a tool that checks inventory before approving a return? Add it to `ReturnFunctions.cs`:

```csharp
yield return AIFunctionFactory.Create(
    (string sku, CancellationToken ct) => inventory.GetStockAsync(sku, ct),
    "check_inventory",
    "Check current inventory for a SKU.");
```

Then update the scenario factory to call the new tool at the appropriate point.

## Connecting a real AI provider

Replace the scripted client with a real one. In `Program.cs`:

```csharp
// Before (scripted):
builder.Services.AddSingleton<IChatClient>(
    SampleChatClientFactory.Create(ReturnScenarioFactory.Create()));

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

With a real model, the agent will decide for itself which tools to call and in what order. The tools, data, safety boundaries, and approval endpoint all stay the same. The model still cannot approve refunds because no such tool exists.

## Troubleshooting

### The server won't start
- Check that port 5102 is not already in use
- Make sure you're running from the repository root
- Run `dotnet build` first to check for build errors

### The agent creates a proposal but I can't find it
- Check `GET /api/refund-proposals` to list all proposals
- The proposal is stored in the `.agentkit/` directory as a JSON file
- Make sure the `.agentkit/` directory exists and is writable

### The approval endpoint returns 404
- Make sure you're using the correct proposal ID
- The endpoint is `POST /api/refund-proposals/{proposalId}/approve`
- Check that the proposal exists with `GET /api/refund-proposals`

## File structure

```text
samples/Commerce.ReturnResolution/
  AgentTools/
    ReturnPolicyRules.pts    # ProtoScript eligibility and refund rules
    agentkit.json            # Tool manifest
  Data/
    orders.json              # Synthetic order records
    customers.json           # Synthetic customer records
    return-policies.json     # Return policy definitions
    customer-messages.json   # Customer return messages
  Domain/
    ReturnModels.cs          # Domain models (OrderFacts, RefundProposal, etc.)
  Repositories/
    IOrderRepository.cs      # Order data access seam
    IRefundProposalStore.cs  # Proposal store seam (includes ApproveAsync)
    JsonOrderRepository.cs   # JSON file implementation
    JsonRefundProposalStore.cs
  Scenarios/
    ReturnScenarioFactory.cs # Scripted flow
  Tools/
    ReturnFunctions.cs       # C# tools (no approve/issue tool!)
  wwwroot/
    orders.html              # Domain page
  Program.cs                 # Entry point (includes approval endpoint)
  README.md
```

## Key Agent Kit concepts demonstrated

| Concept | How this sample uses it |
| --- | --- |
| **ASP.NET Core hosting** | Agent Kit is hosted inside an ASP.NET Core web app |
| **Human approval boundary** | The agent can propose but not approve — approval is a normal endpoint, not a tool |
| **Controlled side effect** | The agent can create a proposal (a reversible action) but not issue a refund (irreversible) |
| **Mixed tool sources** | 4 C# tools + 2 ProtoScript tools |
| **Repository pattern** | Tools depend on interfaces, not concrete data sources |
| **JSONL storage** | Conversations persist as JSONL files under `.agentkit/` |
| **Inspector page** | Built-in web UI for debugging agent behavior |
| **Tool manifest as security boundary** | Only tools in the manifest are visible to the AI; approval is not in the manifest |

## Test

`tests/Buffaly.AgentKit.SampleTests/ReturnResolutionScenarioTests.cs` verifies:
- ProtoScript eligibility and refund tools are invoked
- One pending proposal is written
- The order fixture remains unchanged (the agent doesn't modify orders)
- No approve/issue/refund tool exists in the tool set
- The final answer does not claim a refund occurred

## Where to go next

- **[Samples overview](../README.md)** — See all available samples
- **[DevOps Incident Investigation](../DevOps.IncidentInvestigation/README.md)** — See a console/headless sample
- **[Medical Referral Readiness](../Medical.ReferralReadiness/README.md)** — See an ASP.NET sample with safety boundaries
- **[ASP.NET guide](../../docs/aspnet.md)** — Learn more about hosting Agent Kit in ASP.NET Core
- **[Getting started guide](../../docs/getting-started.md)** — Build your own Agent Kit app from scratch
