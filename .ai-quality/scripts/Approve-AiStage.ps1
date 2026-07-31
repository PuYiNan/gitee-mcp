[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Requirements', 'Plan', 'Tests', 'Delivery')] [string] $Stage,
    [Parameter(Mandatory)] [ValidatePattern('^[a-z0-9][a-z0-9-]{2,63}$')] [string] $WorkItemId,
    [Parameter(Mandatory)] [ValidateNotNullOrEmpty()] [string] $ApprovedBy,
    [string] $Note = ''
)

function Assert-ReadyFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "Missing required artifact: $Path" }
    $content = Get-Content -Raw -LiteralPath $Path
    if ($content -match '\[TODO') { throw "Artifact still contains TODO placeholders: $Path" }
    return $content
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$item = Join-Path $repoRoot ".ai-quality\work-items\$WorkItemId"
$statePath = Join-Path $item 'state.json'
if (-not (Test-Path -LiteralPath $statePath)) { throw "Unknown work item: $WorkItemId" }
$state = Get-Content -Raw -LiteralPath $statePath | ConvertFrom-Json

$rules = @{
    Requirements = @{ From = 'discovery'; To = 'requirements-approved'; Artifact = 'spec.md' }
    Plan = @{ From = 'requirements-approved'; To = 'plan-approved'; Artifact = 'plan.md' }
    Tests = @{ From = 'plan-approved'; To = 'implementation-authorized'; Artifact = 'test-matrix.md' }
    Delivery = @{ From = 'verification-passed'; To = 'accepted'; Artifact = 'delivery.md' }
}
$rule = $rules[$Stage]
if ($state.state -ne $rule.From) {
    throw "Stage $Stage requires state '$($rule.From)', current state is '$($state.state)'."
}

$artifactPath = Join-Path $item $rule.Artifact
$content = Assert-ReadyFile $artifactPath
if ($Stage -eq 'Requirements' -and $content -notmatch 'AC-\d{3}') {
    throw 'Requirements must contain at least one AC-### acceptance criterion.'
}
if ($Stage -eq 'Delivery') {
    & (Join-Path $PSScriptRoot 'Test-AiDelivery.ps1') -WorkItemId $WorkItemId
    if ($LASTEXITCODE -ne 0) { throw 'Delivery validation failed.' }
}

$challenge = "APPROVE $WorkItemId $($Stage.ToUpperInvariant())"
Write-Host "Human approval boundary. Review $artifactPath"
$confirmation = Read-Host "Type exactly: $challenge"
if ($confirmation -cne $challenge) { throw 'Approval cancelled: confirmation did not match.' }

$approval = [ordered]@{
    schemaVersion = 1
    workItemId = $WorkItemId
    stage = $Stage
    approvedBy = $ApprovedBy
    approvedAt = (Get-Date).ToUniversalTime().ToString('o')
    artifact = $rule.Artifact
    artifactSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $artifactPath).Hash.ToLowerInvariant()
    note = $Note
}
$approvalPath = Join-Path $item "approvals\$($Stage.ToLowerInvariant()).json"
$approval | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $approvalPath -Encoding utf8

$state.state = $rule.To
$state.lastTransitionAt = (Get-Date).ToUniversalTime().ToString('o')
$state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $statePath -Encoding utf8
Write-Host "Approved $Stage; state is now $($rule.To)."
