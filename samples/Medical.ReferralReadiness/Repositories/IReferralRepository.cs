using Medical.ReferralReadiness.Domain;
namespace Medical.ReferralReadiness.Repositories;
public interface IReferralRepository
{
    Task<ReferralFacts?> GetReferralAsync(string referralId, CancellationToken cancellationToken);
    Task<ReferralRequirements?> GetRequirementsAsync(string serviceLine, CancellationToken cancellationToken);
    Task<ReferringOffice?> GetReferringOfficeAsync(string referralId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReferralFacts>> ListReferralsAsync(CancellationToken cancellationToken);
}
