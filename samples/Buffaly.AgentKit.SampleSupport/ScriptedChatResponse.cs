namespace Buffaly.AgentKit.SampleSupport;

public sealed class ScriptedChatResponse
{
    public string? ExpectedLastToolName { get; init; }
    public string? ExpectedLastToolResultContains { get; init; }
    public string? FunctionName { get; init; }
    public Dictionary<string, object?> Arguments { get; init; } = new(StringComparer.Ordinal);
    public string? FinalText { get; init; }

    public static ScriptedChatResponse Call(string functionName, Dictionary<string, object?>? arguments = null, string? expectedLastToolName = null, string? expectedLastToolResultContains = null) => new()
    {
        FunctionName = functionName,
        Arguments = arguments ?? new Dictionary<string, object?>(StringComparer.Ordinal),
        ExpectedLastToolName = expectedLastToolName,
        ExpectedLastToolResultContains = expectedLastToolResultContains
    };

    public static ScriptedChatResponse Final(string text, string? expectedLastToolName = null, string? expectedLastToolResultContains = null) => new()
    {
        FinalText = text,
        ExpectedLastToolName = expectedLastToolName,
        ExpectedLastToolResultContains = expectedLastToolResultContains
    };
}
