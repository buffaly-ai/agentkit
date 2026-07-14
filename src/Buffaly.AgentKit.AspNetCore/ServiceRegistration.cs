using Buffaly.AgentKit;
using Buffaly.AgentKit.ProtoScript;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Buffaly.AgentKit.AspNetCore;

public sealed class BuffalyAgentKitBuilder(IServiceCollection services)
{
    public IServiceCollection Services { get; } = services;
    public BuffalyAgentKitBuilder AddProtoScriptTools(string manifestFile, ProtoScriptToolSetOptions? options = null) { Services.AddSingleton<IReadOnlyList<AIFunction>>(_ => ProtoScriptToolSet.LoadAsync(manifestFile, options).GetAwaiter().GetResult().Tools); return this; }
    public BuffalyAgentKitBuilder UseJsonlStore(string directory)
    {
        Services.AddSingleton<IAgentConversationStore>(new JsonlAgentConversationStore(directory));
        var events = new FileAgentEventSink(directory);
        Services.AddSingleton<IAgentEventStore>(events);
        Services.AddSingleton<IAgentEventSink>(events);
        return this;
    }
}

public static class BuffalyAgentKitServiceCollectionExtensions
{
    public static BuffalyAgentKitBuilder AddBuffalyAgentKit(this IServiceCollection services, Action<BuffalyAgentKitBuilder>? configure = null)
    {
        services.AddSingleton<IAgentConversationStore, InMemoryAgentConversationStore>();
        var events = new InMemoryAgentEventStore();
        services.AddSingleton<IAgentEventStore>(events);
        services.AddSingleton<IAgentEventSink>(events);
        services.AddSingleton(sp => new AgentKitRuntime(sp.GetRequiredService<IChatClient>(), sp.GetService<IReadOnlyList<AIFunction>>() ?? Array.Empty<AIFunction>(), eventSink: sp.GetRequiredService<IAgentEventSink>()));
        var builder = new BuffalyAgentKitBuilder(services); configure?.Invoke(builder); return builder;
    }
}

