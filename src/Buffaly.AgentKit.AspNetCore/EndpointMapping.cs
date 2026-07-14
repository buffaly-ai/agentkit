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
        api.MapGet("/tools", (IServiceProvider services) =>
        {
            ProtoScriptToolCatalog? catalog = services.GetService<ProtoScriptToolCatalog>();
            if (catalog is not null) return Results.Ok(catalog.Snapshot);
            var tools = services.GetService<IReadOnlyList<Microsoft.Extensions.AI.AIFunction>>() ?? Array.Empty<Microsoft.Extensions.AI.AIFunction>();
            return Results.Ok(new AgentToolCatalogSnapshot
            {
                Status = "Loaded",
                LoadedAt = DateTimeOffset.UtcNow,
                Tools = tools.Select(tool => new AgentToolDescriptor { Name = tool.Name, Description = tool.Description, Schema = tool.JsonSchema, Metadata = tool.AdditionalProperties }).ToArray()
            });
        });
        api.MapPost("/tools/reload", async (IServiceProvider services, CancellationToken ct) =>
            services.GetService<ProtoScriptToolCatalog>() is { } catalog
                ? Results.Ok(await catalog.ReloadAsync(ct))
                : Results.Conflict(new AgentToolCatalogSnapshot { Status = "NotLoaded", Errors = new[] { "No ProtoScript tool catalog is configured." } }));
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
        api.MapGet("/conversations/{id}/events", async (string id, IAgentConversationStore conversations, IAgentEventStore events, CancellationToken ct) =>
            await conversations.LoadAsync(id, ct) is null ? Results.NotFound() : Results.Ok(await events.ReadAsync(id, ct)));

        if (options.EnableInspector)
        {
            group.MapGet("/", () => Results.Content(InspectorHtml(p), "text/html"));
            group.MapGet("/inspector", () => Results.Content(InspectorHtml(p), "text/html"));
        }

        return endpoints;
    }

    private static string InspectorHtml(string prefix) => $$"""
<!doctype html><html><head><meta charset="utf-8"><title>Buffaly Agent Kit Inspector</title><style>body{font-family:system-ui;margin:2rem;max-width:1100px}button,input{font:inherit}.row{margin:.5rem 0}.event{border-left:4px solid #567;padding:.5rem 1rem;margin:.5rem 0;background:#f6f8fa}.kind{font-weight:700}pre{white-space:pre-wrap}</style></head><body><h1>Agent Kit diagnostic workbench</h1><div class="row"><button id="new">New conversation</button> <button id="tools">Loaded tools</button></div><div class="row"><input id="id" placeholder="conversation id" size="40"><input id="msg" placeholder="message" size="60"><button id="send">Send</button><button id="replay">Replay trace</button></div><div id="out"></div><script>const p='{{WebUtility.HtmlEncode(prefix)}}',out=document.getElementById('out');const show=o=>out.innerHTML='<pre>'+escapeHtml(JSON.stringify(o,null,2))+'</pre>';const escapeHtml=s=>s.replace(/[&<>]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;'}[c]));async function replay(){const e=await(await fetch(p+'/api/conversations/'+id.value+'/events')).json();out.innerHTML=e.map(x=>`<div class="event"><div class="kind">${x.sequence} ${x.kind}</div><pre>${escapeHtml(JSON.stringify(x.data,null,2))}</pre></div>`).join('')}new.onclick=async()=>{const j=await(await fetch(p+'/api/conversations',{method:'POST',headers:{'content-type':'application/json'},body:'{}'})).json();id.value=j.conversationId;show(j)};send.onclick=async()=>{await fetch(p+'/api/conversations/'+id.value+'/turns',{method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({userInput:msg.value})});await replay()};tools.onclick=async()=>show(await(await fetch(p+'/api/tools')).json());document.getElementById('replay').onclick=replay;</script></body></html>
""";

    public sealed record CreateConversationRequest(string? SystemPrompt = null);
    public sealed record TurnRequest(string? Message = null, string? UserInput = null)
    {
        public string GetInput() => UserInput ?? Message ?? string.Empty;
    }
}
