[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9][a-z0-9-]{2,63}$')] [string] $WorkItemId,
    [ValidateSet('Quick', 'Full')] [string] $Mode = 'Full',
    [string] $Target
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$qualityRoot = Join-Path $repoRoot '.ai-quality'
$item = Join-Path $qualityRoot "work-items\$WorkItemId"
$statePath = Join-Path $item 'state.json'
$configPath = Join-Path $qualityRoot 'config.json'

if (-not (Test-Path -LiteralPath $statePath)) { throw "Unknown work item: $WorkItemId" }
$state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json
if ($state.state -notin @('implementation-authorized', 'verification-failed')) {
    throw "Quality gate requires implementation-authorized or verification-failed; current state is $($state.state)."
}
$config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json

function Assert-ApprovalHash([string] $Stage, [string] $Artifact) {
    $approvalPath = Join-Path $item "approvals\$Stage.json"
    if (-not (Test-Path -LiteralPath $approvalPath)) { throw "Missing $Stage approval." }
    $approval = Get-Content -Raw -LiteralPath $approvalPath | ConvertFrom-Json
    $artifactPath = Join-Path $item $Artifact
    $current = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifactPath).Hash.ToLowerInvariant()
    if ($approval.artifactSha256 -ne $current) {
        throw "$Artifact changed after $Stage approval. Return to the appropriate approval stage."
    }
}

Assert-ApprovalHash 'requirements' 'spec.md'
Assert-ApprovalHash 'plan' 'plan.md'
Assert-ApprovalHash 'tests' 'test-matrix.md'

if (-not $Target -and $config.solution) {
    $Target = Join-Path $repoRoot $config.solution
}
if (-not $Target) {
    $candidates = @(Get-ChildItem -LiteralPath $repoRoot -Recurse -File | Where-Object {
        ($_.Extension -in @('.sln', '.slnx')) -and $_.FullName -notlike "*$([IO.Path]::DirectorySeparatorChar).ai-quality$([IO.Path]::DirectorySeparatorChar)*"
    })
    if ($candidates.Count -eq 0) {
        $candidates = @(Get-ChildItem -LiteralPath $repoRoot -Recurse -Filter '*.csproj' -File | Where-Object {
            $_.FullName -notlike "*$([IO.Path]::DirectorySeparatorChar).ai-quality$([IO.Path]::DirectorySeparatorChar)*"
        })
    }
    if ($candidates.Count -ne 1) {
        throw "Expected exactly one solution/project or config.solution; found $($candidates.Count)."
    }
    $Target = $candidates[0].FullName
}
if (-not [IO.Path]::IsPathRooted($Target)) { $Target = Join-Path $repoRoot $Target }
$Target = (Resolve-Path -LiteralPath $Target).Path

$evidenceRoot = Join-Path $item 'evidence'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDirectory = Join-Path $evidenceRoot "quality-gate-$runId"
$testResults = Join-Path $runDirectory 'test-results'
New-Item -ItemType Directory -Path $testResults -Force | Out-Null
$startedAt = (Get-Date).ToUniversalTime()
$steps = [System.Collections.Generic.List[object]]::new()
$overall = 'Passed'
$failure = $null

function Invoke-GateStep([string] $Name, [string] $FilePath, [string[]] $Arguments) {
    $stepStart = (Get-Date).ToUniversalTime()
    $safeName = $Name -replace '[^a-zA-Z0-9-]', '-'
    $logPath = Join-Path $runDirectory "$safeName.log"
    Write-Host "==> $Name"
    & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) { $exitCode = 0 }
    $stepEnd = (Get-Date).ToUniversalTime()
    $steps.Add([pscustomobject]@{
        name = $Name
        command = "$FilePath $($Arguments -join ' ')"
        status = if ($exitCode -eq 0) { 'Passed' } else { 'Failed' }
        exitCode = $exitCode
        startedAt = $stepStart.ToString('o')
        finishedAt = $stepEnd.ToString('o')
        log = [IO.Path]::GetRelativePath($repoRoot, $logPath)
    })
    if ($exitCode -ne 0) { throw "$Name failed with exit code $exitCode." }
}

Push-Location $repoRoot
try {
    Invoke-GateStep 'restore' 'dotnet' @('restore', $Target)
    if ($config.requireFormatCheck) {
        Invoke-GateStep 'format-check' 'dotnet' @('format', $Target, '--verify-no-changes', '--no-restore')
    }
    Invoke-GateStep 'release-build' 'dotnet' @('build', $Target, '--configuration', 'Release', '--no-restore', '-warnaserror')
    Invoke-GateStep 'tests' 'dotnet' @('test', $Target, '--configuration', 'Release', '--no-build', '--logger', 'trx', '--results-directory', $testResults)

    if ($Mode -eq 'Full') {
        $fullHook = Join-Path $repoRoot $config.fullHook
        if (Test-Path -LiteralPath $fullHook) {
            Invoke-GateStep 'full-hook' 'pwsh' @('-NoProfile', '-File', $fullHook, '-WorkItemId', $WorkItemId, '-EvidenceDirectory', $runDirectory)
        }

        $uiHook = Join-Path $repoRoot $config.uiHook
        if ($state.uiScope) {
            if ($config.requireUiHookWhenUiInScope -and -not (Test-Path -LiteralPath $uiHook)) {
                throw "UI scope requires an implemented hook at $($config.uiHook)."
            }
            if (Test-Path -LiteralPath $uiHook) {
                Invoke-GateStep 'ui-hook' 'pwsh' @('-NoProfile', '-File', $uiHook, '-WorkItemId', $WorkItemId, '-EvidenceDirectory', $runDirectory)
            }
        }
    }
}
catch {
    $overall = 'Failed'
    $failure = $_.Exception.Message
}
finally {
    Pop-Location
}

$finishedAt = (Get-Date).ToUniversalTime()
$specHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $item 'spec.md')).Hash.ToLowerInvariant()
$result = [ordered]@{
    schemaVersion = 1
    workItemId = $WorkItemId
    mode = $Mode
    target = [IO.Path]::GetRelativePath($repoRoot, $Target)
    overall = $overall
    failure = $failure
    specSha256 = $specHash
    startedAt = $startedAt.ToString('o')
    finishedAt = $finishedAt.ToString('o')
    steps = $steps
}
$jsonPath = Join-Path $runDirectory 'quality-gate.json'
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$summary = @(
    "# Quality gate: $overall"
    ''
    "- Work item: ``$WorkItemId``"
    "- Mode: ``$Mode``"
    "- Target: ``$($result.target)``"
    "- Started: $($result.startedAt)"
    "- Finished: $($result.finishedAt)"
    if ($failure) { "- Failure: $failure" }
    ''
    '| Step | Status | Exit | Log |'
    '|---|---|---:|---|'
)
foreach ($step in $steps) {
    $summary += "| $($step.name) | $($step.status) | $($step.exitCode) | ``$($step.log)`` |"
}
Set-Content -LiteralPath (Join-Path $runDirectory 'quality-gate.md') -Value $summary -Encoding utf8

Copy-Item -LiteralPath $jsonPath -Destination (Join-Path $evidenceRoot 'latest-quality-gate.json') -Force
Copy-Item -LiteralPath (Join-Path $runDirectory 'quality-gate.md') -Destination (Join-Path $evidenceRoot 'latest-quality-gate.md') -Force

$state.state = if ($overall -eq 'Passed' -and $Mode -eq 'Full') { 'verification-passed' } elseif ($overall -eq 'Failed') { 'verification-failed' } else { $state.state }
$state.lastTransitionAt = (Get-Date).ToUniversalTime().ToString('o')
$state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding utf8

Write-Host "Quality gate: $overall"
Write-Host "Evidence: $runDirectory"
if ($overall -ne 'Passed') { exit 1 }
