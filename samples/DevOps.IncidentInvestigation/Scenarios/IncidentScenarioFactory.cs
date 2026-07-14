using System.Text.Json;
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
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement service = ScriptedTranscript.GetToolResultJson(messages, "get_service_snapshot");
                string name = service.GetProperty("serviceName").GetString()!;
                return ScriptedChatResponse.Call("get_metric_window", new() { ["service_name"] = name, ["metric_name"] = "latency_and_pool", ["from_utc"] = "2026-01-15T14:00:00Z", ["to_utc"] = "2026-01-15T14:20:00Z" });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement service = ScriptedTranscript.GetToolResultJson(messages, "get_service_snapshot");
                return ScriptedChatResponse.Call("get_recent_deployments", new() { ["service_name"] = service.GetProperty("serviceName").GetString()! });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement service = ScriptedTranscript.GetToolResultJson(messages, "get_service_snapshot");
                return ScriptedChatResponse.Call("read_log_excerpt", new() { ["service_name"] = service.GetProperty("serviceName").GetString()!, ["from_utc"] = "2026-01-15T14:05:00Z", ["to_utc"] = "2026-01-15T14:20:00Z", ["maximum_lines"] = 6 });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement metrics = ScriptedTranscript.GetToolResultJson(messages, "get_metric_window");
                JsonElement latest = metrics.GetProperty("points").EnumerateArray().Last();
                JsonElement deployment = ScriptedTranscript.GetToolResultJson(messages, "get_recent_deployments");
                DateTimeOffset deployedAt = deployment.GetProperty("deployedAtUtc").GetDateTimeOffset();
                DateTimeOffset measuredAt = latest.GetProperty("utc").GetDateTimeOffset();
                return ScriptedChatResponse.Call("classify_incident_evidence", new()
                {
                    ["errorRateTenthsPercent"] = Decimal.ToInt32(latest.GetProperty("errorRatePercent").GetDecimal() * 10),
                    ["p95LatencyMs"] = latest.GetProperty("p95LatencyMs").GetInt32(),
                    ["databasePoolUtilizationPercent"] = latest.GetProperty("databasePoolUtilizationPercent").GetInt32(),
                    ["minutesSinceDeployment"] = (int)(measuredAt - deployedAt).TotalMinutes
                });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement classification = ScriptedTranscript.GetToolResultJson(messages, "classify_incident_evidence");
                string query = classification.GetProperty("classification").GetString()!.Replace('_', ' ');
                return ScriptedChatResponse.Call("search_runbooks", new() { ["query"] = query });
            }),
            ScriptedChatResponse.FromTranscript(messages =>
            {
                JsonElement service = ScriptedTranscript.GetToolResultJson(messages, "get_service_snapshot");
                JsonElement metrics = ScriptedTranscript.GetToolResultJson(messages, "get_metric_window");
                JsonElement latest = metrics.GetProperty("points").EnumerateArray().Last();
                JsonElement deployment = ScriptedTranscript.GetToolResultJson(messages, "get_recent_deployments");
                JsonElement classification = ScriptedTranscript.GetToolResultJson(messages, "classify_incident_evidence");
                JsonElement logs = ScriptedTranscript.GetToolResultJson(messages, "read_log_excerpt");
                JsonElement runbooks = ScriptedTranscript.GetToolResultJson(messages, "search_runbooks");
                string signals = string.Join(", ", classification.GetProperty("signals").EnumerateArray().Select(signal => signal.GetString()));
                string runbook = runbooks.GetProperty("matches").EnumerateArray().First().GetProperty("name").GetString()!;
                string logEvidence = string.Join("; ", logs.GetProperty("lines").EnumerateArray().Select(line => line.GetString()));
                return ScriptedChatResponse.Final($"# Incident report: {service.GetProperty("serviceName").GetString()}\n\nEvidence indicates {classification.GetProperty("classification").GetString()} at {classification.GetProperty("severity").GetString()} severity with signals {signals}; this correlation is not a proven root cause. Metrics: p95 {latest.GetProperty("p95LatencyMs").GetInt32()} ms, database pool {latest.GetProperty("databasePoolUtilizationPercent").GetInt32()}%. Log evidence: {logEvidence}. Recent deployment: {deployment.GetProperty("version").GetString()}. Recommended runbook for operator review: {runbook}. Do not perform remediation from this sample.");
            })
        }
    };
}

