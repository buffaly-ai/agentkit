using Commerce.ReturnResolution.Domain;
namespace Commerce.ReturnResolution.Repositories;
public interface IRefundProposalStore
{
    Task<RefundProposal> CreateAsync(CreateRefundProposalRequest request, CancellationToken cancellationToken);
    Task<RefundProposal> ApproveAsync(string proposalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RefundProposal>> ListAsync(CancellationToken cancellationToken);
}
