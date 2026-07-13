# StringReference.cs Change History

## Initial Creation (2026-03-13)
- Added `StringReference` as an opaque handle carrying a string prototype name across C#/ProtoScript boundaries.
- Implemented helper resolution methods to recover prototype and string value within the active runtime context.
- Design Decision: transport only prototype identity for large text payloads to avoid cross-boundary string serialization.
