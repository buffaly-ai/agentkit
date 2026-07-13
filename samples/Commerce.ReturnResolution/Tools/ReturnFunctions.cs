using Commerce.ReturnResolution.Domain;
using Commerce.ReturnResolution.Repositories;
using Microsoft.Extensions.AI;

namespace Commerce.ReturnResolution.Tools;

public static class ReturnFunctions
{
    public static IEnumerable<AIFunction> Create(IOrderRepository orders, IRefundProposalStore proposals)
    {
        yield return AIFunctionFactory.Create((string order_id, CancellationToken ct) => orders.GetOrderAsync(order_id, ct), "get_order_facts", "Get synthetic order facts.");
        yield return AIFunctionFactory.Create((string policy_id, CancellationToken ct) => orders.GetPolicyAsync(policy_id, ct), "get_return_policy", "Get a synthetic return policy.");
        yield return AIFunctionFactory.Create((string order_id, CancellationToken ct) => orders.GetCustomerMessageAsync(order_id, ct), "get_customer_message", "Get the synthetic customer message.");
        yield return AIFunctionFactory.Create((string order_id, decimal amount, string reason, string evidence, CancellationToken ct) => proposals.CreateAsync(new CreateRefundProposalRequest(order_id, amount, reason, evidence), ct), "create_refund_proposal", "Create a pending refund proposal for human approval. Does not approve or issue a refund.");
    }
}
