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

    private ProtoScriptToolSet(
        AgentKitManifest manifest,
        NativeInterpretter interpretter,
        List<ProtoScriptAIFunction> functions,
        ProtoScriptToolSetOptions options)
    {
        Manifest = manifest;
        _interpretter = interpretter;
        _options = options;
        Functions = functions;
    }

    public AgentKitManifest Manifest { get; }
    public IReadOnlyList<ProtoScriptAIFunction> Functions { get; }
    public IReadOnlyList<AIFunction> Tools => Functions.Cast<AIFunction>().ToArray();

    public static async Task<ProtoScriptToolSet> LoadAsync(
        string manifestFile,
        ProtoScriptToolSetOptions? options = null,
        CancellationToken cancellationToken = default)
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
            FunctionRuntimeInfo method = set.ResolveExportMethod(export);
            ProtoScriptFunctionSignature signature = CreateSignature(export, method);
            set.FunctionsInternal.Add(new ProtoScriptAIFunction(
                export,
                signature,
                manifest.ProjectFile,
                (arguments, ct) => set.InvokeAsync(export, signature, arguments, ct)));
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

        string message = string.Join(Environment.NewLine, compiler.Diagnostics.Select(diagnostic => diagnostic.Diagnostic?.Message ?? diagnostic.ToString()));
        throw new InvalidDataException("ProtoScript compilation failed:" + Environment.NewLine + message);
    }

    private static ProtoScriptFunctionSignature CreateSignature(AgentKitManifestExport export, FunctionRuntimeInfo method)
    {
        var compiledNames = method.Parameters.Select(parameter => parameter.ParameterName).ToHashSet(StringComparer.Ordinal);
        string? unknownDescription = export.ParameterDescriptions.Keys.FirstOrDefault(name => !compiledNames.Contains(name));
        if (unknownDescription is not null)
            throw new InvalidDataException($"Export '{export.Name}' provides a description for unknown compiled parameter '{unknownDescription}'.");

        var parameters = new List<ProtoScriptParameterSignature>(method.Parameters.Count);
        foreach (ParameterRuntimeInfo parameter in method.Parameters)
        {
            Type runtimeType = GetRuntimeType(export, parameter.ParameterName, parameter.OriginalType ?? parameter.Type);
            parameters.Add(new ProtoScriptParameterSignature(
                parameter.ParameterName,
                runtimeType,
                ToolSchemaMapper.GetTypeName(runtimeType),
                export.ParameterDescriptions.GetValueOrDefault(parameter.ParameterName, string.Empty)));
        }

        Type returnType = method.ReturnType?.Type
            ?? throw new InvalidDataException($"Export '{export.Name}' method '{export.Method}' has no compiled return type.");
        if (!ToolSchemaMapper.IsSupported(returnType))
            throw new InvalidDataException($"Export '{export.Name}' return type '{ToolSchemaMapper.GetTypeName(returnType)}' is not supported.");

        return new ProtoScriptFunctionSignature(parameters, returnType, ToolSchemaMapper.GetTypeName(returnType));
    }

    private static Type GetRuntimeType(AgentKitManifestExport export, string parameterName, TypeInfo? typeInfo)
    {
        Type runtimeType = typeInfo?.Type
            ?? throw new InvalidDataException($"Export '{export.Name}' parameter '{parameterName}' has no compiled type.");
        if (!ToolSchemaMapper.IsSupported(runtimeType))
            throw new InvalidDataException($"Export '{export.Name}' parameter '{parameterName}' type '{ToolSchemaMapper.GetTypeName(runtimeType)}' is not supported.");
        return runtimeType;
    }

    private async ValueTask<object?> InvokeAsync(
        AgentKitManifestExport export,
        ProtoScriptFunctionSignature signature,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.InvocationTimeout);
        await _gate.WaitAsync(timeout.Token).ConfigureAwait(false);
        try
        {
            var bound = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (ProtoScriptParameterSignature parameter in signature.Parameters)
            {
                if (!arguments.TryGetValue(parameter.Name, out object? value))
                    throw new ArgumentException($"Missing required argument '{parameter.Name}'.");
                bound[parameter.Name] = ConvertArgument(value, parameter.RuntimeType);
            }

            Prototype? instance = string.IsNullOrWhiteSpace(export.Prototype)
                ? null
                : ResolvePrototype(export.Prototype);
            return _interpretter.RunMethodAsObject(instance, export.Method, bound);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static object ConvertArgument(object? value, Type type)
    {
        if (value is JsonElement element)
            value = FromJsonElement(element);

        if (type == typeof(int)) return Convert.ToInt32(value);
        if (type == typeof(long)) return Convert.ToInt64(value);
        if (type == typeof(decimal)) return Convert.ToDecimal(value);
        if (type == typeof(double)) return Convert.ToDouble(value);
        if (type == typeof(float)) return Convert.ToSingle(value);
        if (type == typeof(bool)) return Convert.ToBoolean(value);
        if (type == typeof(JsonObject)) return value is JsonObject jsonObject ? jsonObject : JsonNode.Parse(JsonSerializer.Serialize(value))!.AsObject();
        if (type == typeof(JsonArray)) return value is JsonArray jsonArray ? jsonArray : JsonNode.Parse(JsonSerializer.Serialize(value))!.AsArray();
        return value?.ToString() ?? string.Empty;
    }

    private static object? FromJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out long integer) ? integer : element.GetDouble(),
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

    private FunctionRuntimeInfo ResolveExportMethod(AgentKitManifestExport export)
    {
        if (string.IsNullOrWhiteSpace(export.Prototype))
        {
            return _interpretter.Symbols.GetGlobalScope().GetSymbol(export.Method) as FunctionRuntimeInfo
                ?? throw new InvalidDataException($"Export '{export.Name}' method '{export.Method}' was not found.");
        }

        Prototype prototype = ResolvePrototype(export.Prototype);
        return _interpretter.FindOverriddenMethod(prototype, export.Method)
            ?? throw new InvalidDataException($"Export '{export.Name}' method '{export.Method}' was not found on prototype '{export.Prototype}'.");
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        _gate.Dispose();
        return ValueTask.CompletedTask;
    }
}
