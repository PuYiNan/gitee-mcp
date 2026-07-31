param(
    [Parameter(Mandatory)] [string] $WorkItemId,
    [Parameter(Mandatory)] [string] $EvidenceDirectory
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$testProject = Join-Path $repoRoot 'tests\YourProduct.E2E\YourProduct.E2E.csproj'
if (-not (Test-Path -LiteralPath $testProject)) {
    throw "Configure the Playwright test project path in $PSCommandPath"
}

$results = Join-Path $EvidenceDirectory 'playwright'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$env:AI_QUALITY_EVIDENCE = $results

dotnet test $testProject --configuration Release --logger trx --results-directory $results
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Configure Playwright tracing/screenshots in the test project and write them to
# AI_QUALITY_EVIDENCE. Copy this file to .ai-quality/hooks/ui.ps1 after adapting it.
