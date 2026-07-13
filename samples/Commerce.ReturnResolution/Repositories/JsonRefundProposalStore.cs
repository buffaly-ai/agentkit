using Commerce.ReturnResolution.Domain;
using System.Text.Json;

namespace Commerce.ReturnResolution.Repositories;

public sealed class JsonRefundProposalStore(string storageRoot) : IRefundProposalStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private string FilePath => Path.Combine(storageRoot, "refund-proposals.json");
    public async Task<RefundProposal> CreateAsync(CreateRefundProposalRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken); try
        {
            List<RefundProposal> all = await LoadAsync(cancellationToken);
            string id = "RP-" + (all.Count + 1).ToString("0000");
            var proposal = new RefundProposal(id, request.OrderId, request.Amount, request.Reason, request.Evidence, "pending_human_approval");
            all.Add(proposal); await SaveAsync(all, cancellationToken); return proposal;
        }
        finally { _gate.Release(); }
    }
    public async Task<RefundProposal> ApproveAsync(string proposalId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken); try
        {
            List<RefundProposal> all = await LoadAsync(cancellationToken);
            int index = all.FindIndex(p => p.ProposalId.Equals(proposalId, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new KeyNotFoundException(proposalId);
            all[index] = all[index] with { Status = "approved" }; await SaveAsync(all, cancellationToken); return all[index];
        }
        finally { _gate.Release(); }
    }
    public async Task<IReadOnlyList<RefundProposal>> ListAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);
    private async Task<List<RefundProposal>> LoadAsync(CancellationToken ct) { Directory.CreateDirectory(storageRoot); if (!File.Exists(FilePath)) return []; await using FileStream s = File.OpenRead(FilePath); return await JsonSerializer.DeserializeAsync<List<RefundProposal>>(s, _options, ct) ?? []; }
    private async Task SaveAsync(List<RefundProposal> all, CancellationToken ct) { Directory.CreateDirectory(storageRoot); await using FileStream s = File.Create(FilePath); await JsonSerializer.SerializeAsync(s, all, _options, ct); }
}
