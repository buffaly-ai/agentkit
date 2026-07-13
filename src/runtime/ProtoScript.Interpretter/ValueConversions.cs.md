# ValueConversions.cs Change History

## StringRef Conversion Support (2026-03-13)
- Added conversion rules between `StringReference` and `string`/`StringWrapper`/`Prototype`.
- Updated assignability checks so `StringRef` can be returned from string expressions and passed back into string-typed ProtoScript parameters.
- Design Decision: keep one authoritative conversion path in `ValueConversions` rather than adding boundary-specific special cases.
