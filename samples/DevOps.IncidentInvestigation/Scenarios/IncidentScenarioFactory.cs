using Buffaly.AgentKit.SampleSupport;

namespace DevOps.IncidentInvestigation.Scenarios;

public static class IncidentScenarioFactory
{
    public static ScenarioDefinition Create() => new()
    {
        ScenarioId = "incident-checkout-api",
        Responses = new[]
        {
            ScriptedChatResponse.Call("get_service_snapshot", new() { ["service_name"] = "checkout-api" }),
            ScriptedChatResponse.Call("get_metric_window", new() { ["service_name"] = "checkout-api", ["metric_name"] = "latency_and_pool", ["from_utc"] = "2026-01-15T14:00:00Z", ["to_utc"] = "2026-01-15T14:20:00Z" }, expectedLastToolName: "get_service_snapshot", expectedLastToolResultContains: "checkout-api"),
            ScriptedChatResponse.Call("get_recent_deployments", new() { ["service_name"] = "checkout-api" }, expectedLastToolName: "get_metric_window", expectedLastToolResultContains: "databasePoolUtilizationPercent"),
            ScriptedChatResponse.Call("read_log_excerpt", new() { ["service_name"] = "checkout-api", ["from_utc"] = "2026-01-15T14:05:00Z", ["to_utc"] = "2026-01-15T14:20:00Z", ["maximum_lines"] = 6 }, expectedLastToolName: "get_recent_deployments", expectedLastToolResultContains: "2026.01.15.1400"),
            ScriptedChatResponse.Call("classify_incident_evidence", new() { ["errorRateTenthsPercent"] = 48, ["p95LatencyMs"] = 1850, ["databasePoolUtilizationPercent"] = 94, ["minutesSinceDeployment"] = 7 }, expectedLastToolName: "read_log_excerpt", expectedLastToolResultContains: "timeout"),
            ScriptedChatResponse.Call("search_runbooks", new() { ["query"] = "database pool saturation" }, expectedLastToolName: "classify_incident_evidence", expectedLastToolResultContains: "database_pool_saturation"),
            ScriptedChatResponse.Final("# Incident report: checkout-api latency increase\n\nThe most plausible explanation supported by the synthetic fixtures is database connection-pool saturation correlated with a recent checkout-api deployment; this is not a proven root cause. Evidence: metrics show p95 latency rising to 1850 ms while databasePoolUtilizationPercent reached 94; logs show checkout-api timeout messages after 14:05; deployment history shows checkout-api build 2026.01.15.1400 shortly before the incident; payment-api is a control with no matching degradation. The ProtoScript classifier marked severity high with database_pool_saturation and recent_deployment_correlation signals. Recommended runbook: database-pool-saturation.md. Do not perform remediation from this sample; have an operator review the evidence and choose any action.", expectedLastToolName: "search_runbooks", expectedLastToolResultContains: "database-pool-saturation")
        }
    };
}
