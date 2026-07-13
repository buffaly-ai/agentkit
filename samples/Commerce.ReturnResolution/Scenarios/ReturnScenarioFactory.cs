using Buffaly.AgentKit.SampleSupport;

namespace Commerce.ReturnResolution.Scenarios;

public static class ReturnScenarioFactory
{
    public static ScenarioDefinition Create() => new()
    {
        ScenarioId = "return-ord-1042",
        Responses = new[]
        {
            ScriptedChatResponse.Call("get_order_facts", new() { ["order_id"] = "ORD-1042" }),
            ScriptedChatResponse.Call("get_return_policy", new() { ["policy_id"] = "STANDARD-30" }, expectedLastToolName: "get_order_facts", expectedLastToolResultContains: "ORD-1042"),
            ScriptedChatResponse.Call("evaluate_return_eligibility", new() { ["daysSinceDelivery"] = 6, ["reason"] = "damaged", ["itemCondition"] = "damaged", ["isFinalSale"] = false }, expectedLastToolName: "get_return_policy", expectedLastToolResultContains: "STANDARD-30"),
            ScriptedChatResponse.Call("calculate_refund_amount", new() { ["merchandiseAmountCents"] = 8495, ["shippingAmountCents"] = 795, ["restockingPercent"] = 0, ["refundShipping"] = false }, expectedLastToolName: "evaluate_return_eligibility", expectedLastToolResultContains: "damaged_item_within_window"),
            ScriptedChatResponse.Call("create_refund_proposal", new() { ["order_id"] = "ORD-1042", ["amount"] = 84.95m, ["reason"] = "Damaged item within standard return window", ["evidence"] = "Eligibility rule damaged_item_within_window; calculated merchandise refund 84.95" }, expectedLastToolName: "calculate_refund_amount", expectedLastToolResultContains: "8495"),
            ScriptedChatResponse.Final("Prepared refund proposal for ORD-1042. Proposal ID will be shown in the application record and remains pending_human_approval. No refund has been approved, issued, transmitted, or settled; a human must approve the proposal through the normal application endpoint.", expectedLastToolName: "create_refund_proposal", expectedLastToolResultContains: "pending_human_approval")
        }
    };
}
