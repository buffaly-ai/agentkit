param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("medical", "returns", "incident")]
    [string]$Sample
)

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root
$sdk = (dotnet --version)
$expected = (Get-Content global.json -Raw | ConvertFrom-Json).sdk.version
if ($sdk -ne $expected) { Write-Warning "Pinned SDK is $expected but dotnet reports $sdk. Continuing with installed SDK." }

dotnet restore Buffaly.AgentKit.sln --locked-mode
$env:AgentKit__Provider = "scripted"

switch ($Sample) {
    "medical" {
        $env:AGENTKIT_SAMPLE_STORAGE = Join-Path $root "samples\Medical.ReferralReadiness\.agentkit"
        Write-Host "Starting Medical Referral Readiness at http://127.0.0.1:5101/referrals.html"
        dotnet run --project samples\Medical.ReferralReadiness\Medical.ReferralReadiness.csproj -c Release --urls http://127.0.0.1:5101
    }
    "returns" {
        $env:AGENTKIT_SAMPLE_STORAGE = Join-Path $root "samples\Commerce.ReturnResolution\.agentkit"
        Write-Host "Starting Commerce Return Resolution at http://127.0.0.1:5102/orders.html"
        dotnet run --project samples\Commerce.ReturnResolution\Commerce.ReturnResolution.csproj -c Release --urls http://127.0.0.1:5102
    }
    "incident" {
        Write-Host "Running DevOps Incident Investigation."
        dotnet run --project samples\DevOps.IncidentInvestigation\DevOps.IncidentInvestigation.csproj -c Release
        Write-Host "Outputs:"
        Write-Host "  samples\DevOps.IncidentInvestigation\output\incident-report.md"
        Write-Host "  samples\DevOps.IncidentInvestigation\output\events.jsonl"
        Write-Host "  samples\DevOps.IncidentInvestigation\output\conversation.json"
    }
}
