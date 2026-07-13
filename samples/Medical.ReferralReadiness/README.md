# Medical Referral Readiness sample

ASP.NET sample showing Agent Kit embedded into a read-only administrative workflow. It uses synthetic JSON repositories, C# read-only functions, a ProtoScript readiness rule, JSONL conversation persistence under `.agentkit/`, and the Agent Kit inspector at `/_agentkit`.

Replace `JsonReferralRepository` with a real repository to adapt the sample; keep the Agent Kit runtime and ProtoScript rule boundary unchanged. See `SAFETY.md` before any production adaptation.
