using Buffaly.AgentKit.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Json;
using Xunit;

namespace Buffaly.AgentKit.Tests;

public class AspNetCoreTests
{
    [Fact]
    public async Task MapsCustomPrefix()
    {
        using TestServer server = CreateServer("/kit", true);
        HttpResponseMessage response = await server.CreateClient().GetAsync("/kit/health");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task MapsDefaultSpecPrefixAndApiTools()
    {
        using TestServer server = CreateServer("/_agentkit", true);
        HttpResponseMessage response = await server.CreateClient().GetAsync("/_agentkit/api/tools");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task ApiTurnAcceptsUserInputSchemaAndEventsEndpointExists()
    {
        using TestServer server = CreateServer("/_agentkit", true);
        HttpClient client = server.CreateClient();
        System.Text.Json.Nodes.JsonObject created = (await (await client.PostAsJsonAsync("/_agentkit/api/conversations", new { systemPrompt = "system" })).Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>())!;
        string id = created["conversationId"]!.ToString();
        HttpResponseMessage turn = await client.PostAsJsonAsync($"/_agentkit/api/conversations/{id}/turns", new { userInput = "hello" });
        Assert.True(turn.IsSuccessStatusCode);
        HttpResponseMessage eventsResponse = await client.GetAsync($"/_agentkit/api/conversations/{id}/events");
        Assert.True(eventsResponse.IsSuccessStatusCode);
        AgentEvent[] persistedEvents = (await eventsResponse.Content.ReadFromJsonAsync<AgentEvent[]>())!;
        Assert.Contains(persistedEvents, item => item.Kind == AgentEventKind.UserMessageAdded && item.Data["text"]!.GetValue<string>() == "hello");
        Assert.Contains(persistedEvents, item => item.Kind == AgentEventKind.AssistantMessageAdded && item.Data["text"]!.GetValue<string>() == "ok");
        Assert.Contains(persistedEvents, item => item.Kind == AgentEventKind.TurnCompleted);
    }

    [Fact]
    public async Task NoEndpointsExistUnlessMapped()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();
        app.Start();
        using TestServer server = app.GetTestServer();
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await server.CreateClient().GetAsync("/_agentkit/api/tools")).StatusCode);
    }

    [Fact]
    public async Task InspectorCanBeDisabled()
    {
        using TestServer server = CreateServer("/kit", false);
        HttpResponseMessage response = await server.CreateClient().GetAsync("/kit/inspector");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task JsonlStoreReloadsLatestConversation()
    {
        string dir = Path.Combine(Path.GetTempPath(), "agentkit-jsonl-" + Guid.NewGuid().ToString("n"));
        var store = new JsonlAgentConversationStore(dir);
        AgentConversation c = AgentConversation.Create("abc");
        await store.SaveAsync(c);
        AgentConversation? loaded = await new JsonlAgentConversationStore(dir).LoadAsync("abc");
        Assert.NotNull(loaded);
        Assert.Equal("abc", loaded.Id);
    }

    [Fact]
    public async Task PersistedTurnTraceCanBeReadAfterStoreRecreation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "agentkit-event-restart-" + Guid.NewGuid().ToString("n"));
        var conversationStore = new JsonlAgentConversationStore(directory);
        var eventStore = new FileAgentEventSink(directory);
        var first = new ChatResponse(new ChatMessage(ChatRole.Assistant, [
            new FunctionCallContent("restart-call-1", "echo", new Dictionary<string, object?> { ["value"] = "observed" })]));
        var second = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Observed: observed"));
        var tool = new DelegateAIFunction("echo", "Echo value", (arguments, ct) => ValueTask.FromResult<object?>(arguments["value"]));
        var conversation = AgentConversation.Create("restart-proof");

        await new AgentKitRuntime(new ScriptedChatClient(first, second), [tool], eventSink: eventStore)
            .RunTurnAsync(conversation, "Trace this turn");
        await conversationStore.SaveAsync(conversation);

        var recreatedEvents = new FileAgentEventSink(directory);
        IReadOnlyList<AgentEvent> events = await recreatedEvents.ReadAsync(conversation.Id);

        Assert.Contains(events, item => item.Kind == AgentEventKind.UserMessageAdded && item.Data["text"]!.GetValue<string>() == "Trace this turn");
        Assert.Contains(events, item => item.Kind == AgentEventKind.AssistantFunctionCallAdded && item.Data["callId"]!.GetValue<string>() == "restart-call-1" && item.Data["arguments"]!["value"]!.GetValue<string>() == "observed");
        Assert.Contains(events, item => item.Kind == AgentEventKind.ToolCallCompleted && item.Data["result"]!.GetValue<string>() == "observed");
        Assert.Contains(events, item => item.Kind == AgentEventKind.AssistantMessageAdded && item.Data["text"]!.GetValue<string>() == "Observed: observed");
        Assert.Equal(events.Select(item => item.Sequence).Order(), events.Select(item => item.Sequence));
        Assert.True(File.Exists(Path.Combine(directory, conversation.Id, "conversation.json")));
        Assert.True(File.Exists(Path.Combine(directory, conversation.Id, "events.jsonl")));
        Assert.True(File.Exists(Path.Combine(directory, "conversations.jsonl")));
    }

    private static TestServer CreateServer(string prefix, bool inspector)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IChatClient>(new ScriptedChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))));
        builder.Services.AddBuffalyAgentKit();
        WebApplication app = builder.Build();
        app.MapBuffalyAgentKit(prefix, o => o.EnableInspector = inspector);
        app.Start();
        return app.GetTestServer();
    }
}
