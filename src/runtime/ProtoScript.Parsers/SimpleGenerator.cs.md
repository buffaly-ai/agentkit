# SimpleGenerator.cs Change History

## Initializer Emission for Non-Assignment Entries (2026-04-15)
- Updated simple generation for `new` initializers to emit both assignment initializers and non-assignment initializer expressions.
- Design Decision: preserve collection-entry initializer syntax in generated output instead of assuming every initializer is `Name = Value`.
