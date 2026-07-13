# Buffaly Agent Kit 1.0

Buffaly Agent Kit is a local-first reference implementation for a provider-neutral agent/tool loop with optional ProtoScript tool adapters and ASP.NET Core hosting.

## Packages

- `Buffaly.AgentKit` — headless runtime, conversations, events, tool policy, and turn loop.
- `Buffaly.AgentKit.ProtoScript` — manifest-allowlisted ProtoScript tools exposed as `AIFunction` instances.
- `Buffaly.AgentKit.AspNetCore` — DI registration, JSONL/in-memory stores, API endpoints, and static inspector.
- Frozen runtime packages — local ProtoScript/Ontology runtime packages used by the adapter.

## Target Framework

This local build targets `net9.0` because the .NET 10 SDK is not installed on this machine. `FREEZE.md` records the temporary target-framework deviation.

## License

GPL-3.0-only. See `LICENSE`.
