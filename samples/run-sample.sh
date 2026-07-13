#!/usr/bin/env bash
set -euo pipefail
sample="${1:-}"
if [[ "$sample" != "medical" && "$sample" != "returns" && "$sample" != "incident" ]]; then
  echo "Usage: ./samples/run-sample.sh medical|returns|incident" >&2
  exit 2
fi
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root"
expected=$(python - <<'PY'
import json
print(json.load(open('global.json'))['sdk']['version'])
PY
)
sdk=$(dotnet --version)
if [[ "$sdk" != "$expected" ]]; then
  echo "Warning: pinned SDK is $expected but dotnet reports $sdk; continuing." >&2
fi
dotnet restore Buffaly.AgentKit.sln --locked-mode
export AgentKit__Provider=scripted
case "$sample" in
  medical)
    export AGENTKIT_SAMPLE_STORAGE="$root/samples/Medical.ReferralReadiness/.agentkit"
    echo "Starting Medical Referral Readiness at http://127.0.0.1:5101/referrals.html"
    dotnet run --project samples/Medical.ReferralReadiness/Medical.ReferralReadiness.csproj -c Release --urls http://127.0.0.1:5101
    ;;
  returns)
    export AGENTKIT_SAMPLE_STORAGE="$root/samples/Commerce.ReturnResolution/.agentkit"
    echo "Starting Commerce Return Resolution at http://127.0.0.1:5102/orders.html"
    dotnet run --project samples/Commerce.ReturnResolution/Commerce.ReturnResolution.csproj -c Release --urls http://127.0.0.1:5102
    ;;
  incident)
    echo "Running DevOps Incident Investigation."
    dotnet run --project samples/DevOps.IncidentInvestigation/DevOps.IncidentInvestigation.csproj -c Release
    echo "Outputs:"
    echo "  samples/DevOps.IncidentInvestigation/output/incident-report.md"
    echo "  samples/DevOps.IncidentInvestigation/output/events.jsonl"
    echo "  samples/DevOps.IncidentInvestigation/output/conversation.json"
    ;;
esac
