[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Path
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$qualityRoot = Join-Path $repoRoot '.ai-quality'
$activeId = (Get-Content -Raw -LiteralPath (Join-Path $qualityRoot 'active-work-item.txt')).Trim()
if (-not $activeId) { throw 'Blocked: no active AI quality work item.' }

$item = Join-Path $qualityRoot "work-items\$activeId"
$state = Get-Content -Raw -LiteralPath (Join-Path $item 'state.json') | ConvertFrom-Json
$absolutePath = if ([IO.Path]::IsPathRooted($Path)) { [IO.Path]::GetFullPath($Path) } else { [IO.Path]::GetFullPath((Join-Path $repoRoot $Path)) }
$relative = [IO.Path]::GetRelativePath($repoRoot, $absolutePath).Replace('\', '/')

if ($relative.StartsWith('../') -or $relative -eq '..') { throw "Blocked: path is outside repository: $Path" }
if ($relative -match '^\.ai-quality/work-items/[^/]+/(state\.json|approvals/|evidence/)') {
    throw "Blocked: workflow state, approvals, and evidence may only be written by the workflow CLI: $relative"
}

$activePrefix = ".ai-quality/work-items/$activeId/"
if ($relative.StartsWith($activePrefix)) {
    $artifact = $relative.Substring($activePrefix.Length)
    $allowedArtifact = switch ($state.state) {
        'discovery' { $artifact -eq 'spec.md' }
        'requirements-approved' { $artifact -in @('plan.md', 'test-matrix.md') }
        'plan-approved' { $artifact -eq 'test-matrix.md' }
        'verification-passed' { $artifact -eq 'delivery.md' }
        default { $false }
    }
    if (-not $allowedArtifact) { throw "Blocked in state '$($state.state)': $relative" }
    Write-Output "Allowed workflow artifact edit: $relative"
    exit 0
}

if ($relative.StartsWith('.ai-quality/') -or $relative -in @('AGENTS.md', 'CLAUDE.md', 'aq.ps1')) {
    throw "Blocked: workflow controls require human-reviewed changes: $relative"
}
if ($state.state -notin @('implementation-authorized', 'verification-failed')) {
    throw "Blocked: product edits require implementation-authorized; current state is '$($state.state)'."
}

Write-Output "Allowed product edit in state '$($state.state)': $relative"
