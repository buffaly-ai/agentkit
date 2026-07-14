using System.Text.Json;
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
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement facts = ScriptedTranscript.GetToolResultJson(messages, "get_referral_facts");
                return ScriptedChatResponse.Call("get_referral_requirements", new() { ["service_line"] = GetString(facts, "serviceLine") });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement facts = ScriptedTranscript.GetToolResultJson(messages, "get_referral_facts");
                return ScriptedChatResponse.Call("assess_referral_readiness", new()
                {
                    ["serviceLine"] = GetString(facts, "serviceLine"),
                    ["hasSignedOrder"] = GetBoolean(facts, "hasSignedOrder"),
                    ["hasInsuranceAuthorization"] = GetBoolean(facts, "hasInsuranceAuthorization"),
                    ["hasClinicalSummary"] = GetBoolean(facts, "hasClinicalSummary"),
                    ["hasRelevantImaging"] = GetBoolean(facts, "hasRelevantImaging")
                });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement facts = ScriptedTranscript.GetToolResultJson(messages, "get_referral_facts");
                JsonElement assessment = ScriptedTranscript.GetToolResultJson(messages, "assess_referral_readiness");
                string referralId = GetString(facts, "referralId");
                string status = assessment.GetProperty("status").GetString()!;
                string[] missing = assessment.GetProperty("missingItems").EnumerateArray().Select(item => item.GetString()!).ToArray();
                string missingText = missing.Length == 0 ? "none" : string.Join(" and ", missing);
                return ScriptedChatResponse.Final($"{referralId} administrative readiness: {status}. Missing administrative items: {missingText}. Draft neutral request: Please send {missingText} for {referralId} so the scheduling packet can be completed. This is an administrative completeness review only; it is not diagnosis, triage, treatment, or clinical advice.");
            })
        }
    };

    private static string GetString(JsonElement element, string name) => element.GetProperty(name).GetString()!;
    private static bool GetBoolean(JsonElement element, string name) => element.GetProperty(name).GetBoolean();
}

