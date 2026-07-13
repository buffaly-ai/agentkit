using Buffaly.AgentKit.SampleSupport;
using Commerce.ReturnResolution.Domain;

namespace Commerce.ReturnResolution.Repositories;

public sealed class JsonOrderRepository(string dataRoot) : IOrderRepository
{
    private readonly JsonFixtureStore _store = new(dataRoot);
    public async Task<OrderFacts?> GetOrderAsync(string orderId, CancellationToken cancellationToken) => (await _store.LoadAsync<List<OrderFacts>>("orders.json", cancellationToken)).FirstOrDefault(o => o.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase));
    public async Task<ReturnPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken) => (await _store.LoadAsync<List<ReturnPolicy>>("return-policies.json", cancellationToken)).FirstOrDefault(p => p.PolicyId.Equals(policyId, StringComparison.OrdinalIgnoreCase));
    public async Task<CustomerMessage?> GetCustomerMessageAsync(string orderId, CancellationToken cancellationToken) => (await _store.LoadAsync<List<CustomerMessage>>("customer-messages.json", cancellationToken)).FirstOrDefault(m => m.OrderId.Equals(orderId, StringComparison.OrdinalIgnoreCase));
    public async Task<IReadOnlyList<OrderFacts>> ListOrdersAsync(CancellationToken cancellationToken) => await _store.LoadAsync<List<OrderFacts>>("orders.json", cancellationToken);
}
