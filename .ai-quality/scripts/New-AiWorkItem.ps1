[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateNotNullOrEmpty()] [string] $Title,
    [ValidatePattern('^[a-z0-9][a-z0-9-]{2,63}$')] [string] $Id,
    [switch] $UiScope
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$qualityRoot = Join-Path $repoRoot '.ai-quality'

if (-not $Id) {
    $slug = $Title.ToLowerInvariant() -replace '[^a-z0-9]+', '-'
    $slug = $slug.Trim('-')
    if ($slug.Length -lt 3) { $slug = 'work-item' }
    if ($slug.Length -gt 48) { $slug = $slug.Substring(0, 48).Trim('-') }
    $Id = "$(Get-Date -Format 'yyyyMMdd')-$slug"
}

$workItem = Join-Path $qualityRoot "work-items\$Id"
if (Test-Path -LiteralPath $workItem) {
    throw "Work item already exists: $Id"
}

New-Item -ItemType Directory -Path $workItem -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workItem 'approvals') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workItem 'evidence') -Force | Out-Null

$templateMap = @{
    'spec.md' = 'spec.md'
    'plan.md' = 'plan.md'
    'test-matrix.md' = 'test-matrix.md'
    'delivery.md' = 'delivery.md'
}

foreach ($entry in $templateMap.GetEnumerator()) {
    $content = Get-Content -Raw -LiteralPath (Join-Path $qualityRoot "templates\$($entry.Value)")
    $content = $content.Replace('{{TITLE}}', $Title).Replace('{{ID}}', $Id)
    Set-Content -LiteralPath (Join-Path $workItem $entry.Key) -Value $content -Encoding utf8
}

$now = (Get-Date).ToUniversalTime().ToString('o')
$state = [ordered]@{
    schemaVersion = 1
    id = $Id
    title = $Title
    state = 'discovery'
    uiScope = [bool]$UiScope
    createdAt = $now
    lastTransitionAt = $now
}
$state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $workItem 'state.json') -Encoding utf8
Set-Content -LiteralPath (Join-Path $qualityRoot 'active-work-item.txt') -Value $Id -Encoding utf8

Write-Host "Created $Id in discovery state."
Write-Host "Product-code edits are blocked. Inspect the repository and complete:"
Write-Host "  $workItem\spec.md"
