# Real-provider smoke sample

Opt-in OpenAI proof using the frozen `add_numbers` ProtoScript tool. It skips without network access unless `AGENTKIT_REAL_PROVIDER=1`; when enabled it also requires `OPENAI_API_KEY`. `OPENAI_MODEL` defaults to `gpt-4o-mini`.

```powershell
$env:AGENTKIT_REAL_PROVIDER = "1"
$env:OPENAI_API_KEY = "..."
dotnet run --project samples/AgentKit.RealProviderSmoke
```

Success requires a ProtoScript tool-call event and a final answer containing the observed result. It is not run by default CI.
