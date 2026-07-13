using Buffaly.AgentKit.SampleSupport;
using Medical.ReferralReadiness.Domain;

namespace Medical.ReferralReadiness.Repositories;

public sealed class JsonReferralRepository(string dataRoot) : IReferralRepository
{
    private readonly JsonFixtureStore _store = new(dataRoot);
    public async Task<ReferralFacts?> GetReferralAsync(string referralId, CancellationToken cancellationToken)
        => (await _store.LoadAsync<List<ReferralFacts>>("referrals.json", cancellationToken)).FirstOrDefault(r => r.ReferralId.Equals(referralId, StringComparison.OrdinalIgnoreCase));
    public async Task<ReferralRequirements?> GetRequirementsAsync(string serviceLine, CancellationToken cancellationToken)
        => (await _store.LoadAsync<List<ReferralRequirements>>("referral-requirements.json", cancellationToken)).FirstOrDefault(r => r.ServiceLine.Equals(serviceLine, StringComparison.OrdinalIgnoreCase));
    public async Task<ReferringOffice?> GetReferringOfficeAsync(string referralId, CancellationToken cancellationToken)
    {
        ReferralFacts? referral = await GetReferralAsync(referralId, cancellationToken);
        if (referral == null) return null;
        return (await _store.LoadAsync<List<ReferringOffice>>("referring-offices.json", cancellationToken)).FirstOrDefault(o => o.OfficeId.Equals(referral.ReferringOfficeId, StringComparison.OrdinalIgnoreCase));
    }
    public async Task<IReadOnlyList<ReferralFacts>> ListReferralsAsync(CancellationToken cancellationToken)
        => await _store.LoadAsync<List<ReferralFacts>>("referrals.json", cancellationToken);
}
