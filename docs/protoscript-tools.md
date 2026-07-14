# ProtoScript tools

`Buffaly.AgentKit.ProtoScript` adapts frozen ProtoScript runtime methods into `Microsoft.Extensions.AI.AIFunction` tools.

## ProtoScript in this kit

ProtoScript is a small prototype-oriented scripting/runtime layer used by Buffaly for rule-like logic and ontology-backed functions. Agent Kit uses a frozen local runtime package and exposes only manifest-allowlisted methods.

## Tool project example

`AgentTools/Project.pts`:

```protoscript
prototype AddNumbers
{
    function Execute(int a, int b): int
    {
        return a + b;
    }
}
```

## Manifest schema

`AgentTools/agentkit.json`:

```json
{
  "schemaVersion": 1,
  "projectFile": "Project.pts",
  "exports": [
    {
      "name": "add_numbers",
      "prototype": "AddNumbers",
      "method": "Execute",
      "description": "Add two integers.",
      "parameterDescriptions": {
        "a": "First integer.",
        "b": "Second integer."
      }
    }
  ]
}
```

Fields:

- `schemaVersion`: currently `1`.
- `projectFile`: relative path to the `.pts` project file. Absolute paths and path escapes are rejected.
- `exports`: explicit allowlist of model-visible tools.
- `name`: model-visible tool name.
- `prototype`: optional ProtoScript prototype name for instance/prototype methods.
- `method`: method/function name to invoke.
- `description`: tool description exposed to the provider.
- `parameterDescriptions`: optional descriptions keyed by compiled parameter name. Unknown names fail loading.

## Supported compiled types

- `string`
- `int`
- `long`
- `decimal`
- `double`
- `float`
- `bool`
- `JsonObject`
- `JsonArray`

The `.pts` function signature is authoritative. Agent Kit reads parameter names, parameter types, and the return type from the compiled `FunctionRuntimeInfo`, projects all compiled parameters as required in JSON Schema, and rejects unsupported compiled types while loading. The manifest does not duplicate types or required flags.

## Loading tools

```csharp
await using ProtoScriptToolSet toolSet =
    await ProtoScriptToolSet.LoadAsync("AgentTools/agentkit.json");

IReadOnlyList<AIFunction> tools = toolSet.Tools;
```

## Options

```csharp
var options = new ProtoScriptToolSetOptions
{
    ExecuteStartupStatements = true,
    InvocationTimeout = TimeSpan.FromMinutes(2)
};
```

`ProtoScriptToolSetOptions` intentionally exposes only implemented behavior. Calls through one tool set are bounded by `InvocationTimeout`.

## Mixing C# and ProtoScript tools

```csharp
IEnumerable<AIFunction> csharpTools = MyFunctions.Create();
await using ProtoScriptToolSet protoTools = await ProtoScriptToolSet.LoadAsync("AgentTools/agentkit.json");
AIFunction[] allTools = csharpTools.Concat(protoTools.Tools).ToArray();
```

## Thread safety

The ProtoScript interpreter is invoked through a `SemaphoreSlim`. Calls through one `ProtoScriptToolSet` are serialized.

## Security boundaries

- `projectFile` must be relative to the manifest.
- `projectFile` cannot escape the manifest directory.
- Exports are explicit; no automatic prototype discovery.
- No remote loading is performed by Agent Kit.
- Do not place secrets in manifests or `.pts` files.

## Repository examples

- `samples/Tools/agentkit.json`
- `samples/DevOps.IncidentInvestigation/AgentTools/IncidentRules.pts`
- `samples/Medical.ReferralReadiness/AgentTools/ReferralRules.pts`
- `samples/Commerce.ReturnResolution/AgentTools/ReturnPolicyRules.pts`

