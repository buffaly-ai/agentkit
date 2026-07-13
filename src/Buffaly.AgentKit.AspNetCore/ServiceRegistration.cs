using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Buffaly.AgentKit.AspNetCore;

public sealed class BuffalyAgentKitBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;

    public BuffalyAgentKitBuilder AddProtoScriptTools(string manifestFile, ProtoScriptToolSetOptions? options = null)
    {
        Services.AddSingleton<IReadOnlyList<AIFunction>>(sp => ProtoScriptToolSet.LoadAsync(manifestFile, options).GetAwaiter().GetResult().Tools);
        return this;
    }

    public BuffalyAgentKitBuilder UseJsonlStore(string directory)
    {
        Services.AddSingleton<IAgentConversationStore>(new JsonlAgentConversationStore(directory));
        Services.AddSingleton<IAgentEventSink>(new FileAgentEventSink(Path.Combine(directory, "events.jsonl")));
        return this;
    }
}

public static class BuffalyAgentKitServiceCollectionExtensions
{
    public static BuffalyAgentKitBuilder AddBuffalyAgentKit(this IServiceCollection services, Action<BuffalyAgentKitBuilder>? configure = null)
    {
        services.AddSingleton<IAgentConversationStore, InMemoryAgentConversationStore>();
        services.AddSingleton<IAgentEventSink, NullAgentEventSink>();
        services.AddSingleton(sp => new AgentKitRuntime(
            sp.GetRequiredService<IChatClient>(),
            sp.GetService<IReadOnlyList<AIFunction>>() ?? Array.Empty<AIFunction>(),
            eventSink: sp.GetRequiredService<IAgentEventSink>()));
        var builder = new BuffalyAgentKitBuilder(services);
        configure?.Invoke(builder);
        return builder;
    }
}
