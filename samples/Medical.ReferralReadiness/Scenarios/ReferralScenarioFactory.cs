using Buffaly.AgentKit.SampleSupport;

namespace Medical.ReferralReadiness.Scenarios;

public static class ReferralScenarioFactory
{
    public static ScenarioDefinition Create() => new()
    {
        ScenarioId = "referral-ref-1003",
        Responses = new[]
        {
            ScriptedChatResponse.Call("get_referral_facts", new() { ["referral_id"] = "REF-1003" }),
            ScriptedChatResponse.Call("get_referral_requirements", new() { ["service_line"] = "Orthopedic consultation" }, expectedLastToolName: "get_referral_facts", expectedLastToolResultContains: "REF-1003"),
            ScriptedChatResponse.Call("assess_referral_readiness", new() { ["serviceLine"] = "Orthopedic consultation", ["hasSignedOrder"] = false, ["hasInsuranceAuthorization"] = true, ["hasClinicalSummary"] = true, ["hasRelevantImaging"] = false }, expectedLastToolName: "get_referral_requirements", expectedLastToolResultContains: "Orthopedic consultation"),
            ScriptedChatResponse.Final("REF-1003 is not administratively ready for scheduling. Missing administrative items: signed referral order and relevant imaging report. Draft neutral request: Please send the signed referral order and relevant imaging report for REF-1003 so the scheduling packet can be completed. This is an administrative completeness review only; it is not diagnosis, triage, treatment, or clinical advice.", expectedLastToolName: "assess_referral_readiness", expectedLastToolResultContains: "signed referral order")
        }
    };
}
