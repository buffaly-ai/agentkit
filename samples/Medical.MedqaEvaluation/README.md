# Medical MedQA Evaluation

This sample runs MedQA Arm 1 through Buffaly's direct provider contracts. It does not create `AgentKitRuntime`, a conversation, a tool loop, a session, or a watcher. Each case sends one fresh user message and an empty tools collection. An empty system prompt is represented by omitting the system message entirely.

Run a provider:

```bash
dotnet run --project samples/Medical.MedqaEvaluation -- \
  --input data/medqa.jsonl \
  --output runs/gpt-5.5-shard-0.jsonl \
  --metrics runs/gpt-5.5-shard-0.metrics.json \
  --provider openai \
  --model gpt-5.5 \
  --reasoning medium \
  --shard-index 0 \
  --shard-count 1
```

Credentials and endpoints are read from `OPENAI_API_KEY`, `XAI_API_KEY`, and `OLLAMA_BASE_URL`. Use separate output and metrics paths for each parallel shard. Existing case IDs in an output file are skipped on resume.

A credential-free validation uses the deterministic fixture provider:

```bash
dotnet run --project samples/Medical.MedqaEvaluation -- \
  --input samples/Medical.MedqaEvaluation/fixtures/arm1-smoke.jsonl \
  --output artifacts/medqa-smoke/results.jsonl \
  --metrics artifacts/medqa-smoke/metrics.json \
  --provider openai --model gpt-5.5 --reasoning medium --fixture true
```

The fixture marker in each question controls its answer and is only for validating the harness. It is never used in a live run.
