using Commerce.ReturnResolution.Domain;
namespace Commerce.ReturnResolution.Repositories;
public interface IOrderRepository
{
    Task<OrderFacts?> GetOrderAsync(string orderId, CancellationToken cancellationToken);
    Task<ReturnPolicy?> GetPolicyAsync(string policyId, CancellationToken cancellationToken);
    Task<CustomerMessage?> GetCustomerMessageAsync(string orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<OrderFacts>> ListOrdersAsync(CancellationToken cancellationToken);
}
