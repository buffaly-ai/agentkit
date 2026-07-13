# NewObjectExpressions.cs Change History

## Parser Support for Collection-Style Initializer Entries (2026-04-15)
- Updated object-initializer parsing to preserve non-assignment entries as full expressions inside initializer lists.
- Kept named assignments (`Name = Value`) as `ObjectInitializer` nodes while allowing collection-style entries like `{ "value" }`.
- Design Decision: parse collection entries at the source syntax layer and defer semantic validation to compiler/runtime stages.
