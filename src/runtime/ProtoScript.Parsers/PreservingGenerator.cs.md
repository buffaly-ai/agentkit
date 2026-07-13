# PreservingGenerator.cs Change History

## Preserving Output for Collection Initializer Entries (2026-04-15)
- Updated preserving generator `new` initializer emission to handle both object-member assignments and raw initializer expressions.
- Design Decision: keep round-trip output faithful for collection-style entries while retaining explicit handling for object member initializers.
