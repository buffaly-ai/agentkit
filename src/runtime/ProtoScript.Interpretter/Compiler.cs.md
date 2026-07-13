# Compiler.cs Change History

## Include Missing-File Diagnostics (2026-03-12)
- Updated include-file parsing flow to carry include-site context into missing-file failures.
- Design Decision: wrap missing include targets as `ProtoScriptCompilerException` with `IncludeStatement.Info` so callers receive file/offset for the failing include line instead of a contextless runtime error.

## StringRef Built-in Type Registration (2026-03-13)
- Added built-in type aliases `StringRef` and `stringref` during compiler initialization.
- Design Decision: expose string-reference return/parameter contracts without requiring project-level imports.

## Method Evaluation Null-Guard Diagnostics (2026-03-13)
- Hardened `Compile(MethodEvaluation)` to report diagnostics when method name is missing, `nameof` has no parameters, or a non-function symbol is invoked.
- Design Decision: convert prior `NullReferenceException` crash paths into actionable compiler diagnostics with statement context so best-effort mode can skip offending files cleanly.

## Dotnet Collection Initializer Entry Lowering (2026-04-15)
- Updated dotnet `new` compilation to bucket initializer entries by kind during lowering: named member assignments map into `DotNetNewInstance.MemberInitializers`, and collection-style entries map into `DotNetNewInstance.CollectionInitializers`.
- Design Decision: keep initializer lowering explicit and typed in `DotNetNewInstance` rather than rejecting collection entries at compile time, so runtime can apply deterministic initializer semantics.
- Note: this trimmed feature pass is scoped to member + collection entry lowering for dotnet `new` object creation.
