using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Buffaly.AgentKit.ProtoScript;

public sealed class ProtoScriptAIFunction : AIFunction
{
    private readonly Func<AIFunctionArguments, CancellationToken, ValueTask<object?>> _invoker;
    private readonly JsonElement _schema;

    internal ProtoScriptAIFunction(AgentKitManifestExport export, Func<AIFunctionArguments, CancellationToken, ValueTask<object?>> invoker)
    {
        Export = export;
        _invoker = invoker;
        _schema = ToolSchemaMapper.CreateSchema(export);
    }

    public AgentKitManifestExport Export { get; }
    public override string Name => Export.Name;
    public override string Description => Export.Description;
    public override JsonElement JsonSchema => _schema;
    public override MethodInfo? UnderlyingMethod => null;
    public override JsonSerializerOptions JsonSerializerOptions => JsonSerializerOptions.Default;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties { get; } = new Dictionary<string, object?>();
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) => _invoker(arguments, cancellationToken);
}
