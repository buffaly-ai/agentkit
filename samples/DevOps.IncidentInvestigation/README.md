# DevOps Incident Investigation sample

This console sample demonstrates headless Agent Kit embedding with mixed C# fixture tools and a ProtoScript evidence classifier. It uses only synthetic data under `Data/`, writes an incident report and JSONL event trace to `output/`, and performs no remediation.

To adapt it, replace `IncidentFunctions` with bounded adapters for telemetry, logs, deployment history, and runbook repositories. Keep the Agent Kit runtime, event sink, and ProtoScript manifest unchanged.
