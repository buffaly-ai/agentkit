using Buffaly.AgentKit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Net;

namespace Buffaly.AgentKit.AspNetCore;

public sealed class BuffalyAgentKitEndpointOptions
{
    public bool EnableInspector { get; set; } = true;
}

public static class BuffalyAgentKitEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapBuffalyAgentKit(this IEndpointRouteBuilder endpoints, string prefix = "/_agentkit", Action<BuffalyAgentKitEndpointOptions>? configure = null)
    {
        BuffalyAgentKitEndpointOptions options = new();
        configure?.Invoke(options);
        string p = "/" + prefix.Trim('/');
        var group = endpoints.MapGroup(p);

        group.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        group.MapGet("/conversations", async (IAgentConversationStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct)));
        group.MapPost("/conversations", async (IAgentConversationStore store, CancellationToken ct) => { AgentConversation c = AgentConversation.Create(); await store.SaveAsync(c, ct); return Results.Ok(new { id = c.Id }); });
        group.MapGet("/conversations/{id}", async (string id, IAgentConversationStore store, CancellationToken ct) => await store.LoadAsync(id, ct) is { } c ? Results.Text(c.ExportState(), "application/json") : Results.NotFound());
        group.MapPost("/conversations/{id}/turns", async (string id, TurnRequest request, IAgentConversationStore store, AgentKitRuntime runtime, CancellationToken ct) =>
        {
            AgentConversation conversation = await store.LoadAsync(id, ct) ?? AgentConversation.Create(id);
            AgentTurnResult result = await runtime.RunTurnAsync(conversation, request.GetInput(), ct);
            await store.SaveAsync(conversation, ct);
            return Results.Ok(result);
        });

        var api = group.MapGroup("/api");
        api.MapGet("/tools", (IEnumerable<Microsoft.Extensions.AI.AIFunction> tools) => Results.Ok(tools.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            schema = t.JsonSchema,
            metadata = t.AdditionalProperties
        })));
        api.MapGet("/conversations", async (IAgentConversationStore store, CancellationToken ct) => Results.Ok(await store.ListAsync(ct)));
        api.MapPost("/conversations", async (CreateConversationRequest request, IAgentConversationStore store, CancellationToken ct) => { AgentConversation c = AgentConversation.Create(); if (!string.IsNullOrWhiteSpace(request.SystemPrompt)) c.AddSystemMessage(request.SystemPrompt); await store.SaveAsync(c, ct); return Results.Ok(new { conversationId = c.Id, id = c.Id }); });
        api.MapGet("/conversations/{id}", async (string id, IAgentConversationStore store, CancellationToken ct) => await store.LoadAsync(id, ct) is { } c ? Results.Text(c.ExportState(), "application/json") : Results.NotFound());
        api.MapPost("/conversations/{id}/turns", async (string id, TurnRequest request, IAgentConversationStore store, AgentKitRuntime runtime, CancellationToken ct) =>
        {
            AgentConversation conversation = await store.LoadAsync(id, ct) ?? AgentConversation.Create(id);
            AgentTurnResult result = await runtime.RunTurnAsync(conversation, request.GetInput(), ct);
            await store.SaveAsync(conversation, ct);
            return Results.Ok(result);
        });
        api.MapGet("/conversations/{id}/events", async (string id, IAgentConversationStore store, CancellationToken ct) => await store.LoadAsync(id, ct) is null ? Results.NotFound() : Results.Ok(Array.Empty<AgentEvent>()));

        if (options.EnableInspector)
        {
            group.MapGet("/", () => Results.Content(InspectorHtml(p), "text/html"));
            group.MapGet("/inspector", () => Results.Content(InspectorHtml(p), "text/html"));
        }

        return endpoints;
    }

    private static string InspectorHtml(string prefix) => $$"""
<!doctype html><html><head><meta charset="utf-8"><title>Buffaly Agent Kit</title><style>body{font-family:system-ui;margin:2rem;max-width:900px}button,input{font:inherit}.row{margin:.5rem 0}pre{background:#f6f8fa;padding:1rem;white-space:pre-wrap}</style></head><body><h1>Buffaly Agent Kit Inspector</h1><div class="row"><button id="new">New conversation</button></div><div class="row"><input id="id" placeholder="conversation id" size="40"><input id="msg" placeholder="message" size="60"><button id="send">Send</button></div><pre id="out"></pre><script>const p='{{WebUtility.HtmlEncode(prefix)}}';const out=document.getElementById('out');document.getElementById('new').onclick=async()=>{const r=await fetch(p+'/conversations',{method:'POST'});const j=await r.json();id.value=j.id;out.textContent=JSON.stringify(j,null,2)};document.getElementById('send').onclick=async()=>{const r=await fetch(p+'/conversations/'+id.value+'/turns',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({message:msg.value})});out.textContent=await r.text()};</script></body></html>
""";

    public sealed record CreateConversationRequest(string? SystemPrompt = null);
    public sealed record TurnRequest(string? Message = null, string? UserInput = null)
    {
        public string GetInput() => UserInput ?? Message ?? string.Empty;
    }
}
