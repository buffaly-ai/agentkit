namespace Commerce.ReturnResolution.Domain;
public sealed record OrderFacts(string OrderId, string CustomerId, string PolicyId, int DaysSinceDelivery, string Reason, string ItemCondition, bool IsFinalSale, decimal MerchandiseAmount, decimal ShippingAmount, bool SyntheticData);
public sealed record ReturnPolicy(string PolicyId, int ReturnWindowDays, decimal RestockingPercent, bool RefundShippingForDamage, bool SyntheticData);
public sealed record CustomerMessage(string OrderId, string Message, bool SyntheticData);
public sealed record RefundProposal(string ProposalId, string OrderId, decimal Amount, string Reason, string Evidence, string Status);
public sealed record CreateRefundProposalRequest(string OrderId, decimal Amount, string Reason, string Evidence);
