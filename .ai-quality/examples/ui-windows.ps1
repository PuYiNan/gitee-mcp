param(
    [Parameter(Mandatory)] [string] $WorkItemId,
    [Parameter(Mandatory)] [string] $EvidenceDirectory
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$testProject = Join-Path $repoRoot 'tests\YourProduct.WindowsUiTests\YourProduct.WindowsUiTests.csproj'
if (-not (Test-Path -LiteralPath $testProject)) {
    throw "Configure the Appium/FlaUI test project path in $PSCommandPath"
}

$results = Join-Path $EvidenceDirectory 'windows-ui'
New-Item -ItemType Directory -Path $results -Force | Out-Null
$env:AI_QUALITY_EVIDENCE = $results

dotnet test $testProject --configuration Release --logger trx --results-directory $results
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The UI test fixture should start the packaged application, target stable
# AutomationId values, and save failure screenshots to AI_QUALITY_EVIDENCE.
