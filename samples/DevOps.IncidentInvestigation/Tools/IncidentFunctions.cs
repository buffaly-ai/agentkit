using System.Text.Json;
using Microsoft.Extensions.AI;

namespace DevOps.IncidentInvestigation.Tools;

public static class IncidentFunctions
{
    public static IEnumerable<AIFunction> Create(string dataRoot)
    {
        string root = Path.GetFullPath(dataRoot);
        yield return AIFunctionFactory.Create((string service_name) => ReadJson(root, "services.json", service_name), "get_service_snapshot", "Get a synthetic service snapshot.");
        yield return AIFunctionFactory.Create((string service_name, string metric_name, string from_utc, string to_utc) => ReadJson(root, Path.Combine("metrics", service_name + ".json"), service_name), "get_metric_window", "Get a synthetic metric window.");
        yield return AIFunctionFactory.Create((string service_name, string from_utc, string to_utc, int maximum_lines) => ReadLog(root, service_name, maximum_lines), "read_log_excerpt", "Read a bounded synthetic log excerpt.");
        yield return AIFunctionFactory.Create((string service_name) => ReadJson(root, "deployments.json", service_name), "get_recent_deployments", "Get recent synthetic deployments.");
        yield return AIFunctionFactory.Create((string query) => SearchRunbooks(root, query), "search_runbooks", "Search local synthetic runbooks.");
    }
    private static JsonElement ReadJson(string root, string relativePath, string? contains = null)
    {
        string path = BoundedPath(root, relativePath);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.ValueKind == JsonValueKind.Array && contains != null)
            foreach (JsonElement item in doc.RootElement.EnumerateArray()) if (item.ToString().Contains(contains, StringComparison.OrdinalIgnoreCase)) return item.Clone();
        return doc.RootElement.Clone();
    }
    private static object ReadLog(string root, string serviceName, int maximumLines)
    {
        string path = BoundedPath(root, Path.Combine("logs", serviceName + ".log"));
        return new { serviceName, lines = File.ReadLines(path).Take(Math.Clamp(maximumLines, 1, 50)).ToArray() };
    }
    private static object SearchRunbooks(string root, string query)
    {
        string runbookRoot = BoundedPath(root, "runbooks");
        var matches = Directory.EnumerateFiles(runbookRoot, "*.md").Select(path => new { name = Path.GetFileName(path), text = File.ReadAllText(path) }).Where(r => r.name.Contains("database-pool", StringComparison.OrdinalIgnoreCase) || r.text.Contains(query, StringComparison.OrdinalIgnoreCase)).Select(r => new { r.name, excerpt = r.text.Split('\n').FirstOrDefault(l => l.Contains("pool", StringComparison.OrdinalIgnoreCase)) ?? r.text[..Math.Min(120, r.text.Length)] }).ToArray();
        return new { query, matches };
    }
    private static string BoundedPath(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath)) throw new InvalidOperationException("Path must be relative.");
        string full = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Attempted to access a file outside Data.");
        return full;
    }
}
