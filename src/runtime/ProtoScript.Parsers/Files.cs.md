# Files.cs Change History

## Import Path Strictness (2026-03-12)
- Updated `import` parsing to reject file-path usage such as `import "x.pts";`.
- Design Decision: enforce one clear contract where file inclusion must use `include ...;` and `import` is reserved for type imports.
