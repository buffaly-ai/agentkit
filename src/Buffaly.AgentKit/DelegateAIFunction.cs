using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;
namespace Buffaly.AgentKit;
public sealed class DelegateAIFunction : AIFunction
{
    private readonly Func<AIFunctionArguments, CancellationToken, ValueTask<object?>> _handler; private readonly JsonElement _schema;
    public DelegateAIFunction(string name, string description, Func<AIFunctionArguments, CancellationToken, ValueTask<object?>> handler) { Name = name; Description = description; _handler = handler; _schema = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"object\",\"properties\":{}}"); }
    public override string Name { get; } public override string Description { get; } public override JsonElement JsonSchema => _schema; public override MethodInfo? UnderlyingMethod => null; public override JsonSerializerOptions JsonSerializerOptions => JsonSerializerOptions.Default; public override IReadOnlyDictionary<string, object?> AdditionalProperties { get; } = new Dictionary<string, object?>();
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) => _handler(arguments, cancellationToken);
}
