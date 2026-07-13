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
