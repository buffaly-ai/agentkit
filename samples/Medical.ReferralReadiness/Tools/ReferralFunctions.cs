using Medical.ReferralReadiness.Repositories;
using Microsoft.Extensions.AI;

namespace Medical.ReferralReadiness.Tools;

public static class ReferralFunctions
{
    public static IEnumerable<AIFunction> Create(IReferralRepository repository)
    {
        yield return AIFunctionFactory.Create((string referral_id, CancellationToken ct) => repository.GetReferralAsync(referral_id, ct), "get_referral_facts", "Get synthetic referral administrative facts.");
        yield return AIFunctionFactory.Create((string service_line, CancellationToken ct) => repository.GetRequirementsAsync(service_line, ct), "get_referral_requirements", "Get administrative requirements for a service line.");
        yield return AIFunctionFactory.Create((string referral_id, CancellationToken ct) => repository.GetReferringOfficeAsync(referral_id, ct), "get_referring_office", "Get synthetic referring office details.");
    }
}
