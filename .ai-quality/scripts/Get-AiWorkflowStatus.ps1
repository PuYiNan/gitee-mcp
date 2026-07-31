[CmdletBinding()]
param(
    [string] $WorkItemId,
    [switch] $Json
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$qualityRoot = Join-Path $repoRoot '.ai-quality'
if (-not $WorkItemId) {
    $WorkItemId = (Get-Content -Raw -LiteralPath (Join-Path $qualityRoot 'active-work-item.txt')).Trim()
}
if (-not $WorkItemId) { throw 'No active work item. Run: pwsh ./aq.ps1 new -Title <title>' }

$item = Join-Path $qualityRoot "work-items\$WorkItemId"
$statePath = Join-Path $item 'state.json'
if (-not (Test-Path -LiteralPath $statePath)) { throw "Unknown work item: $WorkItemId" }
$state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json

$allowed = switch ($state.state) {
    'discovery' { 'Inspect repository and edit spec.md only; request Requirements approval.' }
    'requirements-approved' { 'Edit plan.md and test-matrix.md only; request Plan approval.' }
    'plan-approved' { 'Finalize test-matrix.md only; request Tests approval.' }
    'implementation-authorized' { 'Implement approved scope and run checks.' }
    'verification-failed' { 'Fix recorded failures only and rerun Full verification.' }
    'verification-passed' { 'Complete delivery.md and request Delivery acceptance.' }
    'accepted' { 'No further work; create a new work item for changes.' }
    default { 'Stop: unknown state.' }
}

$result = [ordered]@{
    id = $state.id
    title = $state.title
    state = $state.state
    uiScope = $state.uiScope
    allowedAction = $allowed
    path = [IO.Path]::GetRelativePath($repoRoot, $item)
}

if ($Json) { $result | ConvertTo-Json -Depth 5 } else { $result | Format-List }
