namespace Buffaly.AgentKit.ProtoScript;

public sealed class ProtoScriptToolSetOptions
{
    public Dictionary<string, object?> Globals { get; init; } = new(StringComparer.Ordinal);
    public bool ExecuteStartupStatements { get; init; } = true;
    public TimeSpan InvocationTimeout { get; init; } = TimeSpan.FromMinutes(2);
}
