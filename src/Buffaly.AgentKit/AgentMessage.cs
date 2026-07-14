using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Buffaly.AgentKit;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AgentTextContent), "text")]
[JsonDerivedType(typeof(AgentFunctionCallContent), "functionCall")]
[JsonDerivedType(typeof(AgentFunctionResultContent), "functionResult")]
public abstract record AgentMessageContent;

public sealed record AgentTextContent(string Text) : AgentMessageContent;

public sealed record AgentFunctionCallContent(
    string CallId,
    string Name,
    JsonObject Arguments) : AgentMessageContent;

public sealed record AgentFunctionResultContent(
    string CallId,
    string Name,
    string Result,
    bool IsError) : AgentMessageContent;

public sealed record AgentMessage
{
    [JsonConstructor]
    public AgentMessage(AgentMessageRole role, IReadOnlyList<AgentMessageContent> contents)
    {
        Role = role;
        Contents = contents;
    }

    public AgentMessage(AgentMessageRole role, string text)
        : this(role, new AgentMessageContent[] { new AgentTextContent(text) }) { }

    public AgentMessageRole Role { get; init; }

    public IReadOnlyList<AgentMessageContent> Contents { get; init; }

    [JsonIgnore]
    public string Content => Text;

    [JsonIgnore]
    public string Text => string.Concat(Contents.OfType<AgentTextContent>().Select(content => content.Text)) is { Length: > 0 } text
        ? text
        : Contents.OfType<AgentFunctionResultContent>().FirstOrDefault()?.Result ?? string.Empty;

    [JsonIgnore]
    public string? ToolCallId => Contents.OfType<AgentFunctionCallContent>().FirstOrDefault()?.CallId
        ?? Contents.OfType<AgentFunctionResultContent>().FirstOrDefault()?.CallId;

    [JsonIgnore]
    public string? ToolName => Contents.OfType<AgentFunctionCallContent>().FirstOrDefault()?.Name
        ?? Contents.OfType<AgentFunctionResultContent>().FirstOrDefault()?.Name;
}

