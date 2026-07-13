# NewObjectExpression.cs Change History

## Initializer Model Alignment for Collection Entry Parsing (2026-04-15)
- Adjusted `NewObjectExpression` companion history for collection initializer feature work in parser/compiler/runtime.
- Design Decision: continue using a shared initializer expression list where object-member initializers remain explicit nodes and collection entries remain expression-based for lowering.
