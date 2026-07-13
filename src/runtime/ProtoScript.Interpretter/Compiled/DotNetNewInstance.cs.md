# DotNetNewInstance.cs Change History

## Collection Initializer Buckets for Dotnet `new` (2026-04-15)
- Added typed initializer payload models for lowered dotnet object creation: `CollectionInitializer` and `MemberInitializer`.
- Added per-kind storage lists on `DotNetNewInstance` so compilation/runtime do not rely on untyped initializer expressions.
- Design Decision: separate initializer kinds at the compiled-expression level to keep runtime application simple and deterministic.
