using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Ontology;
using ProtoScript.Interpretter;
using ProtoScript.Interpretter.Compiled;
using ProtoScript.Interpretter.RuntimeInfo;

namespace Buffaly.AgentKit.ProtoScript;

public sealed class ProtoScriptToolSet : IAsyncDisposable
{
    private readonly NativeInterpretter _interpretter;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ProtoScriptToolSetOptions _options;
    private bool _disposed;

    private ProtoScriptToolSet(AgentKitManifest manifest, NativeInterpretter interpretter, List<ProtoScriptAIFunction> functions, ProtoScriptToolSetOptions options)
    {
        Manifest = manifest;
        _interpretter = interpretter;
        _options = options;
        Functions = functions;
    }

    public AgentKitManifest Manifest { get; }
    public IReadOnlyList<ProtoScriptAIFunction> Functions { get; }
    public IReadOnlyList<AIFunction> Tools => Functions.Cast<AIFunction>().ToArray();

    public static async Task<ProtoScriptToolSet> LoadAsync(string manifestFile, ProtoScriptToolSetOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ProtoScriptToolSetOptions();
        AgentKitManifest manifest = await AgentKitManifest.LoadAsync(manifestFile, cancellationToken).ConfigureAwait(false);
        string projectFile = manifest.ResolveProjectFile(manifestFile);
        Initializer.Initialize();
        NativeInterpretter interpretter = new(new Compiler());
        interpretter.Compiler.Initialize();
        LoadProjectFile(interpretter, projectFile, options.ExecuteStartupStatements);
        var set = new ProtoScriptToolSet(manifest, interpretter, [], options);
        foreach (AgentKitManifestExport export in manifest.Exports)
        {
            set.ValidateExportMethod(export);
            set.FunctionsInternal.Add(new ProtoScriptAIFunction(export, (args, ct) => set.InvokeAsync(export, args, ct)));
        }
        return set;
    }

    private List<ProtoScriptAIFunction> FunctionsInternal => (List<ProtoScriptAIFunction>)Functions;

    private static void LoadProjectFile(NativeInterpretter interpretter, string projectFile, bool executeStartupStatements)
    {
        List<Statement> startupStatements = interpretter.Compiler.CompileProject(projectFile);
        ThrowIfCompilerDiagnostics(interpretter.Compiler);

        if (executeStartupStatements)
            interpretter.InterpretStatements(startupStatements);
    }

    private static void ThrowIfCompilerDiagnostics(Compiler compiler)
    {
        if (compiler.Diagnostics.Count == 0)
            return;

        string message = string.Join(Environment.NewLine, compiler.Diagnostics.Select(d => d.Diagnostic?.Message ?? d.ToString()));
        throw new InvalidDataException("ProtoScript compilation failed:" + Environment.NewLine + message);
    }

    private async ValueTask<object?> InvokeAsync(AgentKitManifestExport export, AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.InvocationTimeout);
        await _gate.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            Dictionary<string, object> bound = new(StringComparer.Ordinal);
            foreach (AgentKitManifestParameter parameter in export.Parameters)
            {
                if (!arguments.TryGetValue(parameter.Name, out object? value))
                {
                    if (parameter.Required) throw new ArgumentException($"Missing required argument '{parameter.Name}'.");
                    continue;
                }
                bound[parameter.Name] = ConvertArgument(value, parameter.Type);
            }

            Prototype? instance = null;
            if (!string.IsNullOrWhiteSpace(export.Prototype))
                instance = ResolvePrototype(export.Prototype);
            return _interpretter.RunMethodAsObject(instance, export.Method, bound);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static object ConvertArgument(object? value, string type)
    {
        if (value is JsonElement element) value = FromJsonElement(element);
        return type.ToLowerInvariant() switch
        {
            "int" => Convert.ToInt32(value),
            "long" => Convert.ToInt64(value),
            "decimal" => Convert.ToDecimal(value),
            "double" => Convert.ToDouble(value),
            "float" => Convert.ToSingle(value),
            "bool" => Convert.ToBoolean(value),
            "jsonobject" => value is JsonObject jo ? jo : JsonNode.Parse(JsonSerializer.Serialize(value))!.AsObject(),
            "jsonarray" => value is JsonArray ja ? ja : JsonNode.Parse(JsonSerializer.Serialize(value))!.AsArray(),
            _ => value?.ToString() ?? string.Empty
        };
    }

    private static object? FromJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => JsonNode.Parse(element.GetRawText())!.AsObject(),
        JsonValueKind.Array => JsonNode.Parse(element.GetRawText())!.AsArray(),
        _ => element.GetRawText()
    };

    private Prototype ResolvePrototype(string prototypeName)
    {
        if (_interpretter.Symbols.GetGlobalScope().TryGetSymbol(prototypeName, out object? symbol)
            && symbol is PrototypeTypeInfo prototypeTypeInfo
            && prototypeTypeInfo.Prototype != null)
            return prototypeTypeInfo.Prototype;

        return Prototypes.GetPrototypeByPrototypeName(prototypeName);
    }

    private void ValidateExportMethod(AgentKitManifestExport export)
    {
        if (string.IsNullOrWhiteSpace(export.Prototype))
        {
            if (_interpretter.Symbols.GetGlobalScope().GetSymbol(export.Method) is not FunctionRuntimeInfo)
                throw new InvalidDataException($"Export '{export.Name}' method '{export.Method}' was not found.");
            return;
        }

        Prototype prototype = ResolvePrototype(export.Prototype);
        if (_interpretter.FindOverriddenMethod(prototype, export.Method) == null)
            throw new InvalidDataException($"Export '{export.Name}' method '{export.Method}' was not found on prototype '{export.Prototype}'.");
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}

