namespace Buffaly.AgentKit.ProtoScript;

public sealed class ProtoScriptToolSetOptions
{
    public bool ExecuteStartupStatements { get; init; } = true;
    public TimeSpan InvocationTimeout { get; init; } = TimeSpan.FromMinutes(2);
}
