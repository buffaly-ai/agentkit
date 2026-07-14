# ASP.NET Core integration

`Buffaly.AgentKit.AspNetCore` hosts Agent Kit inside an ASP.NET Core application. It provides dependency injection registration, conversation stores, JSONL event output, API endpoints, and a static inspector.

## Install

```bash
dotnet add package Buffaly.AgentKit.AspNetCore --version 1.0.0
```

In this repository, sample apps use `ProjectReference` instead of package references.

## Minimal `Program.cs`

```csharp
using Buffaly.AgentKit.AspNetCore;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IChatClient>(sp => CreateConfiguredChatClient(builder.Configuration));

builder.Services.AddBuffalyAgentKit(agentKit =>
{
    agentKit.AddProtoScriptTools("AgentTools/agentkit.json");
    agentKit.UseJsonlStore(Path.Combine(builder.Environment.ContentRootPath, ".agentkit"));
});

var app = builder.Build();
app.MapBuffalyAgentKit("/_agentkit");
app.Run();
```

## Registration APIs

- `services.AddBuffalyAgentKit(...)`
- `BuffalyAgentKitBuilder.AddProtoScriptTools(string manifestFile, ProtoScriptToolSetOptions? options = null)`
- `BuffalyAgentKitBuilder.UseJsonlStore(string directory)`

`UseJsonlStore` registers `JsonlAgentConversationStore` and `FileAgentEventSink` under the selected directory.

## Endpoint mapping

```csharp
app.MapBuffalyAgentKit("/_agentkit");
```

The current implementation maps both simple routes and `/api` routes under the selected prefix.

Primary API routes:

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/_agentkit/api/tools` | List registered tools. |
| `POST` | `/_agentkit/api/conversations` | Create a conversation. Body may include `systemPrompt`. |
| `GET` | `/_agentkit/api/conversations` | List stored conversations. |
| `GET` | `/_agentkit/api/conversations/{id}` | Return exported conversation JSON. |
| `POST` | `/_agentkit/api/conversations/{id}/turns` | Run a turn. Body may include `message` or `userInput`. |
| `GET` | `/_agentkit/api/conversations/{id}/events` | Current placeholder returns an empty event array when the conversation exists. |

Additional simple routes include `/health`, `/conversations`, and `/conversations/{id}/turns`.

## Static inspector

By default, `MapBuffalyAgentKit` exposes a plain HTML inspector at:

- `/_agentkit/`
- `/_agentkit/inspector`

Disable it:

```csharp
app.MapBuffalyAgentKit("/_agentkit", options => options.EnableInspector = false);
```

The inspector is intentionally minimal: it can create a conversation and send a turn through the mapped API.

## Storage layout

The JSONL store writes under the directory passed to `UseJsonlStore`, commonly `.agentkit/`. Web samples use local `.agentkit/` directories so repeated runs can reload previous conversations.

## Authorization

Agent Kit endpoints are normal ASP.NET Core endpoints. Put them behind the host application's authentication/authorization middleware as needed:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapBuffalyAgentKit("/_agentkit").RequireAuthorization();
```

## Custom prefix

```csharp
app.MapBuffalyAgentKit("/internal/agentkit");
```

## Replace scripted client with a real provider

The samples register `IChatClient` using `SampleChatClientFactory`. Replace only that registration:

```csharp
if (builder.Configuration["AgentKit:Provider"] == "scripted")
{
    builder.Services.AddSingleton<IChatClient>(SampleChatClientFactory.Create(MyScenario.Create()));
}
else
{
    builder.Services.AddSingleton<IChatClient>(sp => CreateConfiguredChatClient(builder.Configuration));
}
```

## Repository examples

- `samples/Medical.ReferralReadiness/Program.cs`
- `samples/Commerce.ReturnResolution/Program.cs`
- `samples/AgentKit.AspNetCore/Program.cs`
