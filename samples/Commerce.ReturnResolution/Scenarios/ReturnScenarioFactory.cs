using System.Text.Json;
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
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement order = ScriptedTranscript.GetToolResultJson(messages, "get_order_facts");
                return ScriptedChatResponse.Call("get_return_policy", new() { ["policy_id"] = order.GetProperty("policyId").GetString()! });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement order = ScriptedTranscript.GetToolResultJson(messages, "get_order_facts");
                return ScriptedChatResponse.Call("evaluate_return_eligibility", new()
                {
                    ["daysSinceDelivery"] = order.GetProperty("daysSinceDelivery").GetInt32(),
                    ["reason"] = order.GetProperty("reason").GetString()!,
                    ["itemCondition"] = order.GetProperty("itemCondition").GetString()!,
                    ["isFinalSale"] = order.GetProperty("isFinalSale").GetBoolean()
                });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement order = ScriptedTranscript.GetToolResultJson(messages, "get_order_facts");
                JsonElement eligibility = ScriptedTranscript.GetToolResultJson(messages, "evaluate_return_eligibility");
                return ScriptedChatResponse.Call("calculate_refund_amount", new()
                {
                    ["merchandiseAmountCents"] = Decimal.ToInt32(order.GetProperty("merchandiseAmount").GetDecimal() * 100),
                    ["shippingAmountCents"] = Decimal.ToInt32(order.GetProperty("shippingAmount").GetDecimal() * 100),
                    ["restockingPercent"] = eligibility.GetProperty("restockingPercent").GetInt32(),
                    ["refundShipping"] = eligibility.GetProperty("refundShipping").GetBoolean()
                });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement order = ScriptedTranscript.GetToolResultJson(messages, "get_order_facts");
                JsonElement eligibility = ScriptedTranscript.GetToolResultJson(messages, "evaluate_return_eligibility");
                int cents = Convert.ToInt32(ScriptedTranscript.GetToolResult(messages, "calculate_refund_amount").Result);
                string rule = eligibility.GetProperty("rule").GetString()!;
                return ScriptedChatResponse.Call("create_refund_proposal", new()
                {
                    ["order_id"] = order.GetProperty("orderId").GetString()!,
                    ["amount"] = cents / 100m,
                    ["reason"] = rule,
                    ["evidence"] = $"ProtoScript eligibility rule {rule}; calculated refund {cents} cents"
                });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement proposal = ScriptedTranscript.GetToolResultJson(messages, "create_refund_proposal");
                return ScriptedChatResponse.Final($"Prepared refund proposal {proposal.GetProperty("proposalId").GetString()} for {proposal.GetProperty("orderId").GetString()}. Status is {proposal.GetProperty("status").GetString()}; approval is still required. No refund has been approved, issued, transmitted, or settled.");
            })
        }
    };
}

