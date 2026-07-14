using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Buffaly.AgentKit.SampleSupport;

public static class ScriptedTranscript
{
    public static FunctionResultContent GetToolResult(IReadOnlyList<ChatMessage> messages, string toolName)
    {
        var callIds = messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()
            .Where(call => call.Name == toolName).Select(call => call.CallId).ToHashSet(StringComparer.Ordinal);
        return messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>()
            .LastOrDefault(result => callIds.Contains(result.CallId))
            ?? throw new InvalidOperationException($"Transcript does not contain a result for tool '{toolName}'.");
    }

    public static JsonElement GetToolResultJson(IReadOnlyList<ChatMessage> messages, string toolName)
    {
        object? result = GetToolResult(messages, toolName).Result;
        string json = result is string text ? text : JsonSerializer.Serialize(result);
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
