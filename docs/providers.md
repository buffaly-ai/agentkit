# Provider integration

Agent Kit uses `Microsoft.Extensions.AI.IChatClient` as the provider boundary. The runtime does not know which model provider is behind the client.

## What Agent Kit expects

An `IChatClient` must:

- accept the current `IEnumerable<ChatMessage>`,
- receive tools through `ChatOptions.Tools`,
- return assistant text for final answers,
- return `FunctionCallContent` when it wants a tool invoked.

Agent Kit will invoke the tool and add a `FunctionResultContent` message before the next model round.

## OpenAI example

```csharp
using Microsoft.Extensions.AI;
using OpenAI;

IChatClient chat = new OpenAIClient(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
    .AsChatClient("gpt-4.1-mini");
```

Exact packages and method names may differ by provider adapter version. Keep the result typed as `IChatClient`.

## Azure OpenAI example

```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

var azure = new AzureOpenAIClient(
    new Uri(builder.Configuration["AzureOpenAI:Endpoint"]!),
    new Azure.AzureKeyCredential(builder.Configuration["AzureOpenAI:Key"]!));

IChatClient chat = azure.AsChatClient(builder.Configuration["AzureOpenAI:Deployment"]!);
```

## Ollama example

Many Ollama integrations expose an `IChatClient` adapter. The shape is typically:

```csharp
using Microsoft.Extensions.AI;

IChatClient chat = new OllamaChatClient(
    new Uri("http://localhost:11434"),
    "llama3.1");
```

Use a model/provider that supports tool/function calling if you want model-driven tool use.

## Custom `IChatClient`

A custom client can relay to an internal service, enforce policy, or provide deterministic tests. Implement `GetResponseAsync`, return `ChatResponse`, and use `FunctionCallContent` for tool calls.

The repository's `samples/Buffaly.AgentKit.SampleSupport/ScriptedChatClient.cs` is the most complete custom example. It validates the observed conversation and then emits the next scripted provider response.

## Scripted provider behavior

The scripted provider is not a fake tool executor. It emits real provider-facing function calls. Agent Kit still performs normal tool dispatch, result appending, event emission, and final response handling.

## Provider-specific notes

Providers differ in how strictly they follow JSON schema, whether they support multiple tool calls per message, and how they represent tool-call IDs. Agent Kit normalizes only the `Microsoft.Extensions.AI` abstraction layer. If a provider has weak schema compliance, use `IAgentToolPolicy` and strict tool implementations to validate arguments before side effects.
