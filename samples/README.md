# Buffaly Agent Kit samples

| Sample | Run command | What it proves |
| --- | --- | --- |
| Medical referral readiness | `./samples/run-sample.sh medical` or `.\samples\run-sample.ps1 medical` | Domain-specific administrative rules and auditable read-only use |
| Return resolution | `./samples/run-sample.sh returns` or `.\samples\run-sample.ps1 returns` | Controlled proposal creation with human approval outside the agent |
| Incident investigation | `./samples/run-sample.sh incident` or `.\samples\run-sample.ps1 incident` | Headless embedding and evidence-oriented tool orchestration |

All samples default to the deterministic scripted provider. Scripted mode is demonstration infrastructure: it emits real provider-facing function calls through Agent Kit and validates the observed tool results, but it is not a language model simulation. A real model is enabled by replacing the registered `IChatClient`; no Agent Kit runtime or tool code needs to change.

The samples use synthetic checked-in data only, require no network access or API keys, and demonstrate mixed C# plus ProtoScript tools. Web samples persist local conversations under their `.agentkit/` folders. The incident sample writes `incident-report.md`, `events.jsonl`, and `conversation.json` under `samples/DevOps.IncidentInvestigation/output/`.
