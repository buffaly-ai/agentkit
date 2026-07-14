using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Buffaly.AgentKit.ProtoScript;

public sealed class ProtoScriptAIFunction : AIFunction
{
    private readonly Func<AIFunctionArguments, CancellationToken, ValueTask<object?>> _invoker;
    private readonly JsonElement _schema;
    private readonly IReadOnlyDictionary<string, object?> _additionalProperties;

    internal ProtoScriptAIFunction(
        AgentKitManifestExport export,
        ProtoScriptFunctionSignature signature,
        string projectFile,
        Func<AIFunctionArguments, CancellationToken, ValueTask<object?>> invoker)
    {
        Export = export;
        Signature = signature;
        _invoker = invoker;
        _schema = ToolSchemaMapper.CreateSchema(signature);
        _additionalProperties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["buffaly.toolSource"] = "ProtoScript",
            ["buffaly.projectFile"] = projectFile,
            ["buffaly.prototype"] = export.Prototype,
            ["buffaly.method"] = export.Method,
            ["buffaly.returnType"] = signature.ReturnTypeName
        };
    }

    public AgentKitManifestExport Export { get; }
    internal ProtoScriptFunctionSignature Signature { get; }
    public override string Name => Export.Name;
    public override string Description => Export.Description;
    public override JsonElement JsonSchema => _schema;
    public override MethodInfo? UnderlyingMethod => null;
    public override JsonSerializerOptions JsonSerializerOptions => JsonSerializerOptions.Default;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _additionalProperties;
    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken) => _invoker(arguments, cancellationToken);
}
