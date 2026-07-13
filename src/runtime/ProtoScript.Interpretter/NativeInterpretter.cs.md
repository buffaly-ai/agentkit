# NativeInterpretter.cs Change History

## Return Type Coercion on External Invocation (2026-03-13)
- Added declared return-type coercion in the external `RunMethod(...)` invocation path used by `RunMethodAsObject(...)`.
- Design Decision: align external method execution with internal evaluation behavior so runtime return contracts (including `StringRef`) are enforced consistently.

## Narrowed External Coercion Scope (2026-03-13)
- Adjusted external `RunMethod(...)` coercion to convert only declared `StringRef` and `string` returns.
- Design Decision: preserve legacy non-string return behavior on this hot path while ensuring explicit string contracts cross the C# boundary correctly.

## Assignment Diagnostics for Annotation Invocation (2026-03-13)
- Improved function parameter assignment failures in `GetFunctionEvaluationScope2(...)` to include function name, parameter name/index, expected type, and actual value/type.
- Design Decision: annotation execution errors previously surfaced as generic `Cannot assign value`; richer diagnostics make malformed annotation usage immediately identifiable.

## Dotnet Initializer Application for Collection Entries (2026-04-15)
- Added runtime initializer application for compiled dotnet `new` instances: member initializers are assigned after construction, and collection initializer entries are applied by invoking instance `Add(...)`.
- Added explicit runtime failures when initializer application is invalid (for example, target type has no compatible `Add` method for a collection entry).
- Design Decision: execute initializer application in the interpreter after constructor activation to mirror C# initializer order while keeping failure diagnostics tied to parsing info.

## Dotnet Indexer Set Assignment Support (2026-04-15)
- Extended `EvaluateAsSet(Compiled.IndexOperator)` to resolve public instance dotnet indexer setters (single-parameter indexers) and return a runtime setter holder with converted key values.
- Extended `Evaluate(AssignmentOperator)` to apply assignments through the dotnet indexer setter holder using target-property type coercion.
- Design Decision: preserve existing prototype/indexed collection assignment behavior and add a focused dotnet branch for `obj[...]=...` assignments with explicit runtime diagnostics when no compatible setter exists.
