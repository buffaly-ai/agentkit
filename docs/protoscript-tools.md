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
      "parameters": [
        { "name": "a", "type": "int", "required": true },
        { "name": "b", "type": "int", "required": true }
      ],
      "returnType": "int"
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
- `parameters`: name/type/description/required metadata.
- `returnType`: return type metadata.

## Supported manifest types

- `string`
- `int`
- `long`
- `decimal`
- `double`
- `float`
- `bool`
- `JsonObject`
- `JsonArray`

The manifest converter accepts these types. The frozen ProtoScript compiler may not accept every .NET type name as a script-language signature. The included samples use compiler-compatible script signatures such as `int`, `string`, and `bool`.

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

`Globals` exists on `ProtoScriptToolSetOptions` for future/global binding scenarios; the current loader does not require it for the included samples.

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
