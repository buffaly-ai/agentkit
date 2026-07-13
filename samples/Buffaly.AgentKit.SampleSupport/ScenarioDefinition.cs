using System.Collections.ObjectModel;

namespace Buffaly.AgentKit.SampleSupport;

public sealed class ScenarioDefinition
{
    public string ScenarioId { get; init; } = string.Empty;
    public IReadOnlyList<ScriptedChatResponse> Responses { get; init; } = Array.Empty<ScriptedChatResponse>();
}
