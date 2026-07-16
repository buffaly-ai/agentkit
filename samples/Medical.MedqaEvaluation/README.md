# MedQA Arm 1: deterministic direct-model evaluation

This sample is a complete, reproducible example of running the MedQA-USMLE Arm 1 baseline through the small open-source Buffaly slice.

Arm 1 is intentionally **not an agent benchmark**. The target model receives only one rendered multiple-choice prompt. There is no agent reasoning loop, tool selection, session history, watcher, RAG, ontology, or hidden framework prompt.

## What executes

For each JSONL case, `Program.RunAsync`:

1. validates the complete input and records its SHA-256 hash;
2. renders the fixed `arm1-v1` prompt;
3. calls `ProviderCompletionClient.AskModelAsync` with:
   - `systemPrompt: string.Empty`;
   - one user prompt;
   - an empty tools collection;
   - explicit provider, model, and reasoning level;
4. parses one A/B/C/D answer;
5. scores exact match;
6. appends and flushes one result row;
7. writes aggregate metrics and a run manifest.

`AgentKitRuntime`, `AgentConversation`, sessions, and watchers are not referenced by this sample.

## Prerequisites

- .NET 9 SDK
- a normalized MedQA-USMLE four-option test JSONL
- one provider:
  - `OPENAI_API_KEY` for OpenAI;
  - `XAI_API_KEY` for xAI;
  - a running Ollama server, optionally selected with `OLLAMA_BASE_URL`

The expected input schema is:

```json
{"source_case_id":1,"question":"...","options":{"A":"...","B":"...","C":"...","D":"..."},"answer":"B"}
```

The canonical Arm 1 test input contains 1,273 unique cases with IDs 1–1,273. Keep datasets outside this repository unless their license permits redistribution.

## 1. Build and test

From the repository root:

```bash
dotnet restore Buffaly.AgentKit.sln --force-evaluate
dotnet test tests/Buffaly.AgentKit.Tests/Buffaly.AgentKit.Tests.csproj --no-restore
dotnet test tests/Buffaly.AgentKit.SampleTests/Buffaly.AgentKit.SampleTests.csproj --no-restore --filter 'FullyQualifiedName~MedqaEvaluationTests'
dotnet build Buffaly.AgentKit.sln --no-restore
```

The provider transport tests inspect outbound JSON and assert that an empty system prompt does not create a system-role message and that an empty tools collection is not serialized.

## 2. Credential-free harness check

```bash
dotnet run --project samples/Medical.MedqaEvaluation -- \
  --input samples/Medical.MedqaEvaluation/fixtures/arm1-smoke.jsonl \
  --output artifacts/medqa-smoke/results.jsonl \
  --metrics artifacts/medqa-smoke/metrics.json \
  --provider openai \
  --model gpt-5.5 \
  --reasoning medium \
  --fixture true
```

Run the same command twice. `results.jsonl` must still contain two rows; that proves resume behavior.

Fixture mode validates the harness only. It does not call a real model and must never be reported as benchmark evidence.

## 3. Run one live case first

Create a one-row JSONL from the real dataset, then run the intended provider. For local Ollama:

```bash
export OLLAMA_BASE_URL=http://localhost:11434

dotnet run --project samples/Medical.MedqaEvaluation -- \
  --input data/medqa-one-case.jsonl \
  --output runs/medgemma-smoke/results.jsonl \
  --metrics runs/medgemma-smoke/metrics.json \
  --provider ollama \
  --model medgemma:27b \
  --reasoning ''
```

Inspect all three files:

- `results.jsonl`
- `metrics.json`
- `metrics.json.manifest.json`

The manifest records the empty system prompt, empty tool list, input path/hash/count, selected catalog, package versions, provider, model, reasoning level, and shard coordinates.

## 4. Run all 1,273 cases

Single process:

```bash
dotnet run --project samples/Medical.MedqaEvaluation -- \
  --input /absolute/path/medqa_usmle_4options_test_normalized.jsonl \
  --output runs/medgemma-27b/results.jsonl \
  --metrics runs/medgemma-27b/metrics.json \
  --provider ollama \
  --model medgemma:27b \
  --reasoning '' \
  --shard-index 0 \
  --shard-count 1
```

The runner appends each completed case immediately. If interrupted, rerun the same command; existing `Source_Case_Id` values are skipped.

## 5. Parallel shards

Start separate processes with the same input/provider/model and distinct output paths. Example for four shards:

```bash
for shard in 0 1 2 3; do
  dotnet run --project samples/Medical.MedqaEvaluation -- \
    --input /absolute/path/medqa_usmle_4options_test_normalized.jsonl \
    --output "runs/medgemma-27b/shard-${shard}.jsonl" \
    --metrics "runs/medgemma-27b/shard-${shard}.metrics.json" \
    --provider ollama --model medgemma:27b --reasoning '' \
    --shard-index "$shard" --shard-count 4 &
done
wait
```

Each process owns its output and state. `Program.MergeShards` sorts by case ID and rejects duplicates; the sample tests exercise this behavior.

## Provider/model matrix

| Provider | Models | Credential/endpoint |
|---|---|---|
| `openai` | `gpt-5.5`, `gpt-5.4-mini` | `OPENAI_API_KEY` |
| `xai` | `grok-4.3` | `XAI_API_KEY` |
| `ollama` | `glm-5.2`, `gemma3:27b`, `gemma4:31b`, `medgemma:27b` | `OLLAMA_BASE_URL` |

Catalog validation is exact and case-sensitive. Unknown, disabled, or mismatched provider/model/reasoning selections fail before provider execution.

## Methodology verification checklist

Before accepting a run, verify:

- input manifest reports 1,273 unique cases and the expected SHA-256;
- `SystemPrompt` is `""`;
- `Tools` is `[]`;
- output contains 1,273 unique `Source_Case_Id` values;
- provider/model/reasoning are constant across all rows;
- no row has `ParseStatus: "error"` unless explicitly included in analysis;
- metrics agree with recomputation from result rows;
- repository commit and provider catalog are preserved with the run artifacts.

## Important parser parity note

This sample preserves the original C# parser ordering for comparability. A response beginning with a standalone answer letter can be recovered before the later multiple-label scan. For example, `A is wrong, B is correct` is recovered as `A`. Changing that behavior requires a new prompt/parser version rather than silently altering Arm 1.
