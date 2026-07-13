namespace Medical.ReferralReadiness.Domain;
public sealed record ReferralFacts(string ReferralId, string ServiceLine, bool HasSignedOrder, bool HasInsuranceAuthorization, bool HasClinicalSummary, bool HasRelevantImaging, string ReferringOfficeId, bool SyntheticData);
public sealed record ReferralRequirements(string ServiceLine, bool RequiresSignedOrder, bool RequiresInsuranceAuthorization, bool RequiresClinicalSummary, bool RequiresRelevantImaging, bool SyntheticData);
public sealed record ReferringOffice(string OfficeId, string DisplayName, string ContactMethod, bool HasProviderContact, bool SyntheticData);
public sealed record PatientSummary(string PatientId, string DisplayName, bool SyntheticData);
