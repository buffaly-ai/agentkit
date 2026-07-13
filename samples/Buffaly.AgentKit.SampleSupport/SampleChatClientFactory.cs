using Microsoft.Extensions.AI;

namespace Buffaly.AgentKit.SampleSupport;

public static class SampleChatClientFactory
{
    public static IChatClient Create(ScenarioDefinition scenario) => new ScriptedChatClient(scenario);
}
